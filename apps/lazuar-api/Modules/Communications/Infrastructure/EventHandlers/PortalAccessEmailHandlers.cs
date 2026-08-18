using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

/// <summary>
/// First-activation and public request send the Portal Access catalog template with a 24h token.
/// </summary>
public class PortalAccessEmailHandlers :
    IIntegrationEventHandler<SubscriptionActivatedIntegrationEvent>,
    IIntegrationEventHandler<PortalMagicLinkRequestedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEventBus _eventBus;

    public PortalAccessEmailHandlers(
        CommunicationsDbContext dbContext,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        IConfiguration configuration,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
        _configuration = configuration;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(SubscriptionActivatedIntegrationEvent @event)
    {
        if (!@event.IsFirstPayment) return;

        await DispatchPortalAccessAsync(@event.OrganizationId, @event.SubscriptionId, @event.ClientProfileId);
    }

    public Task HandleAsync(PortalMagicLinkRequestedIntegrationEvent @event) =>
        DispatchPortalAccessAsync(@event.OrganizationId, @event.SubscriptionId, @event.ClientProfileId);

    private async Task DispatchPortalAccessAsync(Guid organizationId, Guid subscriptionId, Guid clientProfileId)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(organizationId, clientProfileId);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Email))
        {
            return;
        }

        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Name == "Portal Access");
        if (template == null)
        {
            return;
        }

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(organizationId);
        var slug = workspace?.Slug ?? "";
        var businessName = string.IsNullOrWhiteSpace(workspace?.Name) ? "Business" : workspace.Name;
        var customerName = string.IsNullOrWhiteSpace(profile.Full_name) ? "Customer" : profile.Full_name;

        var portalBase = BuildingBlocks.Infrastructure.AppClientUrl.Resolve(_configuration);
        var token = MagicLinkTokens.ToQueryValue(_tokenService.GenerateToken(subscriptionId));
        var portalMagicLink = $"{portalBase}/{slug}/portal?token={token}";

        string Populate(string text, bool htmlEncode)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var name = htmlEncode ? MessageTemplateHydrator.HtmlEncode(customerName) : customerName;
            var business = htmlEncode ? MessageTemplateHydrator.HtmlEncode(businessName) : businessName;
            var link = htmlEncode ? MessageTemplateHydrator.SafeHttpUrl(portalMagicLink) : portalMagicLink;
            return text
                .Replace("{{customer_name}}", name, StringComparison.OrdinalIgnoreCase)
                .Replace("{{business_name}}", business, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", link, StringComparison.OrdinalIgnoreCase);
        }

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            organizationId,
            profile.Email,
            null,
            Populate(template.Subject ?? "", htmlEncode: false),
            MarkdownParser.ToHtml(Populate(template.EmailBody ?? "", htmlEncode: true)),
            null,
            template.Channel ?? "EMAIL"));
        await _dbContext.SaveChangesAsync();
    }
}
