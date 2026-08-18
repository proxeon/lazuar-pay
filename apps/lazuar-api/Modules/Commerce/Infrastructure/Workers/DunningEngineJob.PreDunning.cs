using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Workers;

public partial class DunningEngineJob
{
    private async Task ProcessPreDunningSubscriptionAsync(
        CommerceDbContext db,
        IEventBus eventBus,
        List<DunningCampaign> campaigns,
        Subscription sub,
        bool whatsAppEnabled,
        CancellationToken ct,
        IBillingQueryService? billing = null,
        ICrmQueryService? crm = null)
    {
        var now = DateTime.UtcNow;
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
        var inferredPaymentMethod = DunningCampaignMatcher.InferPaymentMethod(sub.VaultedTokenId, product?.GatewayName);
        var campaign = DunningCampaignMatcher.FindBest(
            campaigns, sub.OrganizationId, sub.ProductId, inferredPaymentMethod);

        if (campaign == null) return;

        int daysUntilDue = (sub.NextBillingDate!.Value.Date - now.Date).Days;
        var targetDate = sub.NextBillingDate.Value.Date;

        var dueSteps = campaign.Steps
            .Where(s => s.DayOffset < 0
                && daysUntilDue <= Math.Abs(s.DayOffset)
                && (s.ActionType == "EMAIL" || s.ActionType == "WHATSAPP" || s.ActionType == "ALL"))
            .Where(s => !sub.ReminderLogs.Any(l =>
                l.DayOffset == s.DayOffset && l.TargetBillingDate.Date == targetDate))
            .OrderBy(s => s.DayOffset)
            .ToList();

        foreach (var step in dueSteps)
        {
            var effectiveAction = ResolveEffectiveCommunicationAction(step, whatsAppEnabled);
            if (effectiveAction == null)
            {
                _logger.LogInformation(
                    "Skipped pre-dunning WHATSAPP step DayOffset={DayOffset} ({StepId}) for Subscription {SubId}: WhatsApp disabled and no email body.",
                    step.DayOffset, step.Id, sub.Id);
                sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
                continue;
            }

            var sent = await DispatchCommunicationStepAsync(db, sub, step, daysOverdue: 0, effectiveAction, eventBus, ct, billing, crm);
            if (!sent)
            {
                _logger.LogWarning(
                    "Did not consume pre-dunning DayOffset={DayOffset} for Subscription {SubId}: CRM profile or email missing.",
                    step.DayOffset, sub.Id);
                continue;
            }

            sub.RecordReminderDispatched(step.Id, targetDate, step.DayOffset);
            _logger.LogInformation(
                "Dispatched pre-dunning step DayOffset={DayOffset} ({StepId}) for Subscription {SubId} as {Action}.",
                step.DayOffset, step.Id, sub.Id, effectiveAction);
        }
    }
}
