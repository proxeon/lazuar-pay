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

using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Modules.Community.Infrastructure;

public static class AuthenticatedCommunityEndpoints
{
    public static RouteGroupBuilder MapAuthenticatedCommunityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/subscriptions", async Task<Results<Ok<ICollection<MyGlobalSubscriptionDto>>, UnauthorizedHttpResult>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
            
            var subscriptions = await queryService.GetMyGlobalSubscriptionsAsync(ctx.UserId);
            return TypedResults.Ok((ICollection<MyGlobalSubscriptionDto>)subscriptions.ToList());
        });

        group.MapPost("/me/subscriptions/{id:guid}/portal-link", async Task<Results<Ok<GeneratePortalLinkResponseDto>, UnauthorizedHttpResult, BadRequest<ProblemDetails>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();

            try
            {
                var result = await mediator.Send(new GenerateMyPortalLinkCommand(ctx.UserId, id));
                return TypedResults.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        return group;
    }
}
