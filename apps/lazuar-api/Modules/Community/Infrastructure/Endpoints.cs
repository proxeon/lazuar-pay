using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
using Modules.Payments.Application.Queries;
using Modules.Payments.Application.Commands;
using Modules.One.Contracts;
using Modules.Messaging.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/community").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/community");

        admin.MapGet("/plans", async Task<Ok<ICollection<CommunityPlanDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var plans = await queryService.GetAdminPlansAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CommunityPlanDto>)plans.ToList());
        });

        admin.MapGet("/plans/{id:guid}", async Task<Results<Ok<CommunityPlanDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var plan = await queryService.GetAdminPlanByIdAsync(ctx.TenantId, id);
            return plan != null ? TypedResults.Ok(plan) : TypedResults.NotFound();
        });

        admin.MapGet("/subscribers", async Task<Ok<PaginatedResponse<CommunitySubscriptionDto>>> (
            [FromQuery] int page,
            [FromQuery] int limit,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            var response = await queryService.GetSubscribersAsync(ctx.TenantId, p, l);
            return TypedResults.Ok(response);
        });

        admin.MapGet("/subscribers/export", async (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var query = new GetSubscribersExportQuery(ctx.TenantId);
            var fileBytes = await mediator.Send(query);
            var filename = $"Subscribers_Export_{DateTime.UtcNow:yyyyMMdd}.csv";
            return Results.File(fileBytes, "text/csv", filename);
        });

        admin.MapGet("/subscribers/{id:guid}/reminders", async Task<Ok<ICollection<DeliveryHistoryItemDto>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var history = await queryService.GetReminderHistoryAsync(ctx.TenantId, id);
            return TypedResults.Ok((ICollection<DeliveryHistoryItemDto>)history.ToList());
        });

        admin.MapGet("/subscribers/{id:guid}/payments", async Task<Ok<PaginatedResponse<PaymentRecordDto>>> (
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

        admin.MapGet("/stats", async Task<Ok<CommunitySubscriberStatsDto>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var stats = await queryService.GetSubscriberStatsAsync(ctx.TenantId);
            return TypedResults.Ok(stats);
        });

        admin.MapGet("/reminder-schedules", async Task<Ok<ICollection<CommunityReminderScheduleDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var schedules = await queryService.GetReminderSchedulesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CommunityReminderScheduleDto>)schedules.ToList());
        });

        admin.MapGet("/payment-config", async Task<Results<Ok<PaymentConfigDto>, NotFound>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var query = new GetPaymentConfigQuery(ctx.TenantId);
            var config = await mediator.Send(query);
            return config != null ? TypedResults.Ok(config) : TypedResults.NotFound();
        });

        publicGroup.MapGet("/{tenantSlug}/plans", async Task<Results<Ok<ICollection<CommunityPlanDto>>, NotFound>> (
            string tenantSlug,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            return TypedResults.Ok((ICollection<CommunityPlanDto>)plans.ToList());
        });

        publicGroup.MapGet("/{tenantSlug}/plans/{slug}", async Task<Results<Ok<CommunityPlanDto>, NotFound>> (
            string tenantSlug,
            string slug,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();
            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            var plan = plans.FirstOrDefault(p => p.Slug == slug);
            return plan != null ? TypedResults.Ok(plan) : TypedResults.NotFound();
        });

        admin.MapPost("/plans", async Task<Ok<IdResponse>> (CreatePlanRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CreatePlanCommand(
                ctx.TenantId, req.Slug, req.Name, req.Audience, req.Short_description,
                req.Long_description, (decimal)req.Price, req.Interval, req.Grace_period_days,
                req.Max_capacity, req.Display_order, req.Features?.ToList() ?? new List<string>(), req.Methodology,
                req.Faq?.Select(f => new Modules.Community.Application.Commands.FaqItemDto(f.Id, f.Question, f.Answer)).ToList() ?? new List<Modules.Community.Application.Commands.FaqItemDto>(),
                req.Telegram_invite_link, req.Weekly_meeting_link);
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        admin.MapPut("/plans/{id:guid}", async Task<Ok<StatusResponse>> (Guid id, UpdatePlanRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new UpdatePlanCommand(
                ctx.TenantId, id, req.Slug, req.Name, req.Audience, req.Short_description,
                req.Long_description, req.Price.HasValue ? (decimal)req.Price.Value : null, req.Interval, req.Features?.ToList(), req.Methodology,
                req.Faq?.Select(f => new Modules.Community.Application.Commands.FaqItemDto(f.Id, f.Question, f.Answer)).ToList(),
                req.Is_active, req.Display_order, req.Max_capacity, req.Grace_period_days,
                req.Telegram_invite_link, req.Weekly_meeting_link);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        admin.MapDelete("/plans/{id:guid}", async Task<Ok<StatusResponse>> (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ArchivePlanCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "archived" });
        });

        admin.MapPost("/subscribers", async Task<Ok<IdResponse>> (CreateSubscriberRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CreateSubscriberCommand(
                ctx.TenantId, req.Name, req.Email, req.Phone, Guid.Parse(req.Plan_id),
                req.Source ?? "MANUAL_ENTRY", req.Is_reminder_only ?? false, req.Preferred_channel,
                req.Amount_paid.HasValue ? (decimal)req.Amount_paid.Value : null, req.Payment_method, req.Reference_number, req.Notes, "ADMIN");
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        admin.MapPost("/subscribers/{id:guid}/payments", async Task<Ok<StatusResponse>> (Guid id, RecordPaymentRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new RecordSubscriptionPaymentCommand(
                ctx.TenantId, id, (decimal)req.Amount, "MYR", req.Payment_method,
                req.Reference_number, "ADMIN", req.Receipt_file);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "paid" });
        });

        admin.MapPost("/subscribers/{id:guid}/record-payment", async Task<Ok<StatusResponse>> (Guid id, RecordPaymentRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new RecordSubscriptionPaymentCommand(
                ctx.TenantId, id, (decimal)req.Amount, "MYR", req.Payment_method,
                req.Reference_number, "ADMIN", req.Receipt_file);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "paid" });
        });

        admin.MapPost("/subscribers/{id:guid}/cancel", async Task<Ok<StatusResponse>> (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CancelSubscriptionCommand(ctx.TenantId, id);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "cancelled" });
        });

        admin.MapPost("/subscribers/{id:guid}/ban", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new BanSubscriberCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "banned" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapPost("/subscribers/{id:guid}/refund", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, RefundRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RequestRefundCommand(ctx.TenantId, id, Guid.Parse(req.Payment_record_id), req.Reason));
                return TypedResults.Ok(new StatusResponse { Status = "refund_requested" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapPost("/subscribers/{id:guid}/change-plan", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, ChangePlanRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ChangePlanCommand(ctx.TenantId, id, Guid.Parse(req.New_plan_id)));
                return TypedResults.Ok(new StatusResponse { Status = "plan_change_scheduled" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapPost("/subscribers/{id:guid}/resend-onboarding", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ResendOnboardingCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "onboarding_resent" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        admin.MapPut("/subscribers/{id:guid}", async Task<Ok<StatusResponse>> (Guid id, UpdateSubscriberProfileRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new UpdateSubscriberProfileCommand(
                ctx.TenantId, id, req.Is_reminder_only, req.Preferred_channel, req.Admin_notes, req.Next_renewal_date?.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        admin.MapPost("/subscribers/{id:guid}/extend-grace", async Task<Ok<StatusResponse>> (Guid id, ExtendGraceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ExtendGracePeriodCommand(ctx.TenantId, id, req.Days));
            return TypedResults.Ok(new StatusResponse { Status = "extended" });
        });

        admin.MapPost("/subscribers/{id:guid}/pause-reminders", async Task<Ok<StatusResponse>> (Guid id, PauseRemindersRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new PauseRemindersCommand(ctx.TenantId, id, req.Pause_until?.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "paused" });
        });

        admin.MapPost("/subscribers/{id:guid}/send-reminder", async Task<Ok<StatusResponse>> (Guid id, SendOneOffReminderRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            await mediator.Send(new SendOneOffReminderCommand(
                ctx.TenantId, id, templateId, req.Custom_message, req.Channel ?? "EMAIL"));
            return TypedResults.Ok(new StatusResponse { Status = "sent" });
        });

        admin.MapPost("/reminders/schedule-one-off", async Task<Ok<StatusResponse>> (ScheduleOneOffRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            await mediator.Send(new ScheduleOneOffReminderCommand(
                ctx.TenantId, Guid.Parse(req.Subscriber_id), templateId, req.Custom_message, req.Channel ?? "DEFAULT", req.Scheduled_at.UtcDateTime));
            return TypedResults.Ok(new StatusResponse { Status = "scheduled" });
        });

        admin.MapPost("/reminder-schedules", async Task<Ok<IdResponse>> (CreateReminderScheduleRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            Guid? planId = !string.IsNullOrEmpty(req.Plan_id) ? Guid.Parse(req.Plan_id) : null;
            var command = new CreateReminderScheduleCommand(
                ctx.TenantId, planId, Guid.Parse(req.Template_id), req.Channel,
                req.Days_relative_to_due, req.Time_of_day, req.Is_enabled);
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        admin.MapPut("/reminder-schedules/{id:guid}", async Task<Ok<StatusResponse>> (Guid id, UpdateReminderScheduleRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            Guid? planId = !string.IsNullOrEmpty(req.Plan_id) ? Guid.Parse(req.Plan_id) : null;
            Guid? templateId = !string.IsNullOrEmpty(req.Template_id) ? Guid.Parse(req.Template_id) : null;
            await mediator.Send(new UpdateReminderScheduleCommand(
                ctx.TenantId, id, planId, templateId, req.Channel,
                req.Days_relative_to_due, req.Time_of_day, req.Is_enabled));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        admin.MapDelete("/reminder-schedules/{id:guid}", async Task<Ok<StatusResponse>> (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new DeleteReminderScheduleCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        admin.MapPut("/payment-config", async Task<Ok<StatusResponse>> (SavePaymentConfigRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new UpdatePaymentConfigCommand(
                ctx.TenantId, req.Gateway_type, req.Api_key, req.Collection_id, req.Webhook_secret, req.Secret_key, req.Is_active);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "saved" });
        });

        admin.MapGet("/templates", async Task<Ok<ICollection<MessageTemplateDto>>> (
            IExecutionContextAccessor ctx,
            IMessageTemplateQueryService templateService) =>
        {
            var templates = await templateService.GetAllTemplatesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<MessageTemplateDto>)templates.ToList());
        });

        admin.MapPut("/templates/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateTemplateRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new UpdateMessageTemplateCommand(ctx.TenantId, id, req.Subject, req.Body));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        admin.MapDelete("/templates/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new ResetMessageTemplateCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "reset" });
        });

        admin.MapPost("/reminders/test", async Task<Ok<TestReminderResponse>> (
            TestReminderRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new SendTestReminderCommand(ctx.TenantId, req.Template_name, req.Channel));
            return TypedResults.Ok(new TestReminderResponse { Success = true, Sent_to = "admin@lazuars.io" });
        });

        admin.MapGet("/coupons", async Task<Ok<ICollection<CouponDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var coupons = await queryService.GetCouponsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<CouponDto>)coupons.ToList());
        });

        admin.MapPost("/coupons", async Task<Ok<IdResponse>> (CreateCouponRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CreateCouponCommand(
                ctx.TenantId, 
                req.Code, 
                req.Discount_type, 
                (decimal)req.Amount, 
                req.Max_uses, 
                req.Expires_at?.UtcDateTime);
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        publicGroup.MapPost("/checkout", async Task<Results<Ok<CheckoutResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>, NotFound>> (
            PublicCheckoutRequestDto req,
            IOneQueryService oneQueryService,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(req.Tenant_slug);
            if (tenant == null || !tenant.IsActive)
                return TypedResults.NotFound();

            var globalUserId = (ctx.UserId != Guid.Empty && req.Is_guest_checkout != true)
                ? ctx.UserId
                : (Guid?)null;

            try
            {
                var command = new RegisterPublicSubscriberCommand(
                    tenant.Id,
                    req.Tenant_slug,
                    req.Plan_slug,
                    req.Name,
                    req.Email,
                    req.Phone,
                    globalUserId);

                var checkoutUrl = await mediator.Send(command);
                return TypedResults.Ok(new CheckoutResponse { Url = checkoutUrl });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 400,
                    Title = "Checkout Generation Failed",
                    Detail = ex.Message
                });
            }
        });

        publicGroup.MapPost("/{tenantSlug}/portal/magic-link", async Task<Results<Ok<StatusResponse>, NotFound>> (
            string tenantSlug,
            [FromBody] MagicLinkRequestDto req,
            HttpRequest httpReq,
            IOneQueryService oneQueryService,
            IMediator mediator) =>
        {
            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();

            var baseUrl = $"{httpReq.Scheme}://{httpReq.Host}";
            var command = new RequestMagicLinkCommand(tenant.Id, tenantSlug, req.Email, baseUrl);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "sent" });
        });

        publicGroup.MapGet("/{tenantSlug}/portal", async Task<Results<Ok<PortalDataResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();

            var query = new GetPortalSubscriptionQuery(tenant.Id, subId.Value);
            var sub = await mediator.Send(query);

            if (sub == null) return TypedResults.Unauthorized();
            return TypedResults.Ok(new PortalDataResponse { Subscription = sub });
        });

        publicGroup.MapPost("/{tenantSlug}/portal/cancel", async Task<Results<Ok<StatusResponse>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            [FromBody] CancelPortalRequest req,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();

            if (Guid.Parse(req.Subscription_id) != subId.Value) return TypedResults.Unauthorized();

            var command = new CancelSubscriptionCommand(tenant.Id, subId.Value);
            await mediator.Send(command);
            return TypedResults.Ok(new StatusResponse { Status = "cancelled" });
        });

        publicGroup.MapGet("/{tenantSlug}/portal/billing-link", async Task<Results<Ok<BillingLinkResponseDto>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>, NotFound, UnauthorizedHttpResult>> (
            string tenantSlug,
            [FromQuery] string token,
            HttpRequest httpReq,
            IOneQueryService oneQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return TypedResults.Unauthorized();

            var tenant = await oneQueryService.GetWorkspaceBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return TypedResults.NotFound();

            try
            {
                var baseUrl = $"{httpReq.Scheme}://{httpReq.Host}/{tenantSlug}/portal?token={token}";
                var query = new GetPortalBillingLinkQuery(tenant.Id, subId.Value, baseUrl);
                var url = await mediator.Send(query);
                return TypedResults.Ok(new BillingLinkResponseDto { Url = url });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        return endpoints;
    }
}
