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
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;
using Modules.Payments.Contracts.Queries;

namespace Modules.Commerce.Infrastructure;

public record GenerateCustomerPortalRequest(string Customer_email, string Return_url);
public record GenerateCustomerPortalResponse(string Url);

public record CreateManualSubscriberRequest(
    string Name,
    string Email,
    string Phone,
    string Product_id,
    string Payment_method,
    decimal Amount_paid,
    string? Reference_number,
    bool? Send_welcome_email,
    DateTimeOffset? Start_date,
    DateTimeOffset? Next_billing_date
);

public static class SubscriberEndpoints
{
    public static RouteGroupBuilder MapSubscriberEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscribers", async (
            [FromQuery] int? page,
            [FromQuery] int? limit,
            [FromQuery] string? search,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
          {
              var p = page ?? 1;
              var l = limit ?? 50;
              var response = await queryService.GetSubscribersAsync(ctx.TenantId, p, l, search);
              return TypedResults.Ok(response);
          });

        group.MapPost("/subscribers", async (
            CreateManualSubscriberRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
          {
              var command = new CreateManualSubscriberCommand(
                  ctx.TenantId,
                  req.Name,
                  req.Email,
                  req.Phone,
                  Guid.Parse(req.Product_id),
                  req.Payment_method,
                  req.Amount_paid,
                  req.Reference_number,
                  req.Send_welcome_email ?? true,
                  req.Start_date?.UtcDateTime,
                  req.Next_billing_date?.UtcDateTime
              );

              await mediator.Send(command);
              return TypedResults.Ok(new StatusResponse { Status = "enrolled" });
          });

        group.MapPost("/subscribers/portal-link", async (
            GenerateCustomerPortalRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
          {
              var query = new GenerateCustomerPortalQuery(ctx.TenantId, req.Customer_email, req.Return_url);
              var url = await mediator.Send(query);
              return TypedResults.Ok(new GenerateCustomerPortalResponse(url));
          });

        group.MapPost("/subscribers/{id:guid}/dunning/pause", async Task<Ok<StatusResponse>> (
            Guid id,
            PauseDunningRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new PauseSubscriberDunningCommand(ctx.TenantId, id, req.Pause_until.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "paused" });
        });

        group.MapPost("/subscribers/{id:guid}/dunning/resume", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ResumeSubscriberDunningCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "resumed" });
        });

        return group;
    }
}
