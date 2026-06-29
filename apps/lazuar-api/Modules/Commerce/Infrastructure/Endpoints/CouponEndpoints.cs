using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Infrastructure;

public static class CouponEndpoints
{
    public static RouteGroupBuilder MapCouponEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/coupons", async Task<Ok<ICollection<CouponDto>>> (
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var coupons = await queryService.GetCouponsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CouponDto>)coupons.ToList());
        });

        group.MapPost("/coupons", async Task<Ok<IdResponse>> (
            CreateCouponRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Applicable_product_ids?
                .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList() ?? new List<Guid>();

            var command = new CreateCouponCommand(
                ctx.TenantId,
                req.Code,
                req.Discount_type,
                (decimal)req.Amount,
                req.Max_uses,
                (decimal)(req.Minimum_original_price ?? 0),
                req.Expires_at?.UtcDateTime,
                productIds
            );

            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPut("/coupons/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateCouponRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Applicable_product_ids?
                .Select(pid => Guid.TryParse(pid, out var parsed) ? parsed : Guid.Empty)
                .Where(pid => pid != Guid.Empty)
                .ToList();

            var command = new UpdateCouponCommand(
                ctx.TenantId,
                id,
                req.Code,
                req.Discount_type,
                req.Amount.HasValue ? (decimal)req.Amount.Value : null,
                req.Max_uses,
                req.Minimum_original_price.HasValue ? (decimal)req.Minimum_original_price.Value : null,
                req.Expires_at?.UtcDateTime,
                productIds,
                req.Is_active
            );

            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/coupons/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new DeleteCouponCommand(ctx.TenantId, id);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        return group;
    }
}
