using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Results;

namespace Modules.Payments.Application.Services;

/// <summary>
/// Shared gateway checkout generation used by string query, detailed query, and M2M create.
/// </summary>
public sealed class CheckoutSessionCashier
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ISecretVault _secretVault;

    public CheckoutSessionCashier(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        ISecretVault secretVault)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _secretVault = secretVault;
    }

    public async Task<GenerateCheckoutSessionResult> GenerateAsync(
        Guid tenantId,
        decimal amount,
        string currency,
        string productName,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        bool setupFutureUsage,
        int quantity,
        string? preferredGateway,
        bool requireActiveGateway,
        CancellationToken cancellationToken)
    {
        var gatewayName = await ResolveGatewayNameAsync(
            tenantId, preferredGateway, requireActiveGateway, cancellationToken);

        var config = await _configRepository.GetByTenantAndGatewayAsync(
            tenantId, gatewayName, cancellationToken);

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            if (requireActiveGateway)
                throw PaymentIntegrationException.PaymentsNotConfigured(gatewayName);

            throw new InvalidOperationException(
                $"Payment gateway '{gatewayName}' is not configured for this workspace.");
        }

        if (!config.IsActive)
        {
            if (requireActiveGateway)
                throw PaymentIntegrationException.PaymentsNotConfigured(gatewayName);

            throw new InvalidOperationException(
                $"Payment gateway '{gatewayName}' is disabled for this workspace.");
        }

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        var result = await adapter.GenerateCheckoutAsync(
            plainApiKey,
            tenantId,
            amount,
            currency,
            productName,
            customerEmail,
            successUrl,
            cancelUrl,
            metadata,
            config.MerchantId,
            setupFutureUsage,
            quantity);

        if (!result.Success || string.IsNullOrEmpty(result.CheckoutUrl))
        {
            if (requireActiveGateway)
                throw PaymentIntegrationException.GatewayError(result.Error);

            throw new InvalidOperationException($"Failed to generate checkout session: {result.Error}");
        }

        return new GenerateCheckoutSessionResult(
            result.CheckoutUrl,
            result.SessionId,
            config.GatewayType.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Explicit preferred → first active tenant config → optional BILLPLZ last resort (legacy only).
    /// </summary>
    public async Task<string> ResolveGatewayNameAsync(
        Guid tenantId,
        string? preferredGateway,
        bool requireActiveGateway,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredGateway))
        {
            return preferredGateway.Trim().ToUpperInvariant();
        }

        var configs = await _configRepository.GetAllByTenantIdAsync(tenantId, cancellationToken);
        var firstActive = configs.FirstOrDefault(c => c.IsActive && !string.IsNullOrWhiteSpace(c.ApiKey));
        if (firstActive != null && !string.IsNullOrWhiteSpace(firstActive.GatewayType))
        {
            return firstActive.GatewayType.Trim().ToUpperInvariant();
        }

        if (requireActiveGateway)
        {
            throw PaymentIntegrationException.PaymentsNotConfigured();
        }

        return "BILLPLZ";
    }
}
