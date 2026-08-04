using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GenerateCheckoutSessionQueryHandler : IQueryHandler<GenerateCheckoutSessionQuery, string>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ISecretVault _secretVault;

    public GenerateCheckoutSessionQueryHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        ISecretVault secretVault)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _secretVault = secretVault;
    }

    public async Task<string> Handle(GenerateCheckoutSessionQuery request, CancellationToken cancellationToken)
    {
        var gatewayName = await ResolveGatewayNameAsync(request.TenantId, request.GatewayName, cancellationToken);

        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, gatewayName, cancellationToken);

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException($"Payment gateway '{gatewayName}' is not configured for this workspace.");
        }

        if (!config.IsActive)
        {
            throw new InvalidOperationException($"Payment gateway '{gatewayName}' is disabled for this workspace.");
        }

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        var result = await adapter.GenerateCheckoutAsync(
            plainApiKey,
            request.TenantId,
            request.Amount,
            request.Currency,
            request.ProductName,
            request.CustomerEmail,
            request.SuccessUrl,
            request.CancelUrl,
            request.Metadata,
            config.MerchantId,
            request.SetupFutureUsage,
            request.Quantity);

        if (!result.Success || string.IsNullOrEmpty(result.CheckoutUrl))
        {
            throw new InvalidOperationException($"Failed to generate checkout session: {result.Error}");
        }

        return result.CheckoutUrl;
    }

    /// <summary>
    /// product/request preference → first active tenant config → BILLPLZ last resort.
    /// </summary>
    private async Task<string> ResolveGatewayNameAsync(
        Guid tenantId,
        string? preferredGateway,
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

        return "BILLPLZ";
    }
}
