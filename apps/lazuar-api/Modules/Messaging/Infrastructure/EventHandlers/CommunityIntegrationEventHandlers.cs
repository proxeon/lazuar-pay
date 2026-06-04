using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using Modules.Community.Contracts;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Infrastructure.EventHandlers;

public class CommunityIntegrationEventHandlers : 
    IIntegrationEventHandler<CommunitySubscriptionActivatedIntegrationEvent>,
    IIntegrationEventHandler<CommunitySubscriptionCancelledIntegrationEvent>,
    IIntegrationEventHandler<CommunityCheckoutInitiatedIntegrationEvent>,
    IIntegrationEventHandler<CommunityRenewalReminderDueIntegrationEvent>,
    IIntegrationEventHandler<CommunityMagicLinkRequestedIntegrationEvent>,
    IIntegrationEventHandler<CommunityOneOffReminderRequestedIntegrationEvent>
{
    private readonly ICrmQueryService _crmQueryService;
    private readonly IEmailService _emailService;
    private readonly IMessageTemplateQueryService _templateService;
    private readonly MessagingDbContext _context;

    public CommunityIntegrationEventHandlers(
        ICrmQueryService crmQueryService, 
        IEmailService emailService,
        IMessageTemplateQueryService templateService,
        MessagingDbContext context)
    {
        _crmQueryService = crmQueryService;
        _emailService = emailService;
        _templateService = templateService;
        _context = context;
    }

    private string RenderTemplate(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return "";
        
        var result = template;
        foreach (var (key, value) in variables)
        {
            var placeholder = $"{{{{{key}}}}}";
            result = result.Replace(placeholder, value ?? "", StringComparison.OrdinalIgnoreCase);
        }
        
        // Convert literal "\n" strings (from DB seeds) AND standard linebreaks to HTML <br>
        result = result.Replace("\\n", "<br>").Replace("\n", "<br>");
        
        return result;
    }

    public async Task HandleAsync(CommunitySubscriptionActivatedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var templateName = @event.IsFirstPayment ? "Community Welcome" : "Community Payment Success";
        var template = await _templateService.GetTemplateByNameAsync(@event.OrganizationId, templateName);

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.FullName,
            ["business_name"] = "Our Community",
            ["plan_name"] = @event.PlanName,
            ["group_link"] = @event.GroupLink,
            ["meeting_link"] = @event.MeetingLink,
            ["total_price"] = @event.AmountPaid.ToString("F2")
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Active";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, your subscription to {@event.PlanName} is now active.";

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }

    public async Task HandleAsync(CommunitySubscriptionCancelledIntegrationEvent @event)
    {
        // 1. Send cancellation confirmation email to the subscriber
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile != null && !string.IsNullOrEmpty(profile.Email))
        {
            var template = await _templateService.GetTemplateByNameAsync(@event.OrganizationId, "Community Subscription Cancelled");

            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.FullName,
                ["business_name"] = "Our Community",
                ["plan_name"] = @event.PlanName,
                ["current_period_end"] = @event.CurrentPeriodEnd.HasValue 
                    ? @event.CurrentPeriodEnd.Value.ToString("dd MMM yyyy") 
                    : "the end of your billing cycle"
            };

            var subject = template != null ? RenderTemplate(template.Subject, variables) : "Subscription Cancelled";
            var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, your subscription to {@event.PlanName} has been cancelled.";
            
            await _emailService.SendEmailAsync(profile.Email, subject, body);
        }

        // 2. Clean up local Messaging queue using Entity Framework Context (Clean Architecture compliant)
        var pendingReminders = await _context.AutomationRules
            .IgnoreQueryFilters()
            .Join(_context.Set<Modules.Messaging.Domain.AutomationQueue>().IgnoreQueryFilters(),
                r => r.Id,
                q => q.AutomationRuleId,
                (r, q) => new { Rule = r, Queue = q })
            .Where(x => x.Rule.OrganizationId == @event.OrganizationId 
                     && x.Queue.BookingId == @event.SubscriptionId 
                     && x.Queue.Status == "PENDING"
                     && x.Rule.TriggerType == "COMMUNITY_ABANDONED")
            .Select(x => x.Queue)
            .ToListAsync();

        if (pendingReminders.Any())
        {
            foreach (var item in pendingReminders)
            {
                item.Status = "CANCELLED";
                item.ProcessedAt = DateTime.UtcNow;
                item.LastError = "Subscription was cancelled or checkout expired.";
            }

            await _context.SaveChangesAsync();
        }
    }

    public Task HandleAsync(CommunityCheckoutInitiatedIntegrationEvent @event)
    {
        return Task.CompletedTask;
    }

    public async Task HandleAsync(CommunityRenewalReminderDueIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var templates = await _templateService.GetTemplatesAsync(new[] { @event.TemplateId });
        var template = templates.FirstOrDefault();

        var variables = new Dictionary<string, string>
        {
            ["customer_name"] = profile.FullName,
            ["business_name"] = "Our Community",
            ["plan_name"] = @event.PlanName,
            ["renewal_link"] = @event.RenewalLink
        };

        var subject = template != null ? RenderTemplate(template.Subject, variables) : "Renewal Reminder";
        var body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, your subscription to {@event.PlanName} is due soon. Renew here: {@event.RenewalLink}";

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }

    public async Task HandleAsync(CommunityMagicLinkRequestedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},<br><br>Click the link below to access your subscriber portal to manage or cancel your subscription. This link expires in 24 hours.<br><br><a href=\"{@event.MagicLinkUrl}\">Access Portal</a><br><br>— Lazuar Support";

        await _emailService.SendEmailAsync(profile.Email, "Your Subscriber Portal Access", body);
    }

    public async Task HandleAsync(CommunityOneOffReminderRequestedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        string subject = "Important Update Regarding Your Subscription";
        string body = "";

        if (!string.IsNullOrWhiteSpace(@event.CustomMessage))
        {
            // Custom messages automatically convert \n to <br> now!
            body = $"Hi {profile.FullName},<br><br>{@event.CustomMessage}";
        }
        else if (@event.TemplateId.HasValue)
        {
            var templates = await _templateService.GetTemplatesAsync(new[] { @event.TemplateId.Value });
            var template = templates.FirstOrDefault();

            // Populate the rich variables dictionary for manual reminders!
            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.FullName,
                ["business_name"] = "Our Community",
                ["plan_name"] = @event.PlanName,
                ["total_price"] = @event.PlanPrice.ToString("F2"),
                ["renewal_link"] = @event.RenewalLink
            };

            subject = template != null ? RenderTemplate(template.Subject, variables) : subject;
            body = template != null ? RenderTemplate(template.Body, variables) : $"Hi {profile.FullName}, this is a notification regarding your subscription.";
        }
        else
        {
            return;
        }

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }
}
