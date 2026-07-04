using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Messaging.Contracts;
using Modules.Communications.Application;

namespace Modules.Communications.Infrastructure.EventHandlers;

public class FulfillmentRequestedIntegrationEventHandler : IIntegrationEventHandler<FulfillmentRequestedIntegrationEvent>
{
    private readonly ICommunicationsRepository _repository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly IEventBus _eventBus;

    public FulfillmentRequestedIntegrationEventHandler(
        ICommunicationsRepository repository,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(FulfillmentRequestedIntegrationEvent @event)
    {
        if (@event.InternalTargetApp != "COMMUNICATIONS" || (@event.EventType != "reminder.due" && @event.EventType != "reminder.dunning"))
        {
            return;
        }

        using var doc = JsonDocument.Parse(@event.Payload.GetRawText());
        var root = doc.RootElement;

        if (!root.TryGetProperty("client_profile_id", out var clientProfileIdProp) ||
            !Guid.TryParse(clientProfileIdProp.GetString(), out var clientProfileId))
        {
            return;
        }

        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        if (profile == null) return;

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(@event.OrganizationId);
        var workspaceSlug = workspace?.Slug ?? "";
        var portalLink = $"https://portal.lazuar.com/{workspaceSlug}/portal";
        var channel = root.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() ?? "EMAIL" : "EMAIL";

        string subject = "";
        string emailBody = "";
        string whatsappBody = "";

        if (@event.EventType == "reminder.dunning")
        {
            var actionType = root.TryGetProperty("action_type", out var atProp) ? atProp.GetString() ?? "EMAIL" : "EMAIL";
            channel = actionType == "ALL" ? "ALL" : actionType;
            
            subject = root.TryGetProperty("subject", out var sProp) ? sProp.GetString() ?? "" : "";
            emailBody = root.TryGetProperty("email_body", out var ebProp) ? ebProp.GetString() ?? "" : "";
            whatsappBody = root.TryGetProperty("whatsapp_body", out var wbProp) ? wbProp.GetString() ?? "" : "";
        }
        else
        {
            if (!root.TryGetProperty("template_id", out var templateIdProp) || !Guid.TryParse(templateIdProp.GetString(), out var templateId)) return;
            
            var template = await _repository.GetTemplateByIdAsync(@event.OrganizationId, templateId);
            if (template == null) return;
            
            subject = template.Subject;
            emailBody = template.EmailBody;
            whatsappBody = template.WhatsAppBody;
            channel = template.Channel;
        }

        string PopulateVariables(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("{{customer_name}}", profile.Full_name, StringComparison.OrdinalIgnoreCase)
                .Replace("{{customer_email}}", profile.Email, StringComparison.OrdinalIgnoreCase)
                .Replace("{{customer_phone}}", profile.Phone ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{{business_name}}", workspace?.Name ?? "Lazuar Merchant", StringComparison.OrdinalIgnoreCase)
                .Replace("{{renewal_link}}", portalLink, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", portalLink, StringComparison.OrdinalIgnoreCase);
        }

        var dispatchEvent = new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            profile.Email,
            profile.Phone,
            PopulateVariables(subject),
            MarkdownParser.ToHtml(PopulateVariables(emailBody)),
            PopulateVariables(whatsappBody),
            channel
        );

        await _eventBus.PublishAsync(dispatchEvent);
    }
}
