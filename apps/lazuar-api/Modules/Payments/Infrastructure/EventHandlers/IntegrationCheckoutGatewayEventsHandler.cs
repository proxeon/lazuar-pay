using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Infrastructure.EventHandlers;

/// <summary>
/// M2M / integrator checkouts: on gateway money events, mark
/// <see cref="IntegrationCheckoutSession"/> and enqueue workspace outbound
/// <c>payment.completed</c> / <c>payment.failed</c> (TargetUrl null → One fan-out).
/// Does not require Commerce products or fulfillment URLs.
/// </summary>
public class IntegrationCheckoutGatewayEventsHandler :
    IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>,
    IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>
{
    public const string EventTypeCompleted = "payment.completed";
    public const string EventTypeFailed = "payment.failed";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IIntegrationCheckoutSessionRepository _sessions;
    private readonly IEventBus _eventBus;
    private readonly ILogger<IntegrationCheckoutGatewayEventsHandler> _logger;

    public IntegrationCheckoutGatewayEventsHandler(
        IIntegrationCheckoutSessionRepository sessions,
        [FromKeyedServices("PaymentsEventBus")] IEventBus eventBus,
        ILogger<IntegrationCheckoutGatewayEventsHandler> logger)
    {
        _sessions = sessions;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var session = await ResolveSessionAsync(
            @event.OrganizationId,
            @event.Metadata,
            @event.GatewayTransactionId);
        if (session is null)
        {
            return;
        }

        // Idempotent: only open → completed emits outbound once.
        if (session.Status != IntegrationCheckoutSession.StatusOpen)
        {
            _logger.LogDebug(
                "Integration checkout {CheckoutId} status is {Status}; skipping duplicate payment.completed outbound.",
                session.Id, session.Status);
            return;
        }

        session.MarkCompleted(@event.GatewayTransactionId);

        var metadata = IntegrationCheckoutMetadata.Deserialize(session.MetadataJson);
        var payload = BuildPayload(
            eventId: @event.Id,
            session: session,
            gatewayTransactionId: @event.GatewayTransactionId,
            amount: @event.AmountPaid,
            currency: @event.Currency,
            status: IntegrationCheckoutSession.StatusCompleted,
            metadata: metadata);

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId,
            TargetUrl: null,
            EventTypeCompleted,
            payload));

        await _sessions.SaveChangesAsync();
    }

    public async Task HandleAsync(GatewayPaymentFailedIntegrationEvent @event)
    {
        var session = await ResolveSessionAsync(
            @event.OrganizationId,
            @event.Metadata,
            @event.GatewayTransactionId);
        if (session is null)
        {
            return;
        }

        if (session.Status != IntegrationCheckoutSession.StatusOpen)
        {
            _logger.LogDebug(
                "Integration checkout {CheckoutId} status is {Status}; skipping duplicate payment.failed outbound.",
                session.Id, session.Status);
            return;
        }

        session.MarkFailed();

        var metadata = IntegrationCheckoutMetadata.Deserialize(session.MetadataJson);
        // Failed event has no amount/currency — prefer session row values.
        var payload = BuildPayload(
            eventId: @event.Id,
            session: session,
            gatewayTransactionId: @event.GatewayTransactionId,
            amount: session.Amount,
            currency: session.Currency,
            status: IntegrationCheckoutSession.StatusFailed,
            metadata: metadata);

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            @event.OrganizationId,
            TargetUrl: null,
            EventTypeFailed,
            payload));

        await _sessions.SaveChangesAsync();
    }

    /// <summary>
    /// Prefer metadata.checkout_id; if missing (Billplz stripped body), resolve by
    /// ProviderSessionId == GatewayTransactionId (bill id).
    /// </summary>
    private async Task<IntegrationCheckoutSession?> ResolveSessionAsync(
        Guid organizationId,
        Dictionary<string, string>? metadata,
        string? gatewayTransactionId)
    {
        if (TryResolveCheckoutId(metadata, out var checkoutId))
        {
            var byId = await _sessions.GetByIdAsync(organizationId, checkoutId);
            if (byId is not null)
            {
                return byId;
            }

            _logger.LogDebug(
                "Integration checkout {CheckoutId} not found for org {OrganizationId}; trying ProviderSessionId fallback.",
                checkoutId, organizationId);
        }

        if (!string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            var byProvider = await _sessions.GetByProviderSessionIdAsync(
                organizationId, gatewayTransactionId);
            if (byProvider is not null)
            {
                _logger.LogDebug(
                    "Resolved IntegrationCheckoutSession {CheckoutId} via ProviderSessionId={ProviderSessionId} for org {OrganizationId}.",
                    byProvider.Id, gatewayTransactionId, organizationId);
                return byProvider;
            }
        }

        return null;
    }

    private static bool TryResolveCheckoutId(Dictionary<string, string>? metadata, out Guid checkoutId)
    {
        checkoutId = default;
        if (metadata is null || metadata.Count == 0)
        {
            return false;
        }

        if (metadata.TryGetValue("checkout_id", out var raw)
            && Guid.TryParse(raw, out checkoutId))
        {
            return true;
        }

        // Case-insensitive fallback for adapters that re-key metadata.
        foreach (var (key, value) in metadata)
        {
            if (key.Equals("checkout_id", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(value, out checkoutId))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonElement BuildPayload(
        Guid eventId,
        IntegrationCheckoutSession session,
        string gatewayTransactionId,
        decimal amount,
        string currency,
        string status,
        Dictionary<string, string> metadata)
    {
        var payloadObj = new
        {
            event_id = eventId.ToString(),
            checkout_id = session.Id.ToString(),
            gateway = session.GatewayName,
            gateway_transaction_id = gatewayTransactionId,
            provider_session_id = session.ProviderSessionId,
            amount,
            currency,
            status,
            metadata,
            description = session.Description,
            customer_email = session.CustomerEmail
        };

        return JsonSerializer.SerializeToElement(payloadObj, PayloadJsonOptions);
    }
}
