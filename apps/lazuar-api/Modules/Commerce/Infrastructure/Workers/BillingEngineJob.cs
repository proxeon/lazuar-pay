using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure.Dunning;
using Modules.Billing.Contracts;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts;

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
        var processedIds = new HashSet<Guid>();

        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");
            var crm = scope.ServiceProvider.GetService<ICrmQueryService>();
            var mediator = scope.ServiceProvider.GetService<IMediator>();
            var one = scope.ServiceProvider.GetService<IOneQueryService>();
            var config = scope.ServiceProvider.GetService<IConfiguration>();
            var tokens = scope.ServiceProvider.GetService<IMagicLinkTokenService>();
            var billing = scope.ServiceProvider.GetRequiredService<IBillingQueryService>();

            var excludeIds = new HashSet<Guid>(failedIds);
            excludeIds.UnionWith(processedIds);

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            try
            {
                Subscription? sub;
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimDueSubscriptionAsync(db, excludeIds, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimDueSubscriptionInMemoryAsync(db, excludeIds, ct);
                    if (sub == null) break;
                }

                try
                {
                    await ProcessOneSubscriptionAsync(db, eventBus, crm, mediator, one, config, tokens, billing, sub, failedIds, ct);
                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                    processedIds.Add(sub.Id);
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
            : """ AND "Id" <> ALL({0})""";

        var sql = $"""
            SELECT * FROM commerce."Subscriptions"
            WHERE "NextBillingDate" IS NOT NULL
              AND "NextBillingDate" <= NOW()
              AND "Status" NOT IN ('PENDING', 'PAST_DUE', 'SUSPENDED', 'CANCELED')
              AND ("CollectionPausedUntil" IS NULL OR "CollectionPausedUntil" <= NOW())
              AND "HasOpenDispute" = FALSE
              {excludeClause}
            ORDER BY "NextBillingDate"
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """;

        var query = excludeIds.Count == 0
            ? db.Subscriptions.FromSqlRaw(sql)
            : db.Subscriptions.FromSqlRaw(sql, excludeIds.ToArray());

        return await query
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
                && s.Status != "PENDING"
                && s.Status != "PAST_DUE"
                && s.Status != "SUSPENDED"
                && s.Status != "CANCELED"
                && (s.CollectionPausedUntil == null || s.CollectionPausedUntil <= now)
                && !s.HasOpenDispute
                && !excludeIds.Contains(s.Id))
            .OrderBy(s => s.NextBillingDate)
            .FirstOrDefaultAsync(ct);
    }

    private async Task ProcessOneSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        ICrmQueryService? crm,
        IMediator? mediator,
        IOneQueryService? one,
        IConfiguration? config,
        IMagicLinkTokenService? tokens,
        IBillingQueryService? billing,
        Subscription sub,
        HashSet<Guid> failedIds,
        CancellationToken ct)
    {
        var product = await db.Products.IgnoreQueryFilters().Include(p => p.Prices).FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
        if (product == null)
        {
            failedIds.Add(sub.Id);
            _logger.LogWarning(
                "Billing skipped subscription {Id}: product {ProductId} is missing.",
                sub.Id, sub.ProductId);
            return;
        }

        if (string.Equals(product.Interval, "one_time", StringComparison.OrdinalIgnoreCase))
        {
            failedIds.Add(sub.Id);
            _logger.LogInformation("Billing skipped one-time subscription {Id}.", sub.Id);
            return;
        }

        if (sub.IsCollectionPaused(DateTime.UtcNow))
        {
            failedIds.Add(sub.Id);
            _logger.LogInformation("Billing skipped collection-paused subscription {Id} until {Until}.", sub.Id, sub.CollectionPausedUntil);
            return;
        }

        if (sub.CollectionPausedUntil.HasValue)
        {
            var interval = SubscriptionBillingAmount.ResolveInterval(sub, product);
            var next = SubscriptionBillingAmount.AdvanceFrom(DateTime.UtcNow, interval);
            if (sub.TryCompleteExpiredCollectionPause(DateTime.UtcNow, next))
            {
                _logger.LogInformation(
                    "Collection pause expired for subscription {Id}; skipped back invoice, next bill {Next}.",
                    sub.Id, next);
                return;
            }
        }

        if (sub.CancelAtPeriodEnd)
        {
            sub.Cancel();
            await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
                sub.OrganizationId,
                sub.Id,
                sub.ClientProfileId,
                sub.ProductId,
                product.FulfillmentTargets.ToList()));
            _logger.LogInformation(
                "Finalized scheduled cancel for subscription {Id} at period end.",
                sub.Id);
            return;
        }

        if (sub.PendingProductId is Guid pendingId && pendingId != Guid.Empty)
        {
            // Stuck PendingProductId == ProductId: clear only. Do not re-snapshot (B02-C22).
            if (pendingId == sub.ProductId)
            {
                sub.ApplyPendingPlanChange();
            }
            else
            {
                var pendingProduct = await db.Products
                    .IgnoreQueryFilters()
                    .Include(p => p.Prices)
                    .FirstOrDefaultAsync(p => p.Id == pendingId, ct);
                if (pendingProduct == null)
                {
                    failedIds.Add(sub.Id);
                    _logger.LogWarning(
                        "Billing skipped subscription {Id}: pending product {ProductId} is missing.",
                        sub.Id, pendingId);
                    return;
                }

                var interval = SubscriptionBillingAmount.ResolveInterval(sub, product);
                if (!PlanChangePolicy.TryResolvePrice(pendingProduct, interval, out var unit, out var billedInterval, out var priceId))
                {
                    failedIds.Add(sub.Id);
                    _logger.LogWarning(
                        "Billing skipped subscription {Id}: pending product {ProductId} has no {Interval} price.",
                        sub.Id, pendingId, interval);
                    return;
                }

                sub.ApplyPendingPlanChange();
                product = pendingProduct;
                sub.SetSnapshot(unit, sub.Quantity);
                sub.SetBillingInterval(billedInterval);
                if (priceId.HasValue)
                {
                    sub.SetPriceId(priceId);
                }
            }
        }

        sub.ApplyPendingQuantity();
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(billing, sub.OrganizationId);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(sub, product, merchantHasSst);
        var chargeAmount = breakdown.Gross;

        var canCharge = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                        && !sub.IsReminderOnly
                        && !sub.HasOpenDispute
                        && !string.IsNullOrEmpty(sub.VaultedTokenId)
                        && !string.IsNullOrEmpty(sub.VaultedCustomerId);

        if (canCharge)
        {
            // Cycle key is UTC .Date (B02-C18). Claim uses full timestamptz; the log key does not convert to MYT.
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
                    chargeAmount,
                    product.Currency,
                    sub.VaultedCustomerId!,
                    sub.VaultedTokenId!,
                    DunningCampaignId: null,
                    GatewayName: product.GatewayName,
                    ChargeAttemptId: attempt.Id,
                    TaxAmount: SubscriptionBillingAmount.LineTax(breakdown),
                    TaxType: breakdown.TaxType
                ));

                _logger.LogInformation(
                    "Dispatched auto-debit request for subscription {Id} (attempt {AttemptNumber}/{Max}).",
                    sub.Id, attempt.AttemptNumber, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                return;
            }

            // TRIALING never enters dunning; without a webhook it would stall forever.
            // ACTIVE attempt-1 waits for the payment webhook (or a later failed handler).
            if (!string.Equals(sub.Status, "TRIALING", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Subscription {Id} already has attempt 1 for {Date}; waiting for webhook.",
                    sub.Id, targetDate);
                return;
            }

            _logger.LogWarning(
                "Subscription {Id} still TRIALING after attempt 1 with no webhook; marking PAST_DUE.",
                sub.Id);
        }

        if (crm == null)
        {
            throw new InvalidOperationException(
                "Cannot mark PAST_DUE without ICrmQueryService to mint a recoverable checkout.");
        }

        var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
        var email = profile?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                $"Cannot mark PAST_DUE for subscription {sub.Id} without a CRM email to mint a renewal checkout.");
        }

        if (mediator == null || one == null || tokens == null)
        {
            throw new InvalidOperationException(
                "Cannot mint a renewal checkout: IMediator, IOneQueryService, and IMagicLinkTokenService are required.");
        }

        var checkoutUrl = await RenewalCheckoutIssuer.MintAsync(
            mediator, one, config, tokens, sub, product, email, ct, billing);
        sub.SetCurrentRenewalCheckout(checkoutUrl, sub.NextBillingDate!.Value);

        sub.MarkAsPastDue();
        await StartPastDueDunningRunAsync(db, eventBus, config, billing, crm, sub, ct);

        var payloadElement = CommerceWebhookPayload.From(
            sub, product, email, "PAST_DUE", checkoutUrl: checkoutUrl, merchantHasSst: merchantHasSst);

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

        _logger.LogInformation(
            "Subscription {Id} cannot auto-debit (reminder-only, unsupported gateway, or missing vault). Marked as PAST_DUE.",
            sub.Id);
    }

    private async Task StartPastDueDunningRunAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        IConfiguration? config,
        IBillingQueryService? billing,
        ICrmQueryService? crm,
        Subscription sub,
        CancellationToken ct)
    {
        await CommerceSubscriptionLock.AcquireAsync(db, sub.Id, ct);
        await db.Entry(sub).Collection(s => s.ReminderLogs).LoadAsync(ct);
        var campaigns = await PastDueDunningProcessor.LoadActiveCampaignsAsync(db, ct);
        var whatsAppEnabled = config?.GetValue("Messaging:WhatsAppEnabled", false) ?? false;
        var processor = new PastDueDunningProcessor(_logger);
        await processor.ProcessAsync(db, eventBus, sub, campaigns, whatsAppEnabled, ct, billing, crm);
    }
}
