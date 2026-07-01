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

        var pastDueSubscriptions = await db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => s.Status == "PAST_DUE" && s.NextBillingDate != null 
                        && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now))
            .ToListAsync(ct);

        foreach (var sub in pastDueSubscriptions)
        {
            var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
            var daysOverdue = (now - sub.NextBillingDate!.Value).TotalDays;

            if (sub.CurrentDunningCampaignId == null)
            {
                var matchingCampaign = await db.DunningCampaigns
                    .IgnoreQueryFilters()
                    .Include(c => c.Steps)
                    .Where(c => c.OrganizationId == sub.OrganizationId && c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync(ct);

                var campaignToAssign = matchingCampaign.FirstOrDefault(c => 
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

            var campaign = await db.DunningCampaigns
                .IgnoreQueryFilters()
                .Include(c => c.Steps)
                .FirstOrDefaultAsync(c => c.Id == sub.CurrentDunningCampaignId, ct);

            if (campaign == null) continue;

            if (daysOverdue >= campaign.GracePeriodDays)
            {
                if (campaign.FinalAction == "CANCEL")
                {
                    sub.Cancel();
                    
                    var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
                    var payloadObj = new
                    {
                        subscription_id = sub.Id.ToString(),
                        client_profile_id = sub.ClientProfileId.ToString(),
                        product_id = sub.ProductId.ToString(),
                        status = "CANCELED"
                    };
                    var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    var targets = product?.FulfillmentTargets.ToList() ?? new System.Collections.Generic.List<string>();
                    foreach (var target in targets)
                    {
                        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            await eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                                sub.OrganizationId, target, "subscription.canceled", payloadElement));
                        }
                    }

                    requiresSave = true;
                    _logger.LogWarning("Subscription {Id} exhausted dunning grace period. Hard canceled.", sub.Id);
                }
                continue; 
            }

            var orderedSteps = campaign.Steps.OrderBy(s => s.DayOffset).ToList();
            if (sub.CurrentDunningStepIndex < orderedSteps.Count)
            {
                var currentStep = orderedSteps[sub.CurrentDunningStepIndex];

                if (daysOverdue >= currentStep.DayOffset)
                {
                    if (!sub.ReminderLogs.Any(l => l.ScheduleId == currentStep.Id && l.TargetBillingDate.Date == now.Date))
                    {
                        var payloadObj = new
                        {
                            subscription_id = sub.Id.ToString(),
                            client_profile_id = sub.ClientProfileId.ToString(),
                            product_id = sub.ProductId.ToString(),
                            template_id = currentStep.TemplateId.ToString(),
                            channel = currentStep.Channel
                        };
                        
                        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                            sub.OrganizationId, "COMMUNICATIONS", "reminder.due", payloadElement));

                        sub.RecordReminderDispatched(currentStep.Id, now.Date);
                        sub.AdvanceDunningStep();
                        requiresSave = true;
                        
                        _logger.LogInformation("Dispatched dunning step {Index} for Subscription {Id}.", sub.CurrentDunningStepIndex, sub.Id);
                    }
                }
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
