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

        // Unset config → 200 empty DTO (not 404) so ops dashboard setup probes stay quiet (plan 663 Phase 9 / issue 011).
        adminGroup.MapGet("/email-config", async Task<Ok<EmailConfigDto>> (
            IExecutionContextAccessor ctx,
            ICommunicationsQueryService queryService) =>
        {
            var config = await queryService.GetEmailConfigAsync(ctx.TenantId);
            return TypedResults.Ok(config ?? new EmailConfigDto
            {
                Has_api_key = false,
                Api_key_hint = null,
                Sender_email = null,
                Is_active = false,
            });
        });

        adminGroup.MapPut("/email-config", async Task<Ok<StatusResponse>> (
            SaveEmailConfigRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            // Empty/null api_key means "keep existing encrypted key" (ops UX).
            var command = new SaveEmailConfigCommand(
                ctx.TenantId,
                string.IsNullOrWhiteSpace(req.Api_key) ? null : req.Api_key,
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
