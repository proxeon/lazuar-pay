// apps/lazuar-api/Modules/Payments/Infrastructure/PlatformEndpoints.cs
// Platform super-admin auth (/auth/*) lives in One: MapPlatformAuthEndpoints.
// This file only maps payment-config under the host /api/v1/platform group.
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Infrastructure;

public static class PlatformEndpoints
{
    public static RouteGroupBuilder MapPlatformEndpoints(this RouteGroupBuilder group)
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
                ctx.TenantId, req.Gateway_type, req.Api_key, req.Collection_id, req.Webhook_secret, req.Secret_key, req.Is_active);

            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        return group;
    }
}
