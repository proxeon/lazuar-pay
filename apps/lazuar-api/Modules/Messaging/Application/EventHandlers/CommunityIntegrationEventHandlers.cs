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
    IIntegrationEventHandler<CommunityRenewalReminderDueIntegrationEvent>
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
        // 1. Fetch cross-module read model data
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        // 2. Execute Messaging Logic
        var subject = @event.IsFirstPayment ? "Welcome to the Community! 🎉" : "Subscription Renewed Successfully";
        var body = $"Hi {profile.FullName},\n\nYour community subscription is now active.";

        await _emailService.SendEmailAsync(profile.Email, subject, body);
    }

    public async Task HandleAsync(CommunitySubscriptionCancelledIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},\n\nYour subscription has been cancelled. You will retain access until the end of your billing cycle.";
        
        await _emailService.SendEmailAsync(profile.Email, "Subscription Cancelled", body);
    }

    public Task HandleAsync(CommunityCheckoutInitiatedIntegrationEvent @event)
    {
        // In the full implementation, this will insert an AutomationQueueEntity
        // into the messaging database with a 12-hour delay for Abandoned Cart recovery.
        return Task.CompletedTask;
    }

    public async Task HandleAsync(CommunityRenewalReminderDueIntegrationEvent @event)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(@event.ClientProfileId);
        if (profile == null || string.IsNullOrEmpty(profile.Email)) return;

        var body = $"Hi {profile.FullName},\n\nThis is a reminder that your community subscription is due for renewal soon.";

        await _emailService.SendEmailAsync(profile.Email, "Action Required: Renewal Due", body);
    }
}
