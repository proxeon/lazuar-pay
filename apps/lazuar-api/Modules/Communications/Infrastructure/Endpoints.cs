using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MediatR;
using Lazuar.ApiTypes;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Communications.Contracts;
using Modules.Communications.Application.Commands;

namespace Modules.Communications.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunicationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/communications").RequireAuthorization("OrgAdmin");

        adminGroup.MapTemplateEndpoints();
        adminGroup.MapBroadcastEndpoints();

        adminGroup.MapGet("/email-config", async Task<Results<Ok<EmailConfigDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            ICommunicationsQueryService queryService) =>
        {
            var config = await queryService.GetEmailConfigAsync(ctx.TenantId);
            return config != null ? TypedResults.Ok(config) : TypedResults.NotFound();
        });

        adminGroup.MapPut("/email-config", async Task<Ok<StatusResponse>> (
            SaveEmailConfigRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new SaveEmailConfigCommand(
                ctx.TenantId,
                req.Api_key,
                req.Sender_email,
                req.Is_active
            );

            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        endpoints.MapPublicComplianceEndpoints();

        return endpoints;
    }
}
