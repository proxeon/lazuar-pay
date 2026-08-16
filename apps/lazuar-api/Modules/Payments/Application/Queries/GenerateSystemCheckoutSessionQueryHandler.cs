using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Application.Queries;

public class GenerateSystemCheckoutSessionQueryHandler : IQueryHandler<GenerateSystemCheckoutSessionQuery, string>
{
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly ISecretVault _secretVault;

    public GenerateSystemCheckoutSessionQueryHandler(
        IPaymentGatewayFactory gatewayFactory,
        ITenantPaymentConfigRepository configRepository,
        ISecretVault secretVault)
    {
        _gatewayFactory = gatewayFactory;
        _configRepository = configRepository;
        _secretVault = secretVault;
    }

    public async Task<string> Handle(GenerateSystemCheckoutSessionQuery request, CancellationToken cancellationToken)
    {
        if (request.Metadata == null
            || !request.Metadata.TryGetValue("type", out var checkoutType)
            || string.IsNullOrWhiteSpace(checkoutType))
        {
            throw new InvalidOperationException("Platform checkout metadata 'type' is required.");
        }

        var systemId = PlatformCheckoutTypes.SystemOrganizationId;
        var config = await ResolvePlatformGatewayAsync(systemId, request.GatewayName, cancellationToken);

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey!);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        if (!request.Metadata.ContainsKey("tenant_id"))
            request.Metadata.Add("tenant_id", request.TenantId.ToString());

        var result = await adapter.GenerateCheckoutAsync(
            plainApiKey,
            systemId,
            request.Amount,
            request.Currency,
            request.ProductName,
            request.CustomerEmail,
            request.SuccessUrl,
            request.CancelUrl,
            request.Metadata,
            config.MerchantId,
            false,
            1);

        if (!result.Success || string.IsNullOrEmpty(result.CheckoutUrl))
        {
            throw new InvalidOperationException($"Failed to generate platform checkout session: {result.Error}");
        }

        return result.CheckoutUrl;
    }

    private async Task<TenantPaymentConfiguration> ResolvePlatformGatewayAsync(
        Guid systemId,
        string? gatewayName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(gatewayName))
        {
            var named = await _configRepository.GetByTenantAndGatewayAsync(systemId, gatewayName, cancellationToken);
            if (named == null || string.IsNullOrEmpty(named.ApiKey))
                throw new InvalidOperationException($"Platform payment gateway '{gatewayName}' is not configured.");
            if (!named.IsActive)
                throw new InvalidOperationException($"Platform payment gateway '{gatewayName}' is disabled.");
            return named;
        }

        var all = await _configRepository.GetAllByTenantIdAsync(systemId, cancellationToken);
        var firstActive = all
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.ApiKey))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.GatewayType, StringComparer.Ordinal)
            .FirstOrDefault();

        if (firstActive == null)
            throw new InvalidOperationException("Platform payment gateway is not configured.");

        return firstActive;
    }
}
