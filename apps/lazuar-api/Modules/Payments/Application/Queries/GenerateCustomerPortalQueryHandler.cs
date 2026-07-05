using System;
using System.Linq;
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

    public GenerateCustomerPortalQueryHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
    }

    public async Task<string> Handle(GenerateCustomerPortalQuery request, CancellationToken cancellationToken)
    {
        // For customer portal, we specifically look for Stripe as it's the only one supporting it.
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, "STRIPE", cancellationToken);

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("Stripe is not configured for this tenant (required for Customer Portal).");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        return await adapter.GenerateCustomerPortalAsync(
            config.ApiKey,
            request.CustomerEmail,
            request.ReturnUrl);
    }
}
