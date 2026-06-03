using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Commands;

namespace Modules.Community.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/community").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/community");

        // Admin: Create Plan
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

        // Admin: Record Manual Payment
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

        // Admin: Cancel Subscription
        admin.MapPost("/subscribers/{id:guid}/cancel", async (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new CancelSubscriptionCommand(ctx.TenantId, id);
            await mediator.Send(command);
            return Results.Ok(new { status = "cancelled" });
        });

        // Public: Initiate Checkout
        publicGroup.MapPost("/checkout/{subscriptionId:guid}", async (
            Guid subscriptionId,
            [FromQuery] Guid orgId, // Passed by frontend based on Tenant Slug
            [FromBody] InitiateCheckoutRequestDto req,
            IMediator mediator) =>
        {
            var command = new InitiateSubscriptionCheckoutCommand(
                orgId, 
                subscriptionId, 
                req.SuccessUrl, 
                req.CancelUrl);
            
            // Calls Community module, which cross-queries Payments module, returns the actual URL
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
