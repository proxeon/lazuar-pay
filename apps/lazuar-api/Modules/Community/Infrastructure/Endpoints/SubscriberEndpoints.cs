using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;

// Explicitly alias ASP.NET Core's ProblemDetails to resolve collisions with TypeSpec-generated DTOs.
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Modules.Community.Infrastructure;

public static class SubscriberEndpoints
{
    public static RouteGroupBuilder MapSubscriberEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscribers", async Task<Ok<PaginatedResponse<CommunitySubscriptionDto>>> (
            [FromQuery] int page,
            [FromQuery] int limit,
            [FromQuery] string? search,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            var response = await queryService.GetSubscribersAsync(ctx.TenantId, p, l, search);
            return TypedResults.Ok(response);
        });

        group.MapGet("/subscribers/export", async (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var fileBytes = await mediator.Send(new GetSubscribersExportQuery(ctx.TenantId));
            var filename = $"Subscribers_Export_{DateTime.UtcNow:yyyyMMdd}.csv";
            return Results.File(fileBytes, "text/csv", filename);
        });

        group.MapGet("/subscribers/{id:guid}/reminders", async Task<Ok<ICollection<DeliveryHistoryItemDto>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var history = await queryService.GetReminderHistoryAsync(ctx.TenantId, id);
            return TypedResults.Ok((ICollection<DeliveryHistoryItemDto>)history.ToList());
        });

        group.MapGet("/subscribers/{id:guid}/payments", async Task<Ok<PaginatedResponse<PaymentRecordDto>>> (
            Guid id,
            [FromQuery] int page,
            [FromQuery] int limit,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            var response = await queryService.GetPaymentHistoryAsync(ctx.TenantId, id, p, l);
            return TypedResults.Ok(response);
        });

        group.MapPost("/subscribers", async Task<Ok<IdResponse>> (
            CreateSubscriberRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new CreateSubscriberCommand(
                ctx.TenantId, req.Name, req.Email, req.Phone, Guid.Parse(req.Plan_id),
                req.Source ?? "MANUAL_ENTRY", req.Is_reminder_only ?? false, req.Preferred_channel,
                req.Amount_paid.HasValue ? (decimal)req.Amount_paid.Value : null, req.Payment_method, req.Reference_number, req.Notes, "ADMIN");
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapPost("/subscribers/{id:guid}/record-payment", async Task<Ok<StatusResponse>> (
            Guid id, 
            RecordPaymentRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new RecordSubscriptionPaymentCommand(
                ctx.TenantId, id, (decimal)req.Amount, "MYR", req.Payment_method,
                req.Reference_number, "ADMIN", req.Receipt_file);
            
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "paid" });
        });

        group.MapPost("/subscribers/{id:guid}/cancel", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new CancelSubscriptionCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "cancelled" });
        });

        group.MapPost("/subscribers/{id:guid}/ban", async Task<Results<Ok<StatusResponse>, BadRequest<ProblemDetails>>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new BanSubscriberCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "banned" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/refund", async Task<Results<Ok<StatusResponse>, BadRequest<ProblemDetails>>> (
            Guid id, 
            RefundRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RequestRefundCommand(ctx.TenantId, id, Guid.Parse(req.Payment_record_id), req.Reason));
                return TypedResults.Ok(new StatusResponse { Status = "refund_requested" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/change-plan", async Task<Results<Ok<StatusResponse>, BadRequest<ProblemDetails>>> (
            Guid id, 
            ChangePlanRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ChangePlanCommand(ctx.TenantId, id, Guid.Parse(req.New_plan_id)));
                return TypedResults.Ok(new StatusResponse { Status = "plan_change_scheduled" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        group.MapPost("/subscribers/{id:guid}/resend-onboarding", async Task<Results<Ok<StatusResponse>, BadRequest<ProblemDetails>>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ResendOnboardingCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "onboarding_resent" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        group.MapPut("/subscribers/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            UpdateSubscriberProfileRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new UpdateSubscriberProfileCommand(
                ctx.TenantId, id, req.Is_reminder_only, req.Preferred_channel, req.Admin_notes, req.Next_renewal_date?.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapPost("/subscribers/{id:guid}/extend-grace", async Task<Ok<StatusResponse>> (
            Guid id, 
            ExtendGraceRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new ExtendGracePeriodCommand(ctx.TenantId, id, req.Days));
            return TypedResults.Ok(new StatusResponse { Status = "extended" });
        });

        group.MapPost("/subscribers/{id:guid}/pause-reminders", async Task<Ok<StatusResponse>> (
            Guid id, 
            PauseRemindersRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new PauseRemindersCommand(ctx.TenantId, id, req.Pause_until?.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "paused" });
        });

        group.MapPost("/subscribers/{id:guid}/send-reminder", async Task<Ok<StatusResponse>> (
            Guid id, 
            SendOneOffReminderRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            await mediator.Send(new SendOneOffReminderCommand(
                ctx.TenantId, id, templateId, req.Custom_message, req.Channel ?? "EMAIL"));
            
            return TypedResults.Ok(new StatusResponse { Status = "sent" });
        });

        return group;
    }
}
