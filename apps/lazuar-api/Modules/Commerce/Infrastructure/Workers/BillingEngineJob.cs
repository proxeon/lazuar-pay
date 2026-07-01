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
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

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
            .Where(s => s.Status == "ACTIVE" && s.NextBillingDate != null && s.NextBillingDate <= now)
            .ToListAsync(ct);

        foreach (var sub in dueSubscriptions)
        {
            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
            if (product == null) continue;

            if (!string.IsNullOrEmpty(sub.VaultedTokenId) && !string.IsNullOrEmpty(sub.VaultedCustomerId))
            {
                var targetDate = sub.NextBillingDate!.Value.Date;
                var attemptExists = await db.ChargeAttemptLogs
                    .AnyAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == targetDate, ct);

                if (!attemptExists)
                {
                    db.ChargeAttemptLogs.Add(new ChargeAttemptLog(sub.Id, targetDate));
                    
                    await eventBus.PublishAsync(new ExecuteOffSessionChargeIntegrationEvent(
                        sub.OrganizationId,
                        sub.Id,
                        product.Price,
                        product.Currency,
                        sub.VaultedCustomerId,
                        sub.VaultedTokenId
                    ));
                    
                    requiresSave = true;
                    _logger.LogInformation("Dispatched auto-debit request for subscription {Id}.", sub.Id);
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

                foreach (var target in product.FulfillmentTargets)
                {
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                            sub.OrganizationId, target, "subscription.suspended", payloadElement));
                    }
                }
                
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
