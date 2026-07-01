using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Billing.Contracts;
using Modules.Commerce.Contracts;
using Modules.Communications.Application;
using Modules.Communications.Contracts;
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

        group.MapGet("/broadcasts/preview", async Task<Ok<BroadcastCostPreviewDto>> (
            IExecutionContextAccessor ctx,
            ISubscriberQueryService subscriberQuery,
            ICreditCostService costService,
            IBillingQueryService billingQuery) =>
        {
            var recipientCount = await subscriberQuery.GetActiveSubscriberCountAsync(ctx.TenantId);
            var costPerRecipient = costService.GetCost(CreditAction.BroadcastEmailPerRecipient);
            var totalCredits = recipientCount * costPerRecipient;
            var available = await billingQuery.GetAvailableCreditsAsync(ctx.TenantId);

            return TypedResults.Ok(new BroadcastCostPreviewDto
            {
                RecipientCount = recipientCount,
                CreditsPerRecipient = costPerRecipient,
                TotalCredits = totalCredits,
                SufficientCredits = available >= totalCredits,
                AvailableCredits = available
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
                TotalRecipients = broadcast.TotalRecipients,
                SentCount = broadcast.SentCount,
                SuppressedCount = broadcast.SuppressedCount,
                FailedCount = broadcast.FailedCount,
                CreditsReserved = broadcast.CreditsReserved,
                CreditsUsed = broadcast.CreditsUsed,
                CreatedAt = new DateTimeOffset(broadcast.CreatedAt),
                CompletedAt = broadcast.CompletedAt.HasValue ? new DateTimeOffset(broadcast.CompletedAt.Value) : null,
                FailureReason = broadcast.FailureReason
            });
        });

        return group;
    }
}
