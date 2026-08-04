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
using Microsoft.EntityFrameworkCore;
using Modules.Messaging.Application;

namespace Modules.Messaging.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/messaging");

        group.MapPost("/notify", async (SendTenantNotificationCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            return Results.Accepted();
        }).RequireAuthorization("OrgAdmin");

        // Support: list recent delivery attempts for the current tenant.
        group.MapGet("/delivery-logs", async Task<Ok<IReadOnlyList<MessageDeliveryLogDto>>> (
            [FromServices] IExecutionContextAccessor ctx,
            [FromServices] MessagingDbContext db,
            [FromQuery] int? limit) =>
        {
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var rows = await db.MessageDeliveryLogs
                .AsNoTracking()
                .Where(l => l.OrganizationId == ctx.TenantId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .Select(l => new MessageDeliveryLogDto(
                    l.Id,
                    l.Channel,
                    l.Recipient,
                    l.Status,
                    l.ProviderMessageId,
                    l.Error,
                    l.CorrelationEventId,
                    l.CreatedAt))
                .ToListAsync();

            return TypedResults.Ok((IReadOnlyList<MessageDeliveryLogDto>)rows);
        }).RequireAuthorization("OrgAdmin");

        return endpoints;
    }
}

public record MessageDeliveryLogDto(
    Guid Id,
    string Channel,
    string Recipient,
    string Status,
    string? ProviderMessageId,
    string? Error,
    Guid? CorrelationEventId,
    DateTime CreatedAt);
