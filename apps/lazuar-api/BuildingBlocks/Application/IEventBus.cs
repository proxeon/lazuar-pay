using MediatR;

namespace BuildingBlocks.Application;

public interface IIntegrationEvent : INotification
{
    Guid Id { get; }
    DateTime OccurredOn { get; }
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent;
}
