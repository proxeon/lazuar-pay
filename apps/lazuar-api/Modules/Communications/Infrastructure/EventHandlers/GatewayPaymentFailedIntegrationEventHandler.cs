using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Communications.Infrastructure.EventHandlers;

/// <summary>
/// Immediate Payment Failed email on a live subscription decline. Not the dunning sequence (LP-073)
/// and not terminal SUSPEND (unhooked).
/// </summary>
public class GatewayPaymentFailedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ICommerceDocumentLookup _commerceDocumentLookup;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEventBus _eventBus;

    public GatewayPaymentFailedIntegrationEventHandler(
        CommunicationsDbContext dbContext,
        ICommerceDocumentLookup commerceDocumentLookup,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        IConfiguration configuration,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _commerceDocumentLookup = commerceDocumentLookup;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
        _configuration = configuration;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(GatewayPaymentFailedIntegrationEvent @event)
    {
        if (!TryResolveSubscriptionId(@event, out var subscriptionId))
        {
            return;
        }

        var context = await _commerceDocumentLookup.GetSubscriptionCommsContextAsync(
            @event.OrganizationId, subscriptionId);
        if (context == null || string.Equals(context.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var profile = await _crmQueryService.GetClientProfileAsync(context.ClientProfileId);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Email))
        {
            return;
        }

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == @event.OrganizationId && t.Name == "Payment Failed");
        if (template == null)
        {
            return;
        }

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(@event.OrganizationId);
        var slug = workspace?.Slug ?? "";
        var businessName = string.IsNullOrWhiteSpace(workspace?.Name) ? "Business" : workspace.Name;
        var customerName = string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name;
        var planName = string.IsNullOrWhiteSpace(context.ProductName) ? "{{plan_name}}" : context.ProductName;

        var portalBase = (_configuration["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        var updatePaymentLink = $"{portalBase}/{slug}/update-payment/{subscriptionId}";
        var portalMagicLink = $"{portalBase}/{slug}/portal?token={_tokenService.GenerateToken(subscriptionId)}";

        string Populate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("{{customer_name}}", customerName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{business_name}}", businessName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{plan_name}}", planName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{renewal_link}}", updatePaymentLink, StringComparison.OrdinalIgnoreCase)
                .Replace("{{update_payment_link}}", updatePaymentLink, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", portalMagicLink, StringComparison.OrdinalIgnoreCase);
        }

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            profile.Email,
            null,
            Populate(template.Subject ?? ""),
            MarkdownParser.ToHtml(Populate(template.EmailBody ?? "")),
            string.IsNullOrEmpty(template.WhatsAppBody) ? null : Populate(template.WhatsAppBody),
            template.Channel ?? "EMAIL"));
        await _dbContext.SaveChangesAsync();
    }

    private static bool TryResolveSubscriptionId(GatewayPaymentFailedIntegrationEvent @event, out Guid subscriptionId)
    {
        subscriptionId = default;
        if (@event.Metadata == null)
        {
            return false;
        }

        if (@event.Metadata.TryGetValue("subscription_id", out var subIdStr)
            && Guid.TryParse(subIdStr, out subscriptionId))
        {
            return true;
        }

        if (@event.Metadata.TryGetValue("receipt", out var receipt)
            && Guid.TryParse(receipt, out subscriptionId))
        {
            return true;
        }

        return false;
    }
}
