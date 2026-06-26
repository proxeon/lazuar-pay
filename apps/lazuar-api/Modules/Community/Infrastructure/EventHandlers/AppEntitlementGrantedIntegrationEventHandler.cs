using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.One.Contracts;
using Modules.Community.Domain.Entities;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.EventHandlers;

public class AppEntitlementGrantedIntegrationEventHandler : IIntegrationEventHandler<AppEntitlementGrantedIntegrationEvent>
{
    private readonly CommunityDbContext _dbContext;

    public AppEntitlementGrantedIntegrationEventHandler(CommunityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(AppEntitlementGrantedIntegrationEvent @event)
    {
        if (@event.AppId != "COMMUNITY") return;

        var hasTemplates = await _dbContext.MessageTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == @event.TenantId);

        if (!hasTemplates)
        {
            var templates = new List<MessageTemplate>
            {
                new MessageTemplate(@event.TenantId, "Community Welcome", "ALL", 
                    "You're in! Welcome to {{plan_name}} 🎉", 
                    "Hi {{customer_name}},\n\nYour payment of RM {{total_price}} was successful, and your access is officially active. We are thrilled to have you here.\n\nHere is everything you need to get started:\n\n1. **Join the Community:** Meet everyone and say hi!\n[Join the Telegram Group]({{group_link}})\n\n2. **Weekly Sessions:** Bookmark our live room.\n[Save the Zoom Link]({{meeting_link}})\n\nYou can access your resources anytime via your private dashboard:\n[Go to my Dashboard]({{portal_magic_link}})\n\nSee you inside,\n— {{business_name}}", 
                    "Hey {{customer_name}}! 🎉 Welcome to {{plan_name}}! Your payment is confirmed. Click here to join the private group right now: {{group_link}}. See you inside! 🚀", 
                    true, new[] { "{{group_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}", "{{portal_magic_link}}", "{{total_price}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Payment Success", "ALL", 
                    "Payment Received: {{plan_name}}", 
                    "Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\nYou can manage your subscription at any time via your portal:\n[Access Portal]({{portal_magic_link}})\n\n— {{business_name}}", 
                    "Hi {{customer_name}}, your payment of RM {{total_price}} for {{plan_name}} is confirmed! ✅ Manage your access here: {{portal_magic_link}}", 
                    true, new[] { "{{total_price}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{portal_magic_link}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Payment Failed", "ALL", 
                    "Action Needed: Payment issue for {{plan_name}}", 
                    "Hi {{customer_name}},\n\nWe tried to process your renewal for {{plan_name}}, but the payment didn't go through. This usually just means your bank blocked the transaction or the card expired.\n\nTo ensure you don't lose access to the community and upcoming sessions, please update your payment details here:\n\n[Securely Update Payment]({{renewal_link}})\n\nIf you need any help, just reply to this email.\n\n— {{business_name}}", 
                    "Hi {{customer_name}} 👋 Quick heads up: your recent card payment for {{plan_name}} was declined by the bank. To keep your access active, you can quickly update your details here: {{renewal_link}}. Let us know if you need help!", 
                    true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Renewal (3 Days)", "ALL", 
                    "Upcoming renewal for {{plan_name}}", 
                    "Hi {{customer_name}},\n\nWe hope you're getting great value out of the community! This is just a quick reminder that your {{plan_name}} subscription will automatically renew in a few days.\n\nIf you need to update your card, download invoices, or manage your account, you can access your dashboard below:\n\n[Manage Account]({{renewal_link}})\n\n— {{business_name}}", 
                    "Hey {{customer_name}}, hope you're doing great! 🌟 Just a quick reminder that your {{plan_name}} cycle renews in 3 days. No action needed if you're staying with us, but you can manage your account anytime here: {{renewal_link}}", 
                    true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Renewal Due Today", "ALL", 
                    "Action Required: {{plan_name}} renewal due today", 
                    "Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n\n[Renew Subscription]({{renewal_link}})\n\n— {{business_name}}", 
                    "Hi {{customer_name}}! ⏳ Your {{plan_name}} membership is due for renewal today. Secure your access here: {{renewal_link}}", 
                    true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Renewal Overdue", "ALL", 
                    "Final Notice: {{plan_name}} is overdue", 
                    "Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n\n[Renew Now]({{renewal_link}})\n\n— {{business_name}}", 
                    "Hey {{customer_name}}, your {{plan_name}} membership is past due and access will be suspended soon. ⚠️ You can resolve this quickly here: {{renewal_link}}", 
                    true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Community Subscription Cancelled", "ALL", 
                    "Your {{plan_name}} membership has ended", 
                    "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}", 
                    "Hi {{customer_name}}, your {{plan_name}} membership has been cancelled. You have access until {{current_period_end}}. We hope to see you back soon! 🙏", 
                    true, Array.Empty<string>(), new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{current_period_end}}" }),
                    
                new MessageTemplate(@event.TenantId, "Abandoned Cart (12h)", "WHATSAPP", 
                    "Complete your purchase for {{item_name}}", 
                    "", 
                    "Hey {{customer_name}}! We noticed you left {{item_name}} in your cart. Did you have any trouble with the payment page? You can finish your checkout securely here: {{checkout_url}} ⚡️", 
                    true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" }),
                    
                new MessageTemplate(@event.TenantId, "Abandoned Cart (24h)", "EMAIL", 
                    "Did you run into an issue?", 
                    "Hi {{customer_name}},\n\nWe noticed you started checking out for {{item_name}} but didn't finish.\n\nIf you had any technical issues, just reply to this email and we'll help you out. Otherwise, your spot is still reserved! You can complete your registration right here:\n\n[Complete my registration]({{checkout_url}})\n\nHope to see you inside.\n\n— {{business_name}}", 
                    "", 
                    true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" })
            };

            _dbContext.MessageTemplates.AddRange(templates);
            await _dbContext.SaveChangesAsync();
        }

        var hasSchedules = await _dbContext.ReminderSchedules
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == @event.TenantId);

        if (!hasSchedules)
        {
            var preDunningTemplate = await _dbContext.MessageTemplates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.OrganizationId == @event.TenantId && t.Name == "Community Renewal (3 Days)");
                
            var dayOfDunningTemplate = await _dbContext.MessageTemplates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.OrganizationId == @event.TenantId && t.Name == "Community Renewal Due Today");
                
            var postDunningTemplate = await _dbContext.MessageTemplates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.OrganizationId == @event.TenantId && t.Name == "Community Renewal Overdue");

            if (preDunningTemplate != null && dayOfDunningTemplate != null && postDunningTemplate != null)
            {
                var defaultSchedules = new List<CommunityReminderSchedule>
                {
                    new CommunityReminderSchedule(@event.TenantId, null, preDunningTemplate.Id, "ALL", -3, "08:00", true),
                    new CommunityReminderSchedule(@event.TenantId, null, dayOfDunningTemplate.Id, "ALL", 0, "08:00", true),
                    new CommunityReminderSchedule(@event.TenantId, null, postDunningTemplate.Id, "ALL", 3, "08:00", true)
                };

                _dbContext.ReminderSchedules.AddRange(defaultSchedules);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
