using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Contracts;
using Modules.Community.Domain.Events;

namespace Modules.Community.Application.EventHandlers;

public class DomainEventHandlers : 
    INotificationHandler<SubscriptionActivatedDomainEvent>,
    INotificationHandler<SubscriptionCancelledDomainEvent>,
    INotificationHandler<CheckoutInitiatedDomainEvent>,
    INotificationHandler<MagicLinkRequestedDomainEvent>,
    INotificationHandler<OneOffReminderRequestedDomainEvent>,
    INotificationHandler<SubscriptionRenewalDueDomainEvent>
{
    private readonly IEventBus _eventBus;

    public DomainEventHandlers(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunitySubscriptionActivatedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.IsFirstPayment
            )
        );
    }

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunitySubscriptionCancelledIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId
            )
        );
    }

    public async Task Handle(CheckoutInitiatedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityCheckoutInitiatedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId
            )
        );
    }

    public async Task Handle(MagicLinkRequestedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityMagicLinkRequestedIntegrationEvent(
                notification.OrganizationId,
                notification.ClientProfileId,
                notification.MagicLinkUrl
            )
        );
    }

    public async Task Handle(OneOffReminderRequestedDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityOneOffReminderRequestedIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.TemplateId,
                notification.CustomMessage,
                notification.Channel
            )
        );
    }

    // -------------------------------------------------------------------------
    // Handle the automated schedule reminder from the background job
    // -------------------------------------------------------------------------
    public async Task Handle(SubscriptionRenewalDueDomainEvent notification, CancellationToken ct)
    {
        await _eventBus.PublishAsync(
            new CommunityRenewalReminderDueIntegrationEvent(
                notification.OrganizationId,
                notification.SubscriptionId,
                notification.ClientProfileId,
                notification.TemplateId,
                notification.Channel
            )
        );
    }
}
