using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Billing.Contracts;
using Modules.Commerce.Contracts;
using Modules.Communications.Application;
using Modules.Communications.Contracts.Commands;

namespace Modules.Communications.Infrastructure;

public static class BroadcastEndpoints
{
    public static RouteGroupBuilder MapBroadcastEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/broadcasts", async Task<Results<Ok<IdResponse>, BadRequest<string>>> (
            CreateBroadcastRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var command = new SendBroadcastCommand(
                ctx.TenantId,
                req.Subject,
                req.Email_body,
                req.Whatsapp_body,
                req.Channel);

            try
            {
                var id = await mediator.Send(command);
                return TypedResults.Ok(new IdResponse { Id = id.ToString() });
            }
            catch (BusinessRuleValidationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        // Register /broadcasts/preview before /broadcasts/{id} so "preview" is not captured as a Guid.
        group.MapGet("/broadcasts/preview", async Task<Ok<BroadcastCostPreviewDto>> (
            IExecutionContextAccessor ctx,
            ISubscriberQueryService subscriberQuery,
            IBillingQueryService billingQuery) =>
        {
            var recipientCount = await subscriberQuery.GetActiveSubscriberCountAsync(ctx.TenantId);
            // v1 broadcasts are free; credit cost fields reserved (always 0 / sufficient).
            var available = await billingQuery.GetAvailableCreditsAsync(ctx.TenantId);

            return TypedResults.Ok(new BroadcastCostPreviewDto
            {
                Recipient_count = recipientCount,
                Credits_per_recipient = 0,
                Total_credits = 0,
                Sufficient_credits = true,
                Available_credits = available
            });
        });

        group.MapGet("/broadcasts/{id:guid}", async Task<Results<Ok<BroadcastStatusDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            ICommunicationsRepository repository) =>
        {
            var broadcast = await repository.GetBroadcastByIdAsync(ctx.TenantId, id);
            if (broadcast == null) return TypedResults.NotFound();

            return TypedResults.Ok(new BroadcastStatusDto
            {
                Id = broadcast.Id.ToString(),
                Status = broadcast.Status,
                Total_recipients = broadcast.TotalRecipients,
                Sent_count = broadcast.SentCount,
                Suppressed_count = broadcast.SuppressedCount,
                Failed_count = broadcast.FailedCount,
                Credits_reserved = 0, // Reserved; v1 free
                Credits_used = 0,     // Reserved; v1 free
                Created_at = new DateTimeOffset(broadcast.CreatedAt),
                Completed_at = broadcast.CompletedAt.HasValue ? new DateTimeOffset(broadcast.CompletedAt.Value) : null,
                Failure_reason = broadcast.FailureReason
            });
        });

        return group;
    }
}
