using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Workers;

public class BillingEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingEngineJob> _logger;
    private readonly BackgroundWorkerOptions _options;
    private const int BatchSize = 50;

    public BillingEngineJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingEngineJob> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Billing Engine Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBillingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the billing engine.");
            }

            await Task.Delay(_options.BillingEngineInterval, stoppingToken);
        }
    }

    /// <summary>One engine cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessBillingAsync(ct);

    private async Task ProcessBillingAsync(CancellationToken ct)
    {
        var failedIds = new HashSet<Guid>();

        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");
            var crm = scope.ServiceProvider.GetService<ICrmQueryService>();

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            try
            {
                Subscription? sub;
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimDueSubscriptionAsync(db, failedIds, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimDueSubscriptionInMemoryAsync(db, failedIds, ct);
                    if (sub == null) break;
                }

                try
                {
                    await ProcessOneSubscriptionAsync(db, eventBus, crm, sub, ct);
                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    failedIds.Add(sub.Id);
                    _logger.LogError(ex, "Billing failed for subscription {Id}; continuing batch.", sub.Id);
                    if (tx != null) await tx.RollbackAsync(ct);
                }
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        }
    }

    internal static async Task<Subscription?> ClaimDueSubscriptionAsync(
        CommerceDbContext db,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND "Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";

        var sql = $"""
            SELECT * FROM commerce."Subscriptions"
            WHERE "NextBillingDate" IS NOT NULL
              AND "NextBillingDate" <= NOW()
              AND "Status" NOT IN ('PAST_DUE', 'SUSPENDED', 'CANCELED')
              {excludeClause}
            ORDER BY "NextBillingDate"
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """;

        return await db.Subscriptions
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Subscription?> ClaimDueSubscriptionInMemoryAsync(
        CommerceDbContext db,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.NextBillingDate != null
                && s.NextBillingDate <= now
                && s.Status != "PAST_DUE"
                && s.Status != "SUSPENDED"
                && s.Status != "CANCELED"
                && !excludeIds.Contains(s.Id))
            .OrderBy(s => s.NextBillingDate)
            .FirstOrDefaultAsync(ct);
    }

    private async Task ProcessOneSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        ICrmQueryService? crm,
        Subscription sub,
        CancellationToken ct)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
        if (product == null) return;

        if (!string.IsNullOrEmpty(sub.VaultedTokenId) && !string.IsNullOrEmpty(sub.VaultedCustomerId))
        {
            var targetDate = sub.NextBillingDate!.Value.Date;
            // Billing owns attempt 1 only; subsequent retries are owned by dunning AUTO_CHARGE.
            var attemptCount = await db.ChargeAttemptLogs
                .CountAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);

            if (attemptCount == 0)
            {
                var attempt = new ChargeAttemptLog(
                    sub.Id,
                    targetDate,
                    attemptNumber: 1,
                    source: ChargeAttemptLog.SourceBilling);
                db.ChargeAttemptLogs.Add(attempt);

                await eventBus.PublishAsync(new Modules.Payments.Contracts.Events.ExecuteOffSessionChargeIntegrationEvent(
                    sub.OrganizationId,
                    sub.Id,
                    product.Price,
                    product.Currency,
                    sub.VaultedCustomerId,
                    sub.VaultedTokenId,
                    DunningCampaignId: null,
                    GatewayName: product.GatewayName,
                    ChargeAttemptId: attempt.Id
                ));

                _logger.LogInformation(
                    "Dispatched auto-debit request for subscription {Id} (attempt {AttemptNumber}/{Max}).",
                    sub.Id, attempt.AttemptNumber, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
            }
        }
        else
        {
            sub.MarkAsPastDue();

            string? email = null;
            if (crm != null)
            {
                var profile = await crm.GetClientProfileAsync(sub.ClientProfileId);
                email = profile?.Email;
            }

            var payloadElement = CommerceWebhookPayload.From(sub, product, email, "PAST_DUE");

            foreach (var target in product.FulfillmentTargets)
            {
                if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
                {
                    var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                    await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                        sub.OrganizationId, internalApp, "subscription.past_due", payloadElement));
                }
            }

            await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                sub.OrganizationId, TargetUrl: null, "subscription.past_due", payloadElement));

            _logger.LogInformation("Subscription {Id} lacks payment method. Marked as PAST_DUE.", sub.Id);
        }
    }
}
