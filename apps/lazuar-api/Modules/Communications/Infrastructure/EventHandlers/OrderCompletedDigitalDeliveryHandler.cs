using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Communications.Infrastructure.EventHandlers;

/// <summary>
/// On order completion, send Digital Product Delivery only when the product has an https fulfillment target.
/// </summary>
public class OrderCompletedDigitalDeliveryHandler : IIntegrationEventHandler<OrderCompletedIntegrationEvent>
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderCompletedDigitalDeliveryHandler> _logger;

    public OrderCompletedDigitalDeliveryHandler(
        CommunicationsDbContext dbContext,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus,
        IConfiguration configuration,
        ILogger<OrderCompletedDigitalDeliveryHandler> logger)
    {
        _dbContext = dbContext;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _eventBus = eventBus;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCompletedIntegrationEvent @event)
    {
        var template = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.OrganizationId == @event.OrganizationId && t.Name == "Digital Product Delivery");

        var fulfillmentUrl = FirstHttpsFulfillment(@event.FulfillmentTargets);
        if (fulfillmentUrl is null)
        {
            _logger.LogDebug(
                "OrderCompleted digital delivery skipped: no https fulfillment target for order {OrderId}.",
                @event.OrderId);
            return;
        }

        if (template == null)
        {
            _logger.LogDebug(
                "OrderCompleted digital delivery skipped: template missing for org {OrgId}.",
                @event.OrganizationId);
            return;
        }

        var profile = await _crmQueryService.GetClientProfileAsync(@event.OrganizationId, @event.ClientProfileId);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Email))
        {
            return;
        }

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(@event.OrganizationId);
        var workspaceSlug = workspace?.Slug ?? "";
        var businessName = workspace?.Name ?? "Business";
        var portalBase = BuildingBlocks.Infrastructure.AppClientUrl.Resolve(_configuration);
        var portalLink = string.IsNullOrEmpty(workspaceSlug)
            ? portalBase
            : $"{portalBase}/{workspaceSlug}/portal";
        var planName = string.IsNullOrWhiteSpace(@event.ProductName) ? "your purchase" : @event.ProductName;

        string Populate(string text, bool htmlEncode)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var name = htmlEncode
                ? MessageTemplateHydrator.HtmlEncode(profile.Full_name ?? "Customer")
                : (profile.Full_name ?? "Customer");
            var business = htmlEncode ? MessageTemplateHydrator.HtmlEncode(businessName) : businessName;
            var plan = htmlEncode ? MessageTemplateHydrator.HtmlEncode(planName) : planName;
            return text
                .Replace("{{customer_name}}", name, StringComparison.OrdinalIgnoreCase)
                .Replace("{{business_name}}", business, StringComparison.OrdinalIgnoreCase)
                .Replace("{{plan_name}}", plan, StringComparison.OrdinalIgnoreCase)
                .Replace("{{fulfillment_url}}", htmlEncode ? MessageTemplateHydrator.SafeHttpUrl(fulfillmentUrl) : fulfillmentUrl, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", htmlEncode ? MessageTemplateHydrator.SafeHttpUrl(portalLink) : portalLink, StringComparison.OrdinalIgnoreCase);
        }

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            profile.Email,
            profile.Phone,
            Populate(template.Subject, htmlEncode: false),
            MarkdownParser.ToHtml(Populate(template.EmailBody, htmlEncode: true)),
            Populate(template.WhatsAppBody, htmlEncode: false),
            template.Channel));
    }

    internal static string? FirstHttpsFulfillment(IEnumerable<string>? targets)
    {
        if (targets is null) return null;
        foreach (var raw in targets)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var target = raw.Trim();
            if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase)) continue;
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                return target;
            }
        }

        return null;
    }
}
