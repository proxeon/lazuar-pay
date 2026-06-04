using System.Text.Json;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure;

public sealed class OutboxEventBus<TDbContext> : IEventBus where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    public OutboxEventBus(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IIntegrationEvent
    {
        var outboxMessage = new OutboxMessage
        {
            Id = @event.Id,
            Type = @event.GetType().AssemblyQualifiedName ?? @event.GetType().FullName!,
            Data = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredOn = @event.OccurredOn
        };

        await _dbContext.Set<OutboxMessage>().AddAsync(outboxMessage);
    }
}
