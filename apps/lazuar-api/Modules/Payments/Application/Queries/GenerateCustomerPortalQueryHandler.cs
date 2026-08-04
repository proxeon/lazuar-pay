using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GenerateCustomerPortalQueryHandler : IQueryHandler<GenerateCustomerPortalQuery, string>
{
    private readonly ITenantPaymentConfigRepository _configRepository;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ISecretVault _secretVault;

    public GenerateCustomerPortalQueryHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory,
        ISecretVault secretVault)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
        _secretVault = secretVault;
    }

    public async Task<string> Handle(GenerateCustomerPortalQuery request, CancellationToken cancellationToken)
    {
        // For customer portal, we specifically look for Stripe as it's the only one supporting it.
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, "STRIPE", cancellationToken);

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("Stripe is not configured for this tenant (required for Customer Portal).");
        }

        if (!config.IsActive)
        {
            throw new InvalidOperationException("Stripe is disabled for this tenant.");
        }

        var plainApiKey = _secretVault.DecryptOrPlaintext(config.ApiKey);
        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        return await adapter.GenerateCustomerPortalAsync(
            plainApiKey,
            request.CustomerEmail,
            request.ReturnUrl);
    }
}
