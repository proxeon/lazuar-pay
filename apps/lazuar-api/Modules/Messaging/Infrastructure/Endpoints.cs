using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using MediatR;
using Modules.Messaging.Application;

namespace Modules.Messaging.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/messaging");

        group.MapPost("/notify", async (SendTenantNotificationCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            return Results.Accepted();
        }).RequireAuthorization("OrgAdmin");

        return endpoints;
    }
}
