using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Dunning;

internal static class DunningStepDispatcher
{
    /// <summary>
    /// When WhatsApp is not productized (Messaging:WhatsAppEnabled=false), demote WHATSAPP/ALL
    /// to email-only recovery. Pure WhatsApp steps without email copy are skipped.
    /// </summary>
    public static string? ResolveEffectiveCommunicationAction(IDunningStepCopy step, bool whatsAppEnabled)
    {
        var action = (step.ActionType ?? "EMAIL").ToUpperInvariant();
        if (action is "AUTOCHARGE" or "AUTO_CHARGE") return action;

        if (whatsAppEnabled) return action;

        if (action == "WHATSAPP")
        {
            if (!string.IsNullOrWhiteSpace(step.EmailBody))
                return "EMAIL";
            return null;
        }

        if (action == "ALL")
            return "EMAIL";

        return action;
    }

    /// <summary>
    /// Hosted pay-this-cycle URL only when it was minted for the current <see cref="Subscription.NextBillingDate"/>.
    /// </summary>
    public static string? ResolveLiveRenewalCheckoutUrl(Subscription sub)
    {
        if (string.IsNullOrWhiteSpace(sub.CurrentRenewalCheckoutUrl)
            || !sub.CurrentRenewalCheckoutForDate.HasValue
            || !sub.NextBillingDate.HasValue)
        {
            return null;
        }

        if (sub.CurrentRenewalCheckoutForDate.Value.Date != sub.NextBillingDate.Value.Date)
            return null;

        return sub.CurrentRenewalCheckoutUrl;
    }

    public static async Task<bool> DispatchCommunicationStepAsync(
        CommerceDbContext db,
        Subscription sub,
        IDunningStepCopy step,
        int daysOverdue,
        string effectiveActionType,
        IEventBus eventBus,
        CancellationToken ct,
        IBillingQueryService? billing = null,
        ICrmQueryService? crm = null)
    {
        if (crm != null)
        {
            var profile = await crm.GetClientProfileAsync(sub.OrganizationId, sub.ClientProfileId);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Email))
            {
                return false;
            }
        }

        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sub.ProductId, ct);
        var checkoutUrl = ResolveLiveRenewalCheckoutUrl(sub) ?? string.Empty;
        var amount = product == null
            ? 0m
            : await SubscriptionBillingAmount.Gross(sub, product, billing);

        var payloadObj = new
        {
            subscription_id = sub.Id.ToString(),
            client_profile_id = sub.ClientProfileId.ToString(),
            product_id = sub.ProductId.ToString(),
            action_type = effectiveActionType,
            subject = step.Subject,
            email_body = step.EmailBody,
            whatsapp_body = effectiveActionType == "EMAIL" ? string.Empty : step.WhatsAppBody,
            plan_name = product?.Name ?? string.Empty,
            amount,
            total_price = amount,
            currency = product?.Currency ?? string.Empty,
            days_overdue = daysOverdue,
            // PAST_DUE: this is the missed due date, not a future renews-on (B03-C29 / 195).
            // Pre-dunning copy may say "renews on"; day-0+ default seed says "due".
            current_period_end = (sub.CurrentPeriodEnd ?? sub.NextBillingDate).HasValue
                ? (sub.CurrentPeriodEnd ?? sub.NextBillingDate)!.Value.ToString("yyyy-MM-dd")
                : string.Empty,
            checkout_url = checkoutUrl
        };

        var payloadElement = JsonSerializer.SerializeToElement(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
            sub.OrganizationId, "COMMUNICATIONS", "reminder.dunning", payloadElement));
        return true;
    }
}
