using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Payments.Application.Commands;
using Modules.Payments.Application.Queries;

namespace Modules.Commerce.Infrastructure;

public static class PaymentConfigEndpoints
{
    public static RouteGroupBuilder MapPaymentConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/payment-config", async Task<Results<Ok<PaymentConfigDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var query = new GetPaymentConfigQuery(ctx.TenantId);
            var config = await mediator.Send(query);
            return config != null ? TypedResults.Ok(config) : TypedResults.NotFound();
        });

        group.MapPut("/payment-config", async Task<Ok<StatusResponse>> (
            SavePaymentConfigRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var estimatedFee = req.Estimated_fee_percentage.HasValue ? (decimal)req.Estimated_fee_percentage.Value : 0m;
            var fixedFee = req.Fixed_fee.HasValue ? (decimal)req.Fixed_fee.Value : 0m;
            var taxRate = req.Tax_rate.HasValue ? (decimal)req.Tax_rate.Value : 0m;

            var command = new UpdatePaymentConfigCommand(
                ctx.TenantId, 
                req.Gateway_type, 
                req.Api_key, 
                req.Collection_id, 
                req.Webhook_secret, 
                req.Secret_key, 
                req.Is_active,
                estimatedFee,
                fixedFee,
                taxRate);
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        return group;
    }
}
