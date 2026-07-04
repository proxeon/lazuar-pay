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
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.Workers;

public class DunningEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DunningEngineJob> _logger;

    public DunningEngineJob(IServiceScopeFactory scopeFactory, ILogger<DunningEngineJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dunning Engine Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDunningAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the dunning engine.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessDunningAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");

        var now = DateTime.UtcNow;
        bool requiresSave = false;

        var campaigns = await db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PriorityOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        // Track 1: Pre-Dunning (ACTIVE subscriptions nearing renewal)
        var preDunningSubs = await db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => s.Status == "ACTIVE" && s.NextBillingDate != null && s.NextBillingDate > now && s.NextBillingDate <= now.AddDays(14))
            .ToListAsync(ct);

        foreach (var sub in preDunningSubs)
        {
            var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
            var campaign = campaigns.FirstOrDefault(c => 
                c.OrganizationId == sub.OrganizationId &&
                (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
                (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod))
            );

            if (campaign == null) continue;

            int daysUntilDue = (sub.NextBillingDate!.Value.Date - now.Date).Days;
            
            var step = campaign.Steps.FirstOrDefault(s => s.DayOffset < 0 && Math.Abs(s.DayOffset) == daysUntilDue && (s.ActionType == "EMAIL" || s.ActionType == "WHATSAPP" || s.ActionType == "ALL"));
            
            if (step != null && !sub.ReminderLogs.Any(l => l.ScheduleId == step.Id && l.TargetBillingDate.Date == sub.NextBillingDate.Value.Date))
            {
                await DispatchCommunicationStepAsync(sub, step, eventBus);
                sub.RecordReminderDispatched(step.Id, sub.NextBillingDate.Value.Date);
                requiresSave = true;
                _logger.LogInformation("Dispatched pre-dunning step {StepId} for Subscription {SubId}.", step.Id, sub.Id);
            }
        }

        // Track 2 & 3: Active Dunning and Terminal Escalation
        var pastDueSubs = await db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextBillingDate != null && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now))
            .ToListAsync(ct);

        foreach (var sub in pastDueSubs)
        {
            var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
            int daysOverdue = (now.Date - sub.NextBillingDate!.Value.Date).Days;

            if (sub.CurrentDunningCampaignId == null)
            {
                var campaignToAssign = campaigns.FirstOrDefault(c => 
                    c.OrganizationId == sub.OrganizationId &&
                    (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
                    (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod))
                );

                if (campaignToAssign != null)
                {
                    sub.AssignDunningCampaign(campaignToAssign.Id);
                    requiresSave = true;
                }
                else
                {
                    continue;
                }
            }

            var campaign = campaigns.FirstOrDefault(c => c.Id == sub.CurrentDunningCampaignId);
            if (campaign == null) continue;

            // Track 3: Terminal Escalation
            if (daysOverdue >= campaign.GracePeriodDays)
            {
                if (campaign.FinalAction == "CANCEL" || campaign.FinalAction == "SUSPEND")
                {
                    var statusString = campaign.FinalAction == "CANCEL" ? "CANCELED" : "SUSPENDED";
                    var eventTypeString = campaign.FinalAction == "CANCEL" ? "subscription.canceled" : "subscription.suspended";

                    if (campaign.FinalAction == "CANCEL")
                    {
                        sub.Cancel();
                        campaign.RecordChurn();
                        _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Canceled.", sub.Id);
                    }
                    else
                    {
                        sub.Suspend();
                        _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Suspended.", sub.Id);
                    }

                    var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                    
                    var payloadObj = new
                    {
                        subscription_id = sub.Id.ToString(),
                        client_profile_id = sub.ClientProfileId.ToString(),
                        product_id = sub.ProductId.ToString(),
                        status = statusString
                    };
                    var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    var targets = product?.FulfillmentTargets.ToList() ?? new System.Collections.Generic.List<string>();
                    foreach (var target in targets)
                    {
                        if (target.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
                        {
                            var internalApp = target.Substring("internal:".Length).Trim().ToUpperInvariant();
                            await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                                sub.OrganizationId, internalApp, eventTypeString, payloadElement));
                        }
                        else if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                                sub.OrganizationId, target, eventTypeString, payloadElement));
                        }
                    }
                    requiresSave = true;
                }
                continue; 
            }

            // Track 2: Active Dunning Steps
            var step = campaign.Steps.FirstOrDefault(s => s.DayOffset == daysOverdue);
            if (step != null && !sub.ReminderLogs.Any(l => l.ScheduleId == step.Id && l.TargetBillingDate.Date == sub.NextBillingDate.Value.Date))
            {
                if (step.ActionType == "AUTOCHARGE" || step.ActionType == "AUTO_CHARGE")
                {
                    var attemptCount = await db.ChargeAttemptLogs.CountAsync(l => l.SubscriptionId == sub.Id && l.TargetBillingDate == sub.NextBillingDate.Value.Date, ct);
                    
                    // Safety Guard: Limit auto-charge retries to max 4 times
                    if (attemptCount < 4 && !string.IsNullOrEmpty(sub.VaultedCustomerId) && !string.IsNullOrEmpty(sub.VaultedTokenId))
                    {
                        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                        if (product != null)
                        {
                            db.ChargeAttemptLogs.Add(new Domain.Entities.ChargeAttemptLog(sub.Id, sub.NextBillingDate.Value.Date));
                            
                            await eventBus.PublishAsync(new ExecuteOffSessionChargeIntegrationEvent(
                                sub.OrganizationId,
                                sub.Id,
                                product.Price,
                                product.Currency,
                                sub.VaultedCustomerId,
                                sub.VaultedTokenId
                            ));
                            _logger.LogInformation("Dispatched auto-charge dunning step for Subscription {Id}.", sub.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Skipped auto-charge for Subscription {Id} due to limits or missing token. Falling back.", sub.Id);
                    }
                }
                else
                {
                    await DispatchCommunicationStepAsync(sub, step, eventBus);
                    _logger.LogInformation("Dispatched communication dunning step for Subscription {Id}.", sub.Id);
                }

                sub.RecordReminderDispatched(step.Id, sub.NextBillingDate.Value.Date);
                requiresSave = true;
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchCommunicationStepAsync(Domain.Aggregates.Subscription sub, Domain.Entities.DunningStep step, IEventBus eventBus)
    {
        var payloadObj = new
        {
            subscription_id = sub.Id.ToString(),
            client_profile_id = sub.ClientProfileId.ToString(),
            product_id = sub.ProductId.ToString(),
            action_type = step.ActionType,
            subject = step.Subject,
            email_body = step.EmailBody,
            whatsapp_body = step.WhatsAppBody
        };
        
        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
            sub.OrganizationId, "COMMUNICATIONS", "reminder.dunning", payloadElement));
    }
}
