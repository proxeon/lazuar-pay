using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Application.Commands;

public class CreateIntegrationCheckoutCommandHandler
    : ICommandHandler<CreateIntegrationCheckoutCommand, IntegrationCheckoutResult>
{
    private static readonly HashSet<string> AllowedGateways = new(StringComparer.OrdinalIgnoreCase)
    {
        "STRIPE", "BILLPLZ", "CHIP", "RAZORPAY"
    };

    private readonly IIntegrationCheckoutSessionRepository _sessions;
    private readonly CheckoutSessionCashier _cashier;

    public CreateIntegrationCheckoutCommandHandler(
        IIntegrationCheckoutSessionRepository sessions,
        CheckoutSessionCashier cashier)
    {
        _sessions = sessions;
        _cashier = cashier;
    }

    public async Task<IntegrationCheckoutResult> Handle(
        CreateIntegrationCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var currency = request.Currency.Trim().ToUpperInvariant();
        var description = request.Description.Trim();
        var email = request.CustomerEmail.Trim();
        var successUrl = request.SuccessUrl.Trim();
        var cancelUrl = request.CancelUrl.Trim();
        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? null
            : request.CustomerName.Trim();
        if (customerName is { Length: > 120 })
            throw PaymentIntegrationException.InvalidRequest("customer_name must be at most 120 characters.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : request.IdempotencyKey.Trim();
        if (idempotencyKey is { Length: > 200 })
            throw PaymentIntegrationException.InvalidRequest("idempotency_key must be at most 200 characters.");

        string? gatewayPreferred = null;
        if (!string.IsNullOrWhiteSpace(request.GatewayName))
        {
            gatewayPreferred = request.GatewayName.Trim().ToUpperInvariant();
            if (!AllowedGateways.Contains(gatewayPreferred))
            {
                throw PaymentIntegrationException.InvalidRequest(
                    "gateway_name must be one of STRIPE, BILLPLZ, CHIP, RAZORPAY.");
            }
        }

        var clientMetadata = IntegrationCheckoutMetadata.NormalizeAndValidate(request.Metadata);
        var fingerprint = IntegrationCheckoutMetadata.ComputeFingerprint(
            request.Amount,
            currency,
            successUrl,
            cancelUrl,
            description,
            email,
            customerName,
            gatewayPreferred,
            request.SetupFutureUsage,
            clientMetadata);

        if (idempotencyKey != null)
        {
            var existing = await _sessions.GetByIdempotencyKeyAsync(
                request.OrganizationId, idempotencyKey, cancellationToken);
            if (existing != null)
                return ReplayOrConflict(existing, fingerprint);
        }

        // Resolve gateway before insert so unconfigured workspaces never get a half-open row.
        var resolvedGateway = await _cashier.ResolveGatewayNameAsync(
            request.OrganizationId,
            gatewayPreferred,
            requireActiveGateway: true,
            cancellationToken);

        var checkoutId = Guid.CreateVersion7();
        var stamped = IntegrationCheckoutMetadata.Stamp(
            clientMetadata,
            request.OrganizationId,
            checkoutId,
            customerName);

        var session = new IntegrationCheckoutSession(
            request.OrganizationId,
            request.Amount,
            currency,
            description,
            email,
            successUrl,
            cancelUrl,
            resolvedGateway,
            IntegrationCheckoutMetadata.Serialize(stamped),
            request.SetupFutureUsage,
            customerName,
            idempotencyKey,
            fingerprint,
            id: checkoutId);

        _sessions.Add(session);

        // Persist first so unique (org, idempotency_key) wins races before provider call.
        try
        {
            await _sessions.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (idempotencyKey != null)
        {
            var raced = await _sessions.GetByIdempotencyKeyAsync(
                request.OrganizationId, idempotencyKey, cancellationToken);
            if (raced != null && raced.Id != session.Id)
                return ReplayOrConflict(raced, fingerprint);

            throw;
        }

        try
        {
            var gatewayResult = await _cashier.GenerateAsync(
                request.OrganizationId,
                request.Amount,
                currency,
                description,
                email,
                successUrl,
                cancelUrl,
                stamped,
                request.SetupFutureUsage,
                quantity: 1,
                preferredGateway: resolvedGateway,
                requireActiveGateway: true,
                cancellationToken);

            session.MarkProviderIssued(gatewayResult.CheckoutUrl, gatewayResult.ProviderSessionId);
            await _sessions.SaveChangesAsync(cancellationToken);
            return Map(session);
        }
        catch (PaymentIntegrationException)
        {
            session.MarkFailed();
            try { await _sessions.SaveChangesAsync(cancellationToken); }
            catch { /* best-effort */ }
            throw;
        }
        catch (Exception)
        {
            session.MarkFailed();
            try { await _sessions.SaveChangesAsync(cancellationToken); }
            catch { /* best-effort */ }
            throw;
        }
    }

    private static IntegrationCheckoutResult ReplayOrConflict(
        IntegrationCheckoutSession existing,
        string fingerprint)
    {
        if (!string.IsNullOrEmpty(existing.RequestFingerprint)
            && !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw PaymentIntegrationException.IdempotencyConflict();
        }

        existing.TryExpireIfPast(DateTime.UtcNow);
        return Map(existing);
    }

    private static void ValidateRequest(CreateIntegrationCheckoutCommand request)
    {
        CheckoutAmountRules.ValidateAmountAndCurrency(request.Amount, request.Currency);

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 200)
        {
            throw PaymentIntegrationException.InvalidRequest(
                "description is required and must be at most 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !request.CustomerEmail.Contains('@'))
        {
            throw PaymentIntegrationException.InvalidRequest("customer_email must be a valid email address.");
        }

        if (!IsAbsoluteHttpUrl(request.SuccessUrl) || !IsAbsoluteHttpUrl(request.CancelUrl))
        {
            throw PaymentIntegrationException.UrlsRequired(
                "success_url and cancel_url must be absolute http(s) URLs.");
        }
    }

    private static bool IsAbsoluteHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme is "http" or "https";
    }

    private static IntegrationCheckoutResult Map(IntegrationCheckoutSession session) =>
        new(
            session.Id,
            session.CheckoutUrl,
            session.GatewayName,
            session.Status,
            session.Amount,
            session.Currency,
            session.ProviderSessionId,
            session.GatewayTransactionId,
            session.ExpiresAt,
            IntegrationCheckoutMetadata.Deserialize(session.MetadataJson));
}
