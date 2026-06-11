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
        var config = await _configRepository.GetActiveByTenantIdAsync(request.TenantId, cancellationToken);

        if (config == null || !config.IsActive || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("Payment gateway is not configured or active for this tenant.");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        return await adapter.GenerateCustomerPortalAsync(
            config.ApiKey,
            request.CustomerEmail,
            request.ReturnUrl);
    }
}
