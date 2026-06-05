namespace BuildingBlocks.Application;

public interface IEventBusSubscriptions
{
    void Subscribe<TEvent, THandler>() 
        where TEvent : IIntegrationEvent 
        where THandler : IIntegrationEventHandler<TEvent>;
}
