using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommunicationsEventBus");

        var queued = await db.Broadcasts
            .IgnoreQueryFilters()
            .Where(b => b.Status == "QUEUED")
            .ToListAsync(ct);

        foreach (var broadcast in queued)
        {
            await ProcessOneAsync(broadcast, db, subscriberQuery, suppression, eventBus, ct);
        }
    }

    private async Task ProcessOneAsync(
        Broadcast broadcast,
        CommunicationsDbContext db,
        ISubscriberQueryService subscriberQuery,
        ISuppressionService suppression,
        IEventBus eventBus,
        CancellationToken ct)
    {
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

                    await eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                        broadcast.OrganizationId,
                        recipient.Email,
                        null,
                        broadcast.Subject,
                        broadcast.EmailBody,
                        null,
                        "EMAIL",
                        broadcast.Id)); // Pass broadcast ID instead of CreditHoldId to bypass wallet checks

                    broadcast.RecordSent();
                }

                await db.SaveChangesAsync(ct);
                page++;

                if (recipients.Count < PageSize) break;
            }

            broadcast.MarkCompleted();
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Broadcast {Id} completed: {Sent} sent, {Suppressed} suppressed, {Failed} failed.",
                broadcast.Id, broadcast.SentCount, broadcast.SuppressedCount, broadcast.FailedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Broadcast {Id} failed during fan-out.", broadcast.Id);

            broadcast.MarkFailed(ex.Message);
            await db.SaveChangesAsync(ct);
        }
    }
}
