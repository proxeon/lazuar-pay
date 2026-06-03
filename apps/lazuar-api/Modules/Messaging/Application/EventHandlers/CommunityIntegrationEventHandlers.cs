using BuildingBlocks.Application;
using Modules.Community.Contracts;
using Modules.CRM.Contracts;
using Modules.Messaging.Contracts;

namespace Modules.Messaging.Application.EventHandlers;

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

    public CommunityIntegrationEventHandlers(
        ICrmQueryService crmQueryService, 
        IEmailService emailService,
        IMessageTemplateQueryService templateService)
    {
        _crmQueryService = crmQueryService;
        _emailService = emailService;
        _templateService = templateService;
    }

    // Accepts a dynamic dictionary and replaces all placeholders
    private string RenderTemplate(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return "";
        
        var result = template;
        foreach (var (key, value) in variables)
        {
            var placeholder = $"{{{{{key}}}}}"; // e.g. {{customer_name}}
            result = result.Replace(placeholder, value ?? "", StringComparison.OrdinalIgnoreCase);
        }
        
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
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

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
            body = $"Hi {profile.FullName},<br><br>{@event.CustomMessage}";
        }
        else if (@event.TemplateId.HasValue)
        {
            var templates = await _templateService.GetTemplatesAsync(new[] { @event.TemplateId.Value });
            var template = templates.FirstOrDefault();

            var variables = new Dictionary<string, string>
            {
                ["customer_name"] = profile.FullName,
                ["business_name"] = "Our Community"
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
