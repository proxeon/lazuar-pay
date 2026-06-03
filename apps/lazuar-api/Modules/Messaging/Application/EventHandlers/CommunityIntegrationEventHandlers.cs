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

    private string RenderTemplate(string template, ClientProfileDto profile)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return template
            .Replace("{{customer_name}}", profile.FullName)
            .Replace("{{business_name}}", "Our Community"); // Can be expanded later
    }

    public async Task HandleAsync(CommunitySubscriptionActivatedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var templateName = @event.IsFirstPayment ? "Community Welcome" : "Community Payment Success";
        var template = await _templateService.GetTemplateByNameAsync(@event.OrganizationId, templateName);

        var subject = template != null ? RenderTemplate(template.Subject, profile) : "Subscription Active";
        var body = template != null ? RenderTemplate(template.Body, profile) : $"Hi {profile.FullName}, your subscription is now active.";

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }

    public async Task HandleAsync(CommunitySubscriptionCancelledIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var template = await _templateService.GetTemplateByNameAsync(@event.OrganizationId, "Community Subscription Cancelled");

        var subject = template != null ? RenderTemplate(template.Subject, profile) : "Subscription Cancelled";
        var body = template != null ? RenderTemplate(template.Body, profile) : $"Hi {profile.FullName}, your subscription has been cancelled.";
        
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

        var subject = template != null ? RenderTemplate(template.Subject, profile) : "Renewal Reminder";
        var body = template != null ? RenderTemplate(template.Body, profile) : $"Hi {profile.FullName}, your subscription is due soon.";

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

            subject = template != null ? RenderTemplate(template.Subject, profile) : subject;
            body = template != null ? RenderTemplate(template.Body, profile) : $"Hi {profile.FullName}, this is a notification regarding your subscription.";
        }
        else
        {
            return;
        }

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }
}
