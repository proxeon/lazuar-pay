using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Contracts.Results;

namespace Modules.Payments.Application.Queries;

public class GenerateCheckoutSessionDetailedQueryHandler
    : IQueryHandler<GenerateCheckoutSessionDetailedQuery, GenerateCheckoutSessionResult>
{
    private readonly CheckoutSessionCashier _cashier;

    public GenerateCheckoutSessionDetailedQueryHandler(CheckoutSessionCashier cashier)
    {
        _cashier = cashier;
    }

    public Task<GenerateCheckoutSessionResult> Handle(
        GenerateCheckoutSessionDetailedQuery request,
        CancellationToken cancellationToken) =>
        _cashier.GenerateAsync(
            request.TenantId,
            request.Amount,
            request.Currency,
            request.ProductName,
            request.CustomerEmail,
            request.SuccessUrl,
            request.CancelUrl,
            request.Metadata,
            request.SetupFutureUsage,
            request.Quantity,
            request.GatewayName,
            request.RequireActiveGateway,
            cancellationToken);
}
