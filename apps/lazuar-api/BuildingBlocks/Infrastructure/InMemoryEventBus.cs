using System.Collections.Concurrent;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure;

public class InMemoryEventBus : IEventBus, IEventBusSubscriptions
{
    private readonly ConcurrentDictionary<string, List<Type>> _handlers = new();
    private readonly IServiceProvider _serviceProvider;

    public InMemoryEventBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
    {
        if (@event == null) return;

        // Use runtime type name instead of compile-time generic parameter (typeof(TEvent).Name)
        // This prevents static compiler dispatch issues when events are invoked through interface casts
        var eventName = @event.GetType().Name;
        if (!_handlers.TryGetValue(eventName, out var handlers)) return;

        using var scope = _serviceProvider.CreateScope();
        foreach (var handlerType in handlers)
        {
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
            var method = handlerType.GetMethod("HandleAsync");
            if (method != null)
            {
                await (Task)method.Invoke(handler, [@event])!;
            }
        }
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        _handlers.AvoidDuplicateAdd(eventName, typeof(THandler));
    }
}

internal static class ConcurrentDictionaryExtensions
{
    public static void AvoidDuplicateAdd(this ConcurrentDictionary<string, List<Type>> dict, string key, Type val)
    {
        dict.AddOrUpdate(key, 
            _ => new List<Type> { val }, 
            (_, list) => { lock (list) { if (!list.Contains(val)) list.Add(val); } return list; });
    }
}
