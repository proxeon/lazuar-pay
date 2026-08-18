using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Communications.Contracts;
using Modules.Communications.Domain.Aggregates;
using Modules.Commerce.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Communications.Infrastructure.Workers;

public class BroadcastFanoutJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BroadcastFanoutJob> _logger;
    private readonly BackgroundWorkerOptions _options;
    private const int PageSize = 100;

    public BroadcastFanoutJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<BroadcastFanoutJob> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
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

            await Task.Delay(_options.BroadcastFanoutInterval, stoppingToken);
        }
    }

    /// <summary>One poll cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessPendingBroadcastsAsync(ct);

    private async Task ProcessPendingBroadcastsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
        var subscriberQuery = scope.ServiceProvider.GetRequiredService<ISubscriberQueryService>();
        var suppression = scope.ServiceProvider.GetRequiredService<ISuppressionService>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommunicationsEventBus");

        var claimed = await ClaimQueuedBroadcastsAsync(db, ct);
        foreach (var broadcast in claimed)
        {
            await ProcessOneAsync(broadcast, db, subscriberQuery, suppression, eventBus, ct);
        }
    }

    /// <summary>
    /// Claims QUEUED broadcasts with FOR UPDATE SKIP LOCKED, then MarkSending so other workers skip them.
    /// </summary>
    internal static async Task<List<Broadcast>> ClaimQueuedBroadcastsAsync(
        CommunicationsDbContext db,
        CancellationToken ct)
    {
        List<Broadcast> queued;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            const string sql = """
                SELECT * FROM communications."Broadcasts"
                WHERE "Status" = 'QUEUED'
                ORDER BY "CreatedAt"
                LIMIT 20
                FOR UPDATE SKIP LOCKED;
                """;

            queued = await db.Broadcasts
                .FromSqlRaw(sql)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            if (queued.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return queued;
            }

            foreach (var broadcast in queued)
            {
                broadcast.MarkSending();
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            queued = await db.Broadcasts
                .IgnoreQueryFilters()
                .Where(b => b.Status == "QUEUED")
                .OrderBy(b => b.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            foreach (var broadcast in queued)
            {
                broadcast.MarkSending();
            }

            if (queued.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }
        }

        return queued;
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
            var apiBaseUrl = _configuration["App:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080/api/v1";
            var hasJwtSecret = PublicComplianceEndpoints.TryJwtHmacSecret(_configuration, out var jwtSecret);

            var page = 1;
            while (true)
            {
                var recipients = await subscriberQuery.GetActiveSubscriberRecipientsAsync(broadcast.OrganizationId, page, PageSize);
                if (recipients.Count == 0) break;

                foreach (var recipient in recipients)
                {
                    if (await suppression.IsSuppressedAsync(broadcast.OrganizationId, recipient.Email, SuppressionLane.Marketing))
                    {
                        broadcast.RecordSuppressed();
                        continue;
                    }

                    string? unsubscribeUrl = null;
                    if (hasJwtSecret)
                    {
                        unsubscribeUrl = PublicComplianceEndpoints.BuildUnsubscribeUrl(
                            apiBaseUrl,
                            broadcast.OrganizationId,
                            recipient.Email,
                            jwtSecret);
                    }

                    await eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
                        OrganizationId: broadcast.OrganizationId,
                        ToEmail: recipient.Email,
                        ToPhone: null,
                        Subject: broadcast.Subject,
                        HtmlEmailBody: broadcast.EmailBody,
                        PlainTextPhoneBody: null,
                        Channel: "EMAIL",
                        CreditHoldId: broadcast.Id,
                        UnsubscribeUrl: unsubscribeUrl));

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
