using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Commands;
using Modules.Community.Application.Queries;
// Resolve TenantSlug to OrgId for public endpoints
using Modules.Tenant.Contracts; 

namespace Modules.Community.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/community").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/community");

        // ==========================================
        // QUERIES (READ MODELS) - Phase 3 additions
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

        admin.MapPost("/plans", async (
            CreatePlanRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
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

        admin.MapPost("/subscribers/{id:guid}/payments", async (
            Guid id, 
            RecordPaymentRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new RecordSubscriptionPaymentCommand(
                ctx.TenantId, id, req.Amount, "MYR", req.PaymentMethod, 
                req.ReferenceNumber, "ADMIN", req.ReceiptFile);

            await mediator.Send(command);
            return Results.Ok(new { status = "paid" });
        });

        admin.MapPost("/subscribers/{id:guid}/cancel", async (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new CancelSubscriptionCommand(ctx.TenantId, id);
            await mediator.Send(command);
            return Results.Ok(new { status = "cancelled" });
        });

        publicGroup.MapPost("/checkout/{subscriptionId:guid}", async (
            Guid subscriptionId,
            [FromQuery] string tenant,
            [FromBody] InitiateCheckoutRequestDto req,
            ITenantQueryService tenantQueryService,
            IMediator mediator) =>
        {
            // Resolve orgId from the query param string "tenant"
            var org = await tenantQueryService.GetTenantBySlugAsync(tenant);
            if (org == null) return Results.NotFound(new { error = "Business not found." });

            var command = new InitiateSubscriptionCheckoutCommand(
                org.Id, 
                subscriptionId, 
                req.SuccessUrl, 
                req.CancelUrl);
            
            var url = await mediator.Send(command);
            return Results.Ok(new { url });
        });

        return endpoints;
    }
}

public record InitiateCheckoutRequestDto(string SuccessUrl, string CancelUrl);

public record CreatePlanRequestDto(
    string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
    decimal Price, string Interval, List<string> Features, string Methodology,
    List<FaqRequestDto> Faq, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
    string? TelegramInviteLink, string? WeeklyMeetingLink);

public record FaqRequestDto(string Id, string Question, string Answer);

public record RecordPaymentRequestDto(
    decimal Amount, string PaymentMethod, string? ReferenceNumber, string? ReceiptFile);
