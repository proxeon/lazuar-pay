using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure;

public class InMemoryEventBus : IEventBus, IEventBusSubscriptions
{
    private readonly ConcurrentDictionary<string, List<Type>> _handlers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
    {
        if (@event == null) return;

        // Use runtime type name instead of compile-time generic parameter (typeof(TEvent).Name)
        // This prevents static compiler dispatch issues when events are invoked through interface casts
        var eventName = @event.GetType().Name;
        if (!_handlers.TryGetValue(eventName, out var handlers))
        {
            _logger.LogInformation("Event {EventName} was published but has no registered handlers.", eventName);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        foreach (var handlerType in handlers)
        {
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
            
            // Retrieve the specific overload matching the exact concrete type of the event to prevent AmbiguousMatchException
            var method = handlerType.GetMethod("HandleAsync", new[] { @event.GetType() });
            if (method != null)
            {
                await (Task)method.Invoke(handler, [@event])!;
            }
            else
            {
                _logger.LogWarning("Handler {HandlerType} is registered for event {EventName} but does not expose a matching HandleAsync method.", handlerType.Name, eventName);
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
