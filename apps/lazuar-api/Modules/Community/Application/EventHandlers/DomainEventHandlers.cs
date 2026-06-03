using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Contracts;
using Modules.Community.Domain.Events;

namespace Modules.Community.Application.EventHandlers;

public class DomainEventHandlers : 
    INotificationHandler<SubscriptionActivatedDomainEvent>,
    INotificationHandler<SubscriptionCancelledDomainEvent>,
    INotificationHandler<CheckoutInitiatedDomainEvent>
{
    private readonly IEventBus _eventBus;

    public DomainEventHandlers(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new CommunitySubscriptionActivatedIntegrationEvent(
            notification.OrganizationId,
            notification.SubscriptionId,
            notification.ClientProfileId,
            notification.IsFirstPayment);

        await _eventBus.PublishAsync(integrationEvent);
    }

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new CommunitySubscriptionCancelledIntegrationEvent(
            notification.OrganizationId,
            notification.SubscriptionId,
            notification.ClientProfileId);

        await _eventBus.PublishAsync(integrationEvent);
    }

    public async Task Handle(CheckoutInitiatedDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new CommunityCheckoutInitiatedIntegrationEvent(
            notification.OrganizationId,
            notification.SubscriptionId,
            notification.ClientProfileId);

        await _eventBus.PublishAsync(integrationEvent);
    }
}
