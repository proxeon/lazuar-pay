using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.One.Application.Commands;

namespace Modules.One.Infrastructure;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/me/profile", async Task<Ok<StatusResponse>> (UpdateProfileRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new UpdateProfileCommand(ctx.UserId, req.Name));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization();

        group.MapPut("/me/security/password", async Task<Ok<StatusResponse>> (ChangePasswordRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ChangePasswordCommand(ctx.UserId, req.Current_password, req.New_password));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        }).RequireAuthorization();

        return group;
    }
}
