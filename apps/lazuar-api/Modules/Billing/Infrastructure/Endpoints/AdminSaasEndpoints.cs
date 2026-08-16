using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Billing.Application.Queries;
using Modules.Billing.Contracts.Commands;

namespace Modules.Billing.Infrastructure;

public static class AdminSaasEndpoints
{
    public static RouteGroupBuilder MapAdminSaasEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/saas", async Task<Ok<WorkspaceSaasSubscriptionDto>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var view = await mediator.Send(new GetWorkspaceSaasQuery(ctx.TenantId));
            return TypedResults.Ok(ToDto(view));
        });

        admin.MapPost("/saas/checkout", async Task<Results<Ok<CreateSaasCheckoutResponseDto>, BadRequest<string>>> (
            CreateSaasCheckoutRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                var checkoutUrl = await mediator.Send(
                    new CreateSaasCheckoutCommand(ctx.TenantId, req.Return_url));
                return TypedResults.Ok(new CreateSaasCheckoutResponseDto { Checkout_url = checkoutUrl });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return admin;
    }

    private static WorkspaceSaasSubscriptionDto ToDto(WorkspaceSaasView view) =>
        new()
        {
            Organization_id = view.OrganizationId.ToString(),
            Status = view.Status,
            Current_period_start = view.CurrentPeriodStart.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(view.CurrentPeriodStart.Value, DateTimeKind.Utc))
                : null,
            Current_period_end = view.CurrentPeriodEnd.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(view.CurrentPeriodEnd.Value, DateTimeKind.Utc))
                : null,
            Next_invoice_at = view.NextInvoiceAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(view.NextInvoiceAt.Value, DateTimeKind.Utc))
                : null,
            Plan = new SaasPlanDto
            {
                Code = view.Plan.Code,
                Name = view.Plan.Name,
                Amount_myr = (double)view.Plan.AmountMyr,
                Interval = view.Plan.Interval,
                Currency = view.Plan.Currency
            }
        };
}
