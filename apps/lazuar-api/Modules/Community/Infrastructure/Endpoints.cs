using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
            Guid orgId, // In reality, resolve from tenant slug
            IMediator mediator) =>
        {
            var command = new InitiateSubscriptionCheckoutCommand(orgId, subscriptionId);
            await mediator.Send(command);
            
            // NOTE: In the real implementation, this endpoint would then call the Payments module
            // to get the actual Stripe/Billplz URL. Since we are using events, the command above 
            // just drops the "Initiated" event into the outbox for the abandoned cart timer.
            
            return Results.Ok(new { status = "checkout_initiated" });
        });

        return endpoints;
    }
}

public record CreatePlanRequestDto(
    string Slug, string Name, string Audience, string ShortDescription, string LongDescription,
    decimal Price, string Interval, List<string> Features, string Methodology,
    List<FaqRequestDto> Faq, int DisplayOrder, int? MaxCapacity, int GracePeriodDays,
    string? TelegramInviteLink, string? WeeklyMeetingLink);

public record FaqRequestDto(string Id, string Question, string Answer);

public record RecordPaymentRequestDto(
    decimal Amount, string PaymentMethod, string? ReferenceNumber, string? ReceiptFile);
