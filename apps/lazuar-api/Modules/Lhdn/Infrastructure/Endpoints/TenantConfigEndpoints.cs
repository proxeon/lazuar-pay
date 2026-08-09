using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Queries;

namespace Modules.Lhdn.Infrastructure;

public static class TenantConfigEndpoints
{
    public static IEndpointRouteBuilder MapLhdnTenantConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        admin.MapGet("/workspaces/{id:guid}/lhdn-config", async Task<Results<Ok<LhdnTenantConfigDto>, NotFound, UnauthorizedHttpResult>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            var result = await mediator.Send(new GetLhdnTenantConfigQuery(id));
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        admin.MapPut("/workspaces/{id:guid}/lhdn-config", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>, UnauthorizedHttpResult>> (
            Guid id,
            [FromBody] UpdateLhdnTenantConfigRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            try
            {
                await mediator.Send(new UpdateLhdnTenantConfigCommand(id, req));
                return TypedResults.Ok(new StatusResponse { Status = "updated" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapPut("/workspaces/{id:guid}/lhdn-certificate", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
            Guid id,
            [FromBody] UpdateLhdnCertificateRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            await mediator.Send(new UpdateLhdnCertificateCommand(id, req.P12_base64_file, req.Passphrase));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        return endpoints;
    }
}
