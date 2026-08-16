using System;
using System.Globalization;
using System.Linq;
using System.Text;
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

        group.MapGet("/subscribers/export", async (
            [FromQuery] string? search,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
          {
              // Cap export size; matches ops CSV bulk download use-case.
              var response = await queryService.GetSubscribersAsync(ctx.TenantId, page: 1, limit: 10_000, search);
              var csv = BuildSubscribersCsv(response.Data);
              var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
              return Results.File(bytes, "text/csv", $"subscribers_export_{DateTime.UtcNow:yyyyMMdd}.csv");
          });

        group.MapPost("/subscribers", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            CreateManualSubscriberDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
          {
              try
              {
                  if (!Guid.TryParse(req.Product_id, out var productId))
                  {
                      return TypedResults.BadRequest(new StatusResponse { Status = "Invalid product_id." });
                  }

                  var command = new CreateManualSubscriberCommand(
                      ctx.TenantId,
                      req.Name,
                      req.Email,
                      req.Phone,
                      productId,
                      req.Payment_method,
                      (decimal)req.Amount_paid,
                      req.Reference_number,
                      req.Send_welcome_email ?? true,
                      req.Start_date?.UtcDateTime,
                      req.Next_billing_date?.UtcDateTime
                  );

                  await mediator.Send(command);
                  return TypedResults.Ok(new StatusResponse { Status = "enrolled" });
              }
              catch (InvalidOperationException ex)
              {
                  return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
              }
              catch (FormatException ex)
              {
                  return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
              }
          }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/portal-link", async (
            GenerateCustomerPortalRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
          {
              var query = new GenerateCustomerPortalQuery(ctx.TenantId, req.Customer_email, req.Return_url);
              var url = await mediator.Send(query);
              return TypedResults.Ok(new GenerateCustomerPortalResponseDto { Url = url });
          }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/cancel", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            Guid id,
            CancelSubscriberRequest? body,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                var status = await mediator.Send(new CancelAdminSubscriptionCommand(
                    ctx.TenantId,
                    id,
                    body?.At_period_end ?? false));
                return TypedResults.Ok(new StatusResponse { Status = status });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/keep", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new KeepAdminSubscriptionCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "kept" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/record-payment", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            Guid id,
            RecordPaymentRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RecordSubscriberPaymentCommand(
                    ctx.TenantId,
                    id,
                    (decimal)req.Amount,
                    req.Payment_method,
                    req.Reference_number,
                    req.Next_billing_date?.UtcDateTime));
                return TypedResults.Ok(new StatusResponse { Status = "payment_recorded" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/change-plan", async Task<Results<Ok<PlanChangePreviewDto>, BadRequest<StatusResponse>>> (
            Guid id,
            ChangePlanRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                Guid? productId = null;
                if (!string.IsNullOrWhiteSpace(req.Product_id))
                {
                    if (!Guid.TryParse(req.Product_id, out var parsed))
                    {
                        return TypedResults.BadRequest(new StatusResponse { Status = "Invalid product_id." });
                    }

                    productId = parsed;
                }

                var preview = await mediator.Send(new ChangePlanCommand(
                    ctx.TenantId,
                    id,
                    productId,
                    req.Prorate,
                    req.Apply));
                return TypedResults.Ok(MapPreview(preview));
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/quantity", async Task<Results<Ok<PlanChangePreviewDto>, BadRequest<StatusResponse>>> (
            Guid id,
            SetQuantityRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                var preview = await mediator.Send(new SetSubscriptionQuantityCommand(
                    ctx.TenantId,
                    id,
                    req.Quantity,
                    req.Prorate,
                    req.Apply));
                return TypedResults.Ok(MapPreview(preview));
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/collection/pause", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            Guid id,
            PauseCollectionRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new PauseCollectionCommand(ctx.TenantId, id, req.Resume_on.UtcDateTime));
                return TypedResults.Ok(new StatusResponse { Status = "paused" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/collection/resume", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ResumeCollectionCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "resumed" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/dunning/pause", async Task<Ok<StatusResponse>> (
            Guid id,
            PauseDunningRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new PauseSubscriberDunningCommand(ctx.TenantId, id, req.Pause_until.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "paused" });
        }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/dunning/resume", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ResumeSubscriberDunningCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "resumed" });
        }).RequireAuthorization("OrgMember");

        group.MapPost("/subscribers/{id:guid}/anonymize", async Task<Results<Ok<StatusResponse>, NotFound<StatusResponse>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new AnonymizeSubscriberCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "anonymized" });
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.NotFound(new StatusResponse { Status = ex.Message });
            }
        }).RequireAuthorization("OrgAdmin");

        return group;
    }

    private static string BuildSubscribersCsv(System.Collections.Generic.IEnumerable<CommerceSubscriptionDto> subscribers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,customer_name,customer_email,customer_phone,product_name,product_price,status,current_period_end,next_billing_date,created_at");

        foreach (var s in subscribers)
        {
            static string Esc(string? v)
            {
                if (string.IsNullOrEmpty(v)) return "";
                if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
                    return $"\"{v.Replace("\"", "\"\"")}\"";
                return v;
            }

            sb.Append(Esc(s.Id)).Append(',')
              .Append(Esc(s.Customer_name)).Append(',')
              .Append(Esc(s.Customer_email)).Append(',')
              .Append(Esc(s.Customer_phone)).Append(',')
              .Append(Esc(s.Product_name)).Append(',')
              .Append(s.Product_price.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Esc(s.Status)).Append(',')
              .Append(s.Current_period_end?.UtcDateTime.ToString("o", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(s.Next_billing_date?.UtcDateTime.ToString("o", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(s.Created_at.UtcDateTime.ToString("o", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    internal static PlanChangePreviewDto MapPreview(Modules.Commerce.Contracts.Commands.PlanChangePreview preview) => new()
    {
        Current_product_id = preview.CurrentProductId.ToString(),
        Current_amount = (double)preview.CurrentAmount,
        Currency = preview.Currency,
        Interval = preview.Interval,
        Next_product_id = preview.NextProductId.ToString(),
        Next_amount = (double)preview.NextAmount,
        Effective_at = preview.EffectiveAt.HasValue ? new DateTimeOffset(preview.EffectiveAt.Value) : null,
        Amount_due_now = (double)preview.AmountDueNow,
        Policy = preview.Policy
    };
}
