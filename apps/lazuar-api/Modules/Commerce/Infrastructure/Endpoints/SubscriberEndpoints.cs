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

public static class SubscriberEndpoints
{
    public static RouteGroupBuilder MapSubscriberEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscribers", async Task<Ok<PaginatedResponse<CommerceSubscriptionDto>>> (
            [FromQuery] int page,
            [FromQuery] int limit,
            [FromQuery] string? search,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
          {
              var p = page < 1 ? 1 : page;
              var l = limit < 1 || limit > 100 ? 50 : limit;
              var response = await queryService.GetSubscribersAsync(ctx.TenantId, p, l, search);
              return TypedResults.Ok(response);
          });

        group.MapPost("/subscribers", async Task<Ok<StatusResponse>> (
            CreateManualSubscriberDto req,
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
                  (decimal)req.Amount_paid,
                  req.Reference_number,
                  req.Send_welcome_email ?? true,
                  req.Start_date?.UtcDateTime,
                  req.Next_billing_date?.UtcDateTime
              );

              await mediator.Send(command);
              return TypedResults.Ok(new StatusResponse { Status = "enrolled" });
          });

        group.MapPost("/subscribers/portal-link", async Task<Ok<GenerateCustomerPortalResponse>> (
            GenerateCustomerPortalRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
          {
              var query = new GenerateCustomerPortalQuery(ctx.TenantId, req.Customer_email, req.Return_url);
              var url = await mediator.Send(query);
              return TypedResults.Ok(new GenerateCustomerPortalResponse(url));
          });

        return group;
    }
}
