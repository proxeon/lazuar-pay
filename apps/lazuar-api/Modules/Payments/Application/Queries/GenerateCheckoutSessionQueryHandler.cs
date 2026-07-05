using System;
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

    public GenerateCheckoutSessionQueryHandler(
        ITenantPaymentConfigRepository configRepository,
        IPaymentGatewayFactory gatewayFactory)
    {
        _configRepository = configRepository;
        _gatewayFactory = gatewayFactory;
    }

    public async Task<string> Handle(GenerateCheckoutSessionQuery request, CancellationToken cancellationToken)
    {
        var config = await _configRepository.GetByTenantAndGatewayAsync(request.TenantId, request.GatewayName, cancellationToken);

        if (config == null || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException($"Payment gateway '{request.GatewayName}' is not configured for this workspace.");
        }

        var adapter = _gatewayFactory.GetAdapter(config.GatewayType);

        var result = await adapter.GenerateCheckoutAsync(
            config.ApiKey,
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
}
