// apps/lazuar-api/Modules/Payments/Application/Queries/GenerateSystemCheckoutSessionQueryHandler.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GenerateSystemCheckoutSessionQueryHandler : IQueryHandler<GenerateSystemCheckoutSessionQuery, string>
{
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IConfiguration _configuration;

    public GenerateSystemCheckoutSessionQueryHandler(
        IPaymentGatewayFactory gatewayFactory,
        IConfiguration configuration)
    {
        _gatewayFactory = gatewayFactory;
        _configuration = configuration;
    }

    public async Task<string> Handle(GenerateSystemCheckoutSessionQuery request, CancellationToken cancellationToken)
    {
        var systemStripeKey = _configuration["LAZUAR_SYSTEM_STRIPE_SECRET_KEY"];
        if (string.IsNullOrEmpty(systemStripeKey))
        {
            throw new InvalidOperationException("System billing is not configured.");
        }

        var adapter = _gatewayFactory.GetAdapter("STRIPE");

        var result = await adapter.GenerateCheckoutAsync(
            systemStripeKey,
            request.TenantId,
            request.Amount,
            request.Currency,
            request.ProductName,
            request.CustomerEmail,
            request.SuccessUrl,
            request.CancelUrl,
            request.Metadata,
            null,
            false,
            1);

        if (!result.Success || string.IsNullOrEmpty(result.CheckoutUrl))
        {
            throw new InvalidOperationException($"Failed to generate system checkout session: {result.Error}");
        }

        return result.CheckoutUrl;
    }
}
