using BuildingBlocks.Application;
using Modules.Community.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Messaging.Application.EventHandlers;

/// <summary>
/// Listens to integration events from the Community module via the Inbox.
/// Executes the notification/messaging logic asynchronously.
/// </summary>
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

    public CommunityIntegrationEventHandlers(
        ICrmQueryService crmQueryService, 
        IEmailService emailService)
    {
        _crmQueryService = crmQueryService;
        _emailService = emailService;
    }

    public async Task HandleAsync(CommunitySubscriptionActivatedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var subject = @event.IsFirstPayment ? "Welcome to the Community! 🎉" : "Subscription Renewed Successfully";
        var body = $"Hi {profile.FullName},<br><br>Your community subscription is now active.";

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }

    public async Task HandleAsync(CommunitySubscriptionCancelledIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},<br><br>Your subscription has been cancelled. You will retain access until the end of your billing cycle.";
        
        await _emailService.SendEmailAsync(profile.Email, "Subscription Cancelled", body);
    }

    public Task HandleAsync(CommunityCheckoutInitiatedIntegrationEvent @event)
    {
        // Abandoned Cart logic
        return Task.CompletedTask;
    }

    public async Task HandleAsync(CommunityRenewalReminderDueIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},<br><br>This is a reminder that your community subscription is due for renewal soon.";

        await _emailService.SendEmailAsync(profile.Email, "Action Required: Renewal Due", body);
    }

    public async Task HandleAsync(CommunityMagicLinkRequestedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},<br><br>Click the link below to access your subscriber portal to manage or cancel your subscription. This link expires in 24 hours.<br><br><a href=\"{@event.MagicLinkUrl}\">Access Portal</a><br><br>— Lazuar Support";

        await _emailService.SendEmailAsync(profile.Email, "Your Subscriber Portal Access", body);
    }

    // ----------------------------------------------------------------------
    // Handle the one-off manual reminder requested by the Admin
    // ----------------------------------------------------------------------
    public async Task HandleAsync(CommunityOneOffReminderRequestedIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        string subject = "Important Update Regarding Your Subscription";
        string body;

        // If the admin typed a custom message in the UI, use it directly
        if (!string.IsNullOrWhiteSpace(@event.CustomMessage))
        {
            body = $"Hi {profile.FullName},<br><br>{@event.CustomMessage}";
        }
        // If the admin selected a saved template, we use the ID 
        // (In Phase 5, we will fetch the TemplateEntity from the DB here to render it)
        else if (@event.TemplateId.HasValue)
        {
            body = $"Hi {profile.FullName},<br><br>This is a notification regarding your community subscription.";
        }
        else
        {
            return; // Nothing to send
        }

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }
}
