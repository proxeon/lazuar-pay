using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;
using Modules.Communications.Application;
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
        var toEmail = profile?.Email;
        if (profile == null || string.IsNullOrWhiteSpace(toEmail))
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
        var portalBase = (_configuration["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        var token = _tokenService.GenerateToken(subscriptionId);
        var links = MessageLinkBuilder.Build(portalBase, workspace?.Slug ?? "", subscriptionId.ToString(), token);

        var ctx = new MessageTemplateContext(
            CustomerName: string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name,
            CustomerEmail: toEmail,
            CustomerPhone: profile.Phone ?? "",
            BusinessName: string.IsNullOrWhiteSpace(workspace?.Name) ? "Lazuar Merchant" : workspace.Name,
            PlanName: context.ProductName ?? "",
            Amount: "",
            TotalPrice: "",
            Currency: "",
            DaysOverdue: "",
            CurrentPeriodEnd: "",
            RenewalLink: links.RenewalLink,
            PortalMagicLink: links.PortalMagicLink,
            UpdatePaymentLink: links.UpdatePaymentLink);

        var whatsapp = MessageTemplateHydrator.Populate(template.WhatsAppBody, ctx);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            toEmail,
            null,
            MessageTemplateHydrator.Populate(template.Subject, ctx),
            MarkdownParser.ToHtml(MessageTemplateHydrator.Populate(template.EmailBody, ctx)),
            string.IsNullOrEmpty(whatsapp) ? null : whatsapp,
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
