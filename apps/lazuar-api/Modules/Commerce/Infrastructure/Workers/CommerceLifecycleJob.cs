using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Contracts.Events;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.Workers;

public class CommerceLifecycleJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommerceLifecycleJob> _logger;
    private const int DefaultGracePeriodDays = 3;

    public CommerceLifecycleJob(IServiceScopeFactory scopeFactory, ILogger<CommerceLifecycleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Commerce Lifecycle Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecycleActionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing commerce lifecycle actions.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessLifecycleActionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

        var now = DateTime.UtcNow;
        bool requiresSave = false;

        var overdue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "ACTIVE" && s.NextBillingDate != null && s.NextBillingDate < now)
            .ToListAsync(ct);

        foreach (var sub in overdue)
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
                
                await eventBus.PublishAsync(new SubscriptionSuspendedIntegrationEvent(
                    sub.OrganizationId, sub.Id, sub.ClientProfileId, sub.ProductId, product.FulfillmentTargets.ToList()
                ));
                
                requiresSave = true;
                _logger.LogInformation("Transitioned subscription {Id} to PAST_DUE state.", sub.Id);
            }
        }

        var pastDue = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextBillingDate != null)
            .ToListAsync(ct);

        foreach (var sub in pastDue)
        {
            if (sub.NextBillingDate!.Value.AddDays(DefaultGracePeriodDays) < now)
            {
                sub.Cancel();
                
                var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                var targets = product?.FulfillmentTargets.ToList() ?? new System.Collections.Generic.List<string>();

                await eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
                    sub.OrganizationId, sub.Id, sub.ClientProfileId, sub.ProductId, targets
                ));
                
                requiresSave = true;
                _logger.LogWarning("Subscription {Id} exceeded grace period. Transitioned to CANCELED.", sub.Id);
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
