using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Messaging.Domain;

namespace Modules.Messaging.Infrastructure;

public static class AutomationRuleSeeder
{
    public static async Task SeedDefaultRulesAsync(Guid orgId, MessagingDbContext db)
    {
        var existingTemplates = await db.MessageTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == orgId)
            .ToListAsync();

        if (!existingTemplates.Any())
        {
            var defaultTemplates = GetDefaultTemplates(orgId);
            db.MessageTemplates.AddRange(defaultTemplates);
            await db.SaveChangesAsync();
            
            existingTemplates = defaultTemplates;
        }

        var lookup = existingTemplates.ToDictionary(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase);
        Guid? Tpl(string name) => lookup.TryGetValue(name, out var id) ? id : null;

        var existingRules = await db.AutomationRules
            .IgnoreQueryFilters()
            .Where(r => r.OrganizationId == orgId)
            .AnyAsync();

        if (existingRules) return; // Prevent double seeding

        var rules = new List<AutomationRule>
        {
            new(orgId, "Post-Visit Thank You", "BOOKING_COMPLETED", "EMAIL", Tpl("Thank You / Post-Visit"), 120, false),
            new(orgId, "No-Show Follow Up", "BOOKING_NO_SHOW", "EMAIL", Tpl("Promotional Offer"), 60, false),
            new(orgId, "Cancellation Follow Up", "BOOKING_CANCELLED", "EMAIL", Tpl("Promotional Offer"), 60, false),
            new(orgId, "Birthday Greeting", "BIRTHDAY", "EMAIL", Tpl("Promotional Offer"), 0, false),
            new(orgId, "Pass Renewal Reminder", "PASS_RENEWAL_DUE", "WHATSAPP", Tpl("Pass Renewal Reminder"), 0, true),
            
            // ─── Community Subscription Recovery Reminders ─────────────────────
            new(orgId, "Community Abandoned (12h)", "COMMUNITY_ABANDONED", "WHATSAPP", Tpl("Abandoned Cart (12h)"), 720, true),
            new(orgId, "Community Abandoned (24h)", "COMMUNITY_ABANDONED", "EMAIL", Tpl("Abandoned Cart (24h)"), 1440, true)
        };

        db.AutomationRules.AddRange(rules);
        await db.SaveChangesAsync();
    }

    public static List<MessageTemplate> GetDefaultTemplates(Guid orgId) => new()
    {
        new(orgId, "Booking Confirmation", "ALL", "Your booking {{booking_ref}} is confirmed!", "Hi {{customer_name}},\n\nYour booking has been confirmed!\n\n📅 {{appointment_time}}\n📍 {{branch_name}}\n💆 {{service_name}}\n🔖 {{booking_ref}}\n💰 RM {{total_price}}\n\n— {{business_name}}", true, "booking_confirmation"),
        new(orgId, "Appointment Reminder", "ALL", "Reminder: Your appointment is tomorrow — {{booking_ref}}", "Hi {{customer_name}},\n\nYour appointment is coming up tomorrow.\n\n📅 {{appointment_time}}\n📍 {{branch_name}}\n🔖 {{booking_ref}}\n\n— {{business_name}}", true, "appointment_reminder"),
        new(orgId, "Thank You / Post-Visit", "ALL", "Thank you for visiting {{business_name}}!", "Hi {{customer_name}},\n\nThank you for visiting us today!\n\n— {{business_name}}", true),
        new(orgId, "Outstanding Balance Reminder", "ALL", "Payment reminder for booking {{booking_ref}}", "Hi {{customer_name}},\n\nYour booking {{booking_ref}} has an outstanding balance of RM {{balance_due}}.\n\n— {{business_name}}", true),
        new(orgId, "Promotional Offer", "ALL", "Special offer just for you, {{customer_name}}! 🎉", "Hi {{customer_name}},\n\nWe have an exclusive offer for you!\n\n— {{business_name}}", false),
        new(orgId, "Pass Renewal Reminder", "ALL", "Your {{plan_name}} pass is due for renewal", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is due for renewal.\n\n💰 Renewal: RM {{plan_price}}\n\nTap below to renew and keep your credits active:\n{{renewal_link}}\n\n— {{business_name}}", true, "pass_renewal_reminder"),

        // ─── Community Subscription Templates ────────────────────
        new(orgId, "Community Welcome", "ALL", "Welcome to {{plan_name}}! 🎉", "Hi {{customer_name}},\n\nWelcome to {{plan_name}}!\n\nHere is your private group link:\n{{group_link}}\n\nWeekly session link:\n{{meeting_link}}\n\nSee you there! 🙏\n\n— {{business_name}}", true),
        new(orgId, "Community Payment Success", "ALL", "Payment Received: {{plan_name}}", "Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\n— {{business_name}}", true),
        new(orgId, "Community Payment Failed", "ALL", "Payment Failed: {{plan_name}}", "Hi {{customer_name}},\n\nWe were unable to process your renewal payment for {{plan_name}}.\n\nPlease complete your payment to avoid losing access to the community:\n{{renewal_link}}\n\n— {{business_name}}", true),
        new(orgId, "Community Renewal (3 Days)", "ALL", "Your {{plan_name}} subscription renews in 3 days", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is expiring in 3 days. To ensure you don't lose access to the community and weekly sessions, please renew your subscription here:\n{{renewal_link}}\n\n— {{business_name}}", true),
        new(orgId, "Community Renewal Due Today", "ALL", "Action Required: {{plan_name}} renewal due today", "Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n{{renewal_link}}\n\n— {{business_name}}", true),
        new(orgId, "Community Renewal Overdue", "ALL", "Final Notice: {{plan_name}} is overdue", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n{{renewal_link}}\n\n— {{business_name}}", true),
        new(orgId, "Community Subscription Cancelled", "ALL", "Your {{plan_name}} membership has ended", "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}", true),

        // ─── Abandoned Cart Recovery ─────────────────────────────
        new(orgId, "Abandoned Cart (12h)", "ALL", "Complete your purchase for {{item_name}}", "Hi {{customer_name}},\n\nWe noticed you didn't complete your purchase for {{item_name}}. Did you have trouble with the payment page?\n\nHere is a fresh link to complete your transaction:\n{{checkout_url}}\n\n— {{business_name}}", true),
        new(orgId, "Abandoned Cart (24h)", "ALL", "Don't miss out on {{item_name}}", "Hi {{customer_name}},\n\nSpots are filling up fast / Don't miss out on {{item_name}}! Grab yours here before it's gone:\n{{checkout_url}}\n\n— {{business_name}}", true)
    };
}
