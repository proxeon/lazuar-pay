using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Workers;

public class BillingEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingEngineJob> _logger;

    public BillingEngineJob(IServiceScopeFactory scopeFactory, ILogger<BillingEngineJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessBillingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

        var now = DateTime.UtcNow;
        bool requiresSave = false;

        var dueSubscriptions = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.NextBillingDate != null && s.NextBillingDate <= now)
            .ToListAsync(ct);

        foreach (var sub in dueSubscriptions)
        {
            if (sub.Status == "PAST_DUE" || sub.Status == "SUSPENDED" || sub.Status == "CANCELED")
            {
                continue;
            }

            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
            if (product == null) continue;

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

                    requiresSave = true;
                    _logger.LogInformation(
                        "Dispatched auto-debit request for subscription {Id} (attempt {AttemptNumber}/{Max}).",
                        sub.Id, attempt.AttemptNumber, ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
                }
            }
            else
            {
                sub.MarkAsPastDue();
                
                var payloadObj = new
                {
                    subscription_id = sub.Id.ToString(),
                    client_profile_id = sub.ClientProfileId.ToString(),
                    product_id = sub.ProductId.ToString(),
                    status = "PAST_DUE"
                };
                
                var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                // internal: apps still routed via FulfillmentRequested; HTTP goes once to workspace endpoints.
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

                requiresSave = true;
                _logger.LogInformation("Subscription {Id} lacks payment method. Marked as PAST_DUE.", sub.Id);
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
