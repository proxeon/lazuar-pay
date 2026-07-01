using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Communications.Contracts;
using Modules.Communications.Domain.Aggregates;
using Modules.Commerce.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Infrastructure.Workers;

public class BroadcastFanoutJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BroadcastFanoutJob> _logger;
    private const int PageSize = 100;

    public BroadcastFanoutJob(IServiceScopeFactory scopeFactory, ILogger<BroadcastFanoutJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Broadcast fan-out job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBroadcastsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in broadcast fan-out job.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingBroadcastsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
        var subscriberQuery = scope.ServiceProvider.GetRequiredService<ISubscriberQueryService>();
        var suppression = scope.ServiceProvider.GetRequiredService<ISuppressionService>();
        var costService = scope.ServiceProvider.GetRequiredService<ICreditCostService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommunicationsEventBus");

        var queued = await db.Broadcasts
            .IgnoreQueryFilters()
            .Where(b => b.Status == "QUEUED")
            .ToListAsync(ct);

        foreach (var broadcast in queued)
        {
            await ProcessOneAsync(broadcast, db, subscriberQuery, suppression, costService, mediator, eventBus, ct);
        }
    }

    private async Task ProcessOneAsync(
        Broadcast broadcast,
        CommunicationsDbContext db,
        ISubscriberQueryService subscriberQuery,
        ISuppressionService suppression,
        ICreditCostService costService,
        IMediator mediator,
        IEventBus eventBus,
        CancellationToken ct)
    {
        var costPerRecipient = costService.GetCost(CreditAction.BroadcastEmailPerRecipient);
        var holdId = broadcast.CreditHoldId!.Value;

        try
        {
            broadcast.MarkSending();
            await db.SaveChangesAsync(ct);

            var page = 1;
            while (true)
            {
                var recipients = await subscriberQuery.GetActiveSubscriberRecipientsAsync(broadcast.OrganizationId, page, PageSize);
                if (recipients.Count == 0) break;

                foreach (var recipient in recipients)
                {
                    if (await suppression.IsSuppressedAsync(broadcast.OrganizationId, recipient.Email))
                    {
                        broadcast.RecordSuppressed();
                        continue;
                    }

                    // Consume from the reserved hold BEFORE dispatching so a send can never happen
                    // without a committed credit.
                    await mediator.Send(new ConsumeCreditHoldCommand(
                        broadcast.OrganizationId, holdId, costPerRecipient,
                        $"Broadcast recipient: {recipient.Email}"), ct);

                    await eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                        broadcast.OrganizationId,
                        recipient.Email,
                        null,
                        broadcast.Subject,
                        broadcast.EmailBody,
                        null,
                        "EMAIL",
                        holdId));

                    broadcast.RecordSent(costPerRecipient);
                }

                // Persist progress so the UI can show live counts.
                await db.SaveChangesAsync(ct);
                page++;

                if (recipients.Count < PageSize) break;
            }

            // Release unused credits (suppressed recipients) back to the wallet.
            await mediator.Send(new ReleaseCreditHoldCommand(
                broadcast.OrganizationId, holdId, $"Broadcast completed: {broadcast.Subject}"), ct);

            broadcast.MarkCompleted();
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Broadcast {Id} completed: {Sent} sent, {Suppressed} suppressed, {Failed} failed.",
                broadcast.Id, broadcast.SentCount, broadcast.SuppressedCount, broadcast.FailedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Broadcast {Id} failed during fan-out.", broadcast.Id);
            try
            {
                await mediator.Send(new ReleaseCreditHoldCommand(
                    broadcast.OrganizationId, holdId, $"Broadcast failed: {broadcast.Subject}"), ct);
            }
            catch (Exception releaseEx)
            {
                _logger.LogError(releaseEx, "Failed to release credit hold {HoldId} for failed broadcast {Id}.", holdId, broadcast.Id);
            }

            broadcast.MarkFailed(ex.Message);
            await db.SaveChangesAsync(ct);
        }
    }
}
