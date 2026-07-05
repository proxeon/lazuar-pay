using System.Collections.Generic;
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
        group.MapGet("/payment-config", async Task<Ok<IEnumerable<PaymentConfigDto>>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var query = new GetPaymentConfigQuery(ctx.TenantId);
            var configs = await mediator.Send(query);
            return TypedResults.Ok(configs);
        });

        group.MapPut("/payment-config", async Task<Ok<StatusResponse>> (
            SavePaymentConfigRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new UpdatePaymentConfigCommand(
                ctx.TenantId, 
                req.Gateway_type, 
                req.Api_key, 
                req.Collection_id, 
                req.Webhook_secret, 
                req.Secret_key);
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        return group;
    }
}
