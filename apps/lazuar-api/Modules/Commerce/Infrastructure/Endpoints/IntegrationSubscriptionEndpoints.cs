using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Infrastructure;

public static class IntegrationSubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapCommerceIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/integrations/commerce").RequireCors();

        group.MapGet("/subscriptions", async (
            [FromQuery] int? page,
            [FromQuery] int? limit,
            [FromQuery] string? status,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            if (ctx.TenantId == Guid.Empty)
            {
                return Results.Json(new ProblemDetails { Status = 401, Title = "UNAUTHORIZED", Detail = "Missing or invalid authentication." }, statusCode: 401);
            }

            var p = page is null or < 1 ? 1 : page.Value;
            var l = limit is null or < 1 or > 100 ? 50 : limit.Value;
            var response = await queryService.GetSubscribersAsync(ctx.TenantId, p, l, searchTerm: null, status);
            return Results.Ok(response);
        }).RequireAuthorization("IntegrationCommerceSubscriptionsRead");

        group.MapGet("/subscriptions/{id:guid}", async (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            if (ctx.TenantId == Guid.Empty)
            {
                return Results.Json(new ProblemDetails { Status = 401, Title = "UNAUTHORIZED", Detail = "Missing or invalid authentication." }, statusCode: 401);
            }

            var dto = await queryService.GetSubscriberByIdAsync(ctx.TenantId, id);
            return dto == null
                ? Results.Json(new ProblemDetails { Status = 404, Title = "NOT_FOUND", Detail = "Subscription not found." }, statusCode: 404)
                : Results.Ok(dto);
        }).RequireAuthorization("IntegrationCommerceSubscriptionsRead");

        group.MapPost("/subscriptions/{id:guid}/cancel", async (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService,
            IMediator mediator) =>
        {
            if (ctx.TenantId == Guid.Empty)
            {
                return Results.Json(new ProblemDetails { Status = 401, Title = "UNAUTHORIZED", Detail = "Missing or invalid authentication." }, statusCode: 401);
            }

            var existing = await queryService.GetSubscriberByIdAsync(ctx.TenantId, id);
            if (existing == null)
            {
                return Results.Json(new ProblemDetails { Status = 404, Title = "NOT_FOUND", Detail = "Subscription not found." }, statusCode: 404);
            }

            if (string.Equals(existing.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new ProblemDetails { Status = 400, Title = "ALREADY_CANCELED", Detail = "Subscription is already canceled." }, statusCode: 400);
            }

            try
            {
                var status = await mediator.Send(new CancelAdminSubscriptionCommand(ctx.TenantId, id, AtPeriodEnd: false));
                return Results.Ok(new StatusResponse { Status = status });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ProblemDetails { Status = 400, Title = "CANCEL_REJECTED", Detail = ex.Message }, statusCode: 400);
            }
        }).RequireAuthorization("IntegrationCommerceSubscriptionsWrite");

        return endpoints;
    }
}
