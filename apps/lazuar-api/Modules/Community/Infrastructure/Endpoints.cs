using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
using Modules.Tenant.Contracts;

namespace Modules.Community.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/community").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/community");

        // ==========================================
        // QUERIES (READ MODELS) 
        // ==========================================

        admin.MapGet("/plans", async (
            IExecutionContextAccessor ctx, 
            ICommunityQueryService queryService) =>
        {
            var plans = await queryService.GetAdminPlansAsync(ctx.TenantId);
            return Results.Ok(plans);
        });

        admin.MapGet("/plans/{id:guid}", async (
            Guid id,
            IExecutionContextAccessor ctx, 
            ICommunityQueryService queryService) =>
        {
            var plan = await queryService.GetAdminPlanByIdAsync(ctx.TenantId, id);
            return plan != null ? Results.Ok(plan) : Results.NotFound();
        });

        admin.MapGet("/subscribers", async (
            IExecutionContextAccessor ctx, 
            ICommunityQueryService queryService) =>
        {
            var subscribers = await queryService.GetSubscribersAsync(ctx.TenantId);
            return Results.Ok(subscribers);
        });

        admin.MapGet("/stats", async (
            IExecutionContextAccessor ctx, 
            ICommunityQueryService queryService) =>
        {
            var stats = await queryService.GetSubscriberStatsAsync(ctx.TenantId);
            return Results.Ok(stats);
        });

        admin.MapGet("/reminder-schedules", async (
            IExecutionContextAccessor ctx, 
            ICommunityQueryService queryService) =>
        {
            var schedules = await queryService.GetReminderSchedulesAsync(ctx.TenantId);
            return Results.Ok(schedules);
        });

        publicGroup.MapGet("/{tenantSlug}/plans", async (
            string tenantSlug, 
            ITenantQueryService tenantQueryService, 
            ICommunityQueryService queryService) =>
        {
            var tenant = await tenantQueryService.GetTenantBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return Results.NotFound(new { error = "Business not found." });

            var plans = await queryService.GetPublicPlansAsync(tenant.Id);
            return Results.Ok(plans);
        });

        // ==========================================
        // COMMANDS (WRITE MODELS)
        // ==========================================

        // --- Plans ---
        admin.MapPost("/plans", async (CreatePlanRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CreatePlanCommand(
                ctx.TenantId, req.Slug, req.Name, req.Audience, req.ShortDescription, 
                req.LongDescription, req.Price, req.Interval, req.GracePeriodDays, 
                req.MaxCapacity, req.DisplayOrder, req.Features, req.Methodology, 
                req.Faq.Select(f => new FaqItemDto(f.Id, f.Question, f.Answer)).ToList(),
                req.TelegramInviteLink, req.WeeklyMeetingLink);

            var id = await mediator.Send(command);
            return Results.Ok(new { id });
        });

        admin.MapPut("/plans/{id:guid}", async (Guid id, UpdatePlanRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new UpdatePlanCommand(
                ctx.TenantId, id, req.Slug, req.Name, req.Audience, req.ShortDescription, 
                req.LongDescription, req.Price, req.Interval, req.Features, req.Methodology, 
                req.Faq?.Select(f => new FaqItemDto(f.Id, f.Question, f.Answer)).ToList(), 
                req.IsActive, req.DisplayOrder, req.MaxCapacity, req.GracePeriodDays, 
                req.TelegramInviteLink, req.WeeklyMeetingLink);

            await mediator.Send(command);
            return Results.Ok(new { status = "updated" });
        });

        admin.MapDelete("/plans/{id:guid}", async (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ArchivePlanCommand(ctx.TenantId, id));
            return Results.Ok(new { status = "archived" });
        });

        // --- Subscribers ---
        admin.MapPost("/subscribers/{id:guid}/payments", async (Guid id, RecordPaymentRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new RecordSubscriptionPaymentCommand(
                ctx.TenantId, id, req.Amount, "MYR", req.PaymentMethod, 
                req.ReferenceNumber, "ADMIN", req.ReceiptFile);

            await mediator.Send(command);
            return Results.Ok(new { status = "paid" });
        });

        admin.MapPost("/subscribers/{id:guid}/cancel", async (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CancelSubscriptionCommand(ctx.TenantId, id);
            await mediator.Send(command);
            return Results.Ok(new { status = "cancelled" });
        });

        admin.MapPut("/subscribers/{id:guid}", async (Guid id, UpdateSubscriberProfileRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new UpdateSubscriberProfileCommand(
                ctx.TenantId, id, req.IsReminderOnly, req.PreferredChannel, req.AdminNotes, req.NextRenewalDate));
            return Results.Ok(new { status = "updated" });
        });

        admin.MapPost("/subscribers/{id:guid}/extend-grace", async (Guid id, ExtendGraceRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new ExtendGracePeriodCommand(ctx.TenantId, id, req.Days));
            return Results.Ok(new { status = "extended" });
        });

        admin.MapPost("/subscribers/{id:guid}/pause-reminders", async (Guid id, PauseRemindersRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new PauseRemindersCommand(ctx.TenantId, id, req.PauseUntil));
            return Results.Ok(new { status = "paused" });
        });

        admin.MapPost("/subscribers/{id:guid}/send-reminder", async (Guid id, SendOneOffReminderRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new SendOneOffReminderCommand(
                ctx.TenantId, id, req.TemplateId, req.CustomMessage, req.Channel ?? "EMAIL"));
            return Results.Ok(new { status = "sent" });
        });

        // --- Reminder Schedules ---
        admin.MapPost("/reminder-schedules", async (CreateReminderScheduleRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            var command = new CreateReminderScheduleCommand(
                ctx.TenantId, req.PlanId, req.TemplateId, req.Channel, 
                req.DaysRelativeToDue, req.TimeOfDay, req.IsEnabled);
            var id = await mediator.Send(command);
            return Results.Ok(new { id });
        });

        admin.MapPut("/reminder-schedules/{id:guid}", async (Guid id, UpdateReminderScheduleRequestDto req, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new UpdateReminderScheduleCommand(
                ctx.TenantId, id, req.PlanId, req.TemplateId, req.Channel, 
                req.DaysRelativeToDue, req.TimeOfDay, req.IsEnabled));
            return Results.Ok(new { status = "updated" });
        });

        admin.MapDelete("/reminder-schedules/{id:guid}", async (Guid id, IExecutionContextAccessor ctx, IMediator mediator) =>
        {
            await mediator.Send(new DeleteReminderScheduleCommand(ctx.TenantId, id));
            return Results.Ok(new { status = "deleted" });
        });

        // --- Public Checkout ---
        publicGroup.MapPost("/checkout/{subscriptionId:guid}", async (
            Guid subscriptionId,
            [FromQuery] string tenant,
            [FromBody] InitiateCheckoutRequestDto req,
            ITenantQueryService tenantQueryService,
            IMediator mediator) =>
        {
            var org = await tenantQueryService.GetTenantBySlugAsync(tenant);
            if (org == null) return Results.NotFound(new { error = "Business not found." });

            var command = new InitiateSubscriptionCheckoutCommand(
                org.Id, subscriptionId, req.SuccessUrl, req.CancelUrl);
            
            var url = await mediator.Send(command);
            return Results.Ok(new { url });
        });

        // ==========================================
        // SUBSCRIBER PORTAL (SELF-SERVICE)
        // ==========================================
        
        publicGroup.MapPost("/{tenantSlug}/portal/magic-link", async (
            string tenantSlug,
            [FromBody] MagicLinkRequestDto req,
            HttpRequest httpReq,
            ITenantQueryService tenantQueryService,
            IMediator mediator) =>
        {
            var tenant = await tenantQueryService.GetTenantBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return Results.NotFound(new { error = "Business not found." });

            var baseUrl = $"{httpReq.Scheme}://{httpReq.Host}";
            
            var command = new RequestMagicLinkCommand(tenant.Id, tenantSlug, req.Email, baseUrl);
            await mediator.Send(command);

            return Results.Ok(new { status = "sent" }); // Always return success for security
        });

        publicGroup.MapGet("/{tenantSlug}/portal", async (
            string tenantSlug,
            [FromQuery] string token,
            ITenantQueryService tenantQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return Results.Unauthorized();

            var tenant = await tenantQueryService.GetTenantBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return Results.NotFound(new { error = "Business not found." });

            var query = new GetPortalSubscriptionQuery(tenant.Id, subId.Value);
            var sub = await mediator.Send(query);

            if (sub == null) return Results.Unauthorized();

            return Results.Ok(new { subscription = sub });
        });

        publicGroup.MapPost("/{tenantSlug}/portal/cancel", async (
            string tenantSlug,
            [FromQuery] string token,
            ITenantQueryService tenantQueryService,
            IMagicLinkTokenService tokenService,
            IMediator mediator) =>
        {
            var subId = tokenService.ValidateToken(token);
            if (!subId.HasValue) return Results.Unauthorized();

            var tenant = await tenantQueryService.GetTenantBySlugAsync(tenantSlug);
            if (tenant == null || !tenant.IsActive) return Results.NotFound();

            var command = new CancelSubscriptionCommand(tenant.Id, subId.Value);
            await mediator.Send(command);

            return Results.Ok(new { status = "cancelled" });
        });

        return endpoints;
    }
}

// ==========================================
// DTOs
// ==========================================

public record InitiateCheckoutRequestDto(string SuccessUrl, string CancelUrl);

public record CreatePlanRequestDto(
    string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
    decimal Price, string Interval, List<string> Features, string Methodology,
    List<FaqRequestDto> Faq, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
    string? TelegramInviteLink, string? WeeklyMeetingLink);

public record UpdatePlanRequestDto(
    string? Slug, string? Name, string? Audience, string? ShortDescription, string? LongDescription, 
    decimal? Price, string? Interval, List<string>? Features, string? Methodology, 
    List<FaqRequestDto>? Faq, bool? IsActive, int? DisplayOrder, int? MaxCapacity, int? GracePeriodDays, 
    string? TelegramInviteLink, string? WeeklyMeetingLink);

public record FaqRequestDto(string Id, string Question, string Answer);

public record RecordPaymentRequestDto(
    decimal Amount, string PaymentMethod, string? ReferenceNumber, string? ReceiptFile);

public record UpdateSubscriberProfileRequestDto(
    bool IsReminderOnly, string? PreferredChannel, string? AdminNotes, DateTime? NextRenewalDate);

public record ExtendGraceRequestDto(int Days);

public record PauseRemindersRequestDto(DateTime? PauseUntil);

public record SendOneOffReminderRequestDto(Guid? TemplateId, string? CustomMessage, string? Channel);

public record CreateReminderScheduleRequestDto(
    Guid? PlanId, Guid TemplateId, string Channel, int DaysRelativeToDue, string TimeOfDay, bool IsEnabled);

public record UpdateReminderScheduleRequestDto(
    Guid? PlanId, Guid? TemplateId, string? Channel, int? DaysRelativeToDue, string? TimeOfDay, bool? IsEnabled);

public record MagicLinkRequestDto(string Email);
