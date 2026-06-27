using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;

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

        return group;
    }
}
