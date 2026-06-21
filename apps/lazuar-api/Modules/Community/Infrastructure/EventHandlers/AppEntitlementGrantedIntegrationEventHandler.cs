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
                new MessageTemplate(@event.TenantId, "Community Welcome", "ALL", "Welcome to {{plan_name}}! 🎉", "Hi {{customer_name}},\n\nWelcome to {{plan_name}}!\n\nHere is your private group link:\n{{group_link}}\n\nWeekly session link:\n{{meeting_link}}\n\nSee you there! 🙏\n\n— {{business_name}}", true, new[] { "{{group_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}" }),
                new MessageTemplate(@event.TenantId, "Community Payment Success", "ALL", "Payment Received: {{plan_name}}", "Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\n— {{business_name}}", true, new[] { "{{total_price}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                new MessageTemplate(@event.TenantId, "Community Payment Failed", "ALL", "Payment Failed: {{plan_name}}", "Hi {{customer_name}},\n\nWe were unable to process your renewal payment for {{plan_name}}.\n\nPlease complete your payment to avoid losing access to the community:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                new MessageTemplate(@event.TenantId, "Community Renewal (3 Days)", "ALL", "Your {{plan_name}} subscription renews in 3 days", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is expiring in 3 days. To ensure you don't lose access to the community and weekly sessions, please renew your subscription here:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                new MessageTemplate(@event.TenantId, "Community Renewal Due Today", "ALL", "Action Required: {{plan_name}} renewal due today", "Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                new MessageTemplate(@event.TenantId, "Community Renewal Overdue", "ALL", "Final Notice: {{plan_name}} is overdue", "Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n{{renewal_link}}\n\n— {{business_name}}", true, new[] { "{{renewal_link}}" }, new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}" }),
                new MessageTemplate(@event.TenantId, "Community Subscription Cancelled", "ALL", "Your {{plan_name}} membership has ended", "Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}", true, Array.Empty<string>(), new[] { "{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{current_period_end}}" }),
                new MessageTemplate(@event.TenantId, "Abandoned Cart (12h)", "WHATSAPP", "Complete your purchase for {{item_name}}", "Hi {{customer_name}},\n\nWe noticed you didn't complete your purchase for {{item_name}}. Did you have trouble with the payment page?\n\nHere is a fresh link to complete your transaction:\n{{checkout_url}}\n\n— {{business_name}}", true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" }),
                new MessageTemplate(@event.TenantId, "Abandoned Cart (24h)", "EMAIL", "Don't miss out on {{item_name}}", "Hi {{customer_name}},\n\nSpots are filling up fast! Grab yours here before it's gone:\n{{checkout_url}}\n\n— {{business_name}}", true, new[] { "{{checkout_url}}" }, new[] { "{{customer_name}}", "{{item_name}}", "{{business_name}}" })
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
