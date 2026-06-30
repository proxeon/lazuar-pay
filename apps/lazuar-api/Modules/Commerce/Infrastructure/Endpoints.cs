using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.Application;
using MediatR;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Application.Queries;
using Lazuar.ApiTypes;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Modules.Commerce.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/commerce").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/commerce");

        adminGroup.MapProductEndpoints();
        adminGroup.MapDunningCampaignEndpoints();
        adminGroup.MapPaymentConfigEndpoints();
        
        adminGroup.MapSubscriberEndpoints();
        adminGroup.MapTransactionEndpoints();
        adminGroup.MapCouponEndpoints();
        adminGroup.MapStatsEndpoints();

        adminGroup.MapPost("/custom-checkouts", async Task<Ok<IdResponse>> (
            CreateCustomCheckoutRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var lineItems = req.Line_items.Select(x => new CustomLineItemData(x.Description, x.Quantity, (decimal)x.Unit_price)).ToList();
            
            var command = new CreateCustomCheckoutCommand(
                ctx.TenantId,
                req.Client_email,
                req.Client_name,
                lineItems,
                req.Expires_at?.UtcDateTime,
                req.Is_b2b_required
            );

            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        adminGroup.MapPost("/checkouts/{id:guid}/mark-paid", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new MarkCheckoutAsPaidOfflineCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "completed" });
        });

        adminGroup.MapGet("/custom-checkouts", async Task<Ok<PaginatedResponse<CustomCheckoutDto>>> (
            [FromQuery] int page,
            [FromQuery] int limit,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            var response = await queryService.GetCustomCheckoutsAsync(ctx.TenantId, p, l);
            return TypedResults.Ok(response);
        });

        publicGroup.MapPublicCommerceEndpoints();

        return endpoints;
    }
}
