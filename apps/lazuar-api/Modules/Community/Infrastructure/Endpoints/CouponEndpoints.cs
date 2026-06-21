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
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure;

public static class CouponEndpoints
{
    public static RouteGroupBuilder MapCouponEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/coupons", async Task<Ok<ICollection<CouponDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var coupons = await queryService.GetCouponsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CouponDto>)coupons.ToList());
        });

        group.MapPost("/coupons", async Task<Ok<IdResponse>> (
            CreateCouponRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new CreateCouponCommand(
                ctx.TenantId,
                req.Code,
                Enum.Parse<DiscountType>(req.Discount_type, ignoreCase: true),
                (decimal)req.Amount,
                req.Max_uses,
                req.Expires_at?.UtcDateTime,
                req.Minimum_original_price.HasValue ? (decimal)req.Minimum_original_price.Value : 0,
                req.Applicable_plan_ids?.Select(Guid.Parse).ToList());
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPut("/coupons/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdateCouponRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new UpdateCouponCommand(
                ctx.TenantId,
                id,
                req.Code,
                req.Discount_type,
                req.Amount.HasValue ? (decimal)req.Amount.Value : null,
                req.Max_uses ?? 0,
                req.Minimum_original_price.HasValue ? (decimal)req.Minimum_original_price.Value : 0m,
                req.Expires_at?.UtcDateTime,
                req.Is_active,
                req.Applicable_plan_ids?.Select(Guid.Parse).ToList());
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/coupons/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new ArchiveCouponCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "archived" });
        });

        return group;
    }
}
