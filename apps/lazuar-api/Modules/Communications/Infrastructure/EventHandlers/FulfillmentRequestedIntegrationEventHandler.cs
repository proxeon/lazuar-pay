using System;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts;
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
    private readonly CommunicationsDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly ILogger<FulfillmentRequestedIntegrationEventHandler> _logger;

    public FulfillmentRequestedIntegrationEventHandler(
        ICommunicationsRepository repository,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        CommunicationsDbContext db,
        [FromKeyedServices("CommunicationsEventBus")] IEventBus eventBus,
        IConfiguration configuration,
        IMagicLinkTokenService tokenService,
        ILogger<FulfillmentRequestedIntegrationEventHandler> logger)
    {
        _repository = repository;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _db = db;
        _eventBus = eventBus;
        _configuration = configuration;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task HandleAsync(FulfillmentRequestedIntegrationEvent @event)
    {
        if (@event.InternalTargetApp != "COMMUNICATIONS" || (@event.EventType != "reminder.due" && @event.EventType != "reminder.dunning"))
        {
            return;
        }

        using var doc = JsonDocument.Parse(@event.Payload.GetRawText());
        var root = doc.RootElement;
        var isDunning = @event.EventType == "reminder.dunning";
        var subIdStr = root.TryGetProperty("subscription_id", out var sidProp) ? sidProp.GetString() ?? "" : "";
        var rawClientProfileId = root.TryGetProperty("client_profile_id", out var clientProfileIdProp)
            ? clientProfileIdProp.GetString()
            : null;

        if (!Guid.TryParse(rawClientProfileId, out var clientProfileId))
        {
            if (!isDunning) return;
            _logger.LogError(
                "Dunning hydrate failed: missing client_profile_id. OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} ClientProfileId={ClientProfileId}",
                @event.OrganizationId, subIdStr, rawClientProfileId);
            throw new InvalidOperationException(
                $"Dunning hydrate failed: missing or invalid client_profile_id for organization {@event.OrganizationId} subscription {subIdStr}.");
        }

        var profile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        if (profile == null)
        {
            if (!isDunning) return;
            _logger.LogError(
                "Dunning hydrate failed: CRM profile missing. OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} ClientProfileId={ClientProfileId}",
                @event.OrganizationId, subIdStr, clientProfileId);
            throw new InvalidOperationException(
                $"Dunning hydrate failed: CRM profile {clientProfileId} not found for organization {@event.OrganizationId} subscription {subIdStr}.");
        }

        if (isDunning && string.IsNullOrWhiteSpace(profile.Email))
        {
            _logger.LogError(
                "Dunning hydrate failed: profile email empty. OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} ClientProfileId={ClientProfileId}",
                @event.OrganizationId, subIdStr, clientProfileId);
            throw new InvalidOperationException(
                $"Dunning hydrate failed: CRM profile {clientProfileId} has no email for organization {@event.OrganizationId} subscription {subIdStr}.");
        }

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(@event.OrganizationId);
        var workspaceSlug = workspace?.Slug ?? "";

        var portalBase = (_configuration["App:ClientUrl"] ?? "https://portal.lazuar.com").TrimEnd('/');
        var portalLink = $"{portalBase}/{workspaceSlug}/portal";
        var updatePaymentLink = $"{portalBase}/{workspaceSlug}/update-payment/{subIdStr}";

        string portalMagicLink = portalLink;
        if (Guid.TryParse(subIdStr, out var subscriptionId))
        {
            var token = _tokenService.GenerateToken(subscriptionId);
            portalMagicLink = $"{portalBase}/{workspaceSlug}/portal?token={token}";
        }

        var planName = root.TryGetProperty("plan_name", out var planProp) ? planProp.GetString() ?? "" : "";
        var amount = ReadNumericString(root, "amount");
        var totalPrice = root.TryGetProperty("total_price", out var totalProp)
            ? (totalProp.ValueKind == JsonValueKind.String ? totalProp.GetString() ?? amount : totalProp.ToString())
            : amount;
        var currency = root.TryGetProperty("currency", out var currProp) ? currProp.GetString() ?? "" : "";
        var daysOverdue = root.TryGetProperty("days_overdue", out var daysProp)
            ? (daysProp.ValueKind == JsonValueKind.String ? daysProp.GetString() ?? "0" : daysProp.ToString())
            : "0";

        var channel = root.TryGetProperty("channel", out var channelProp) ? channelProp.GetString() ?? "EMAIL" : "EMAIL";

        string subject = "";
        string emailBody = "";
        string whatsappBody = "";

        if (isDunning)
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
                .Replace("{{plan_name}}", planName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{amount}}", amount, StringComparison.OrdinalIgnoreCase)
                .Replace("{{total_price}}", totalPrice, StringComparison.OrdinalIgnoreCase)
                .Replace("{{currency}}", currency, StringComparison.OrdinalIgnoreCase)
                .Replace("{{days_overdue}}", daysOverdue, StringComparison.OrdinalIgnoreCase)
                .Replace("{{renewal_link}}", portalLink, StringComparison.OrdinalIgnoreCase)
                .Replace("{{portal_magic_link}}", portalMagicLink, StringComparison.OrdinalIgnoreCase)
                .Replace("{{update_payment_link}}", updatePaymentLink, StringComparison.OrdinalIgnoreCase);
        }

        var populatedSubject = PopulateVariables(subject);
        var populatedHtml = MarkdownParser.ToHtml(PopulateVariables(emailBody));
        var emailChannel = channel is "EMAIL" or "ALL";

        if (isDunning &&
            ((string.IsNullOrWhiteSpace(populatedSubject) && string.IsNullOrWhiteSpace(populatedHtml))
             || (emailChannel && string.IsNullOrWhiteSpace(populatedHtml))))
        {
            _logger.LogError(
                "Dunning hydrate failed: empty EMAIL subject/body. OrganizationId={OrganizationId} SubscriptionId={SubscriptionId} ClientProfileId={ClientProfileId}",
                @event.OrganizationId, subIdStr, clientProfileId);
            throw new InvalidOperationException(
                $"Dunning hydrate failed: empty EMAIL subject/body for organization {@event.OrganizationId} subscription {subIdStr}.");
        }

        var dispatchEvent = new DispatchMessageIntegrationEvent(
            @event.OrganizationId,
            profile.Email,
            profile.Phone,
            populatedSubject,
            populatedHtml,
            PopulateVariables(whatsappBody),
            channel
        );

        await _eventBus.PublishAsync(dispatchEvent);
        await _db.SaveChangesAsync();
    }

    private static string ReadNumericString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop)) return "";
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.GetRawText(),
            _ => prop.ToString()
        };
    }
}
