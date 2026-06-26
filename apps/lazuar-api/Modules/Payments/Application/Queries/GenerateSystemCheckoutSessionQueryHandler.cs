using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GenerateSystemCheckoutSessionQueryHandler : IQueryHandler<GenerateSystemCheckoutSessionQuery, string>
{
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly ITenantPaymentConfigRepository _configRepository;

    public GenerateSystemCheckoutSessionQueryHandler(
        IPaymentGatewayFactory gatewayFactory,
        ITenantPaymentConfigRepository configRepository)
    {
        _gatewayFactory = gatewayFactory;
        _configRepository = configRepository;
    }

    public async Task<string> Handle(GenerateSystemCheckoutSessionQuery request, CancellationToken cancellationToken)
    {
        var systemId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var config = await _configRepository.GetActiveByTenantIdAsync(systemId, cancellationToken);

        if (config == null || !config.IsActive)
        {
            throw new InvalidOperationException("Platform payment gateway is not configured.");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        if (!request.Metadata.ContainsKey("tenant_id")) 
            request.Metadata.Add("tenant_id", request.TenantId.ToString());
            
        if (!request.Metadata.ContainsKey("type")) 
            request.Metadata.Add("type", "utility_credit_topup");

        var result = await adapter.GenerateCheckoutAsync(
            config.ApiKey ?? "",
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
}
