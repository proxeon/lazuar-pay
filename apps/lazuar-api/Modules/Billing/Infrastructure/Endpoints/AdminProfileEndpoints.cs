using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;

namespace Modules.Billing.Infrastructure;

public static class AdminProfileEndpoints
{
    public static RouteGroupBuilder MapAdminProfileEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/profile", async Task<Results<Ok<TenantBillingProfileDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var profile = await queryService.GetBillingProfileAsync(ctx.TenantId);
            return profile != null ? TypedResults.Ok(profile) : TypedResults.NotFound();
        });

        admin.MapPut("/profile", async Task<Ok<StatusResponse>> (
            UpdateTenantBillingProfileRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new UpdateTenantBillingProfileCommand(
                ctx.TenantId,
                req.Legal_name,
                req.Tin,
                req.Registration_number,
                req.Sst_registration_number,
                req.Logo_url,
                req.Address
            );

            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        return admin;
    }
}
