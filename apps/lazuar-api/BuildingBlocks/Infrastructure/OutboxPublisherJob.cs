using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

public abstract class OutboxPublisherJob<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    protected OutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                var messages = await db.Set<OutboxMessage>()
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.OccurredOn)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                if (messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var eventType = Type.GetType(message.Type);
                            if (eventType == null)
                            {
                                throw new InvalidOperationException($"Type '{message.Type}' cannot be resolved.");
                            }

                            var integrationEvent = JsonSerializer.Deserialize(message.Data, eventType);
                            if (integrationEvent is IIntegrationEvent @event)
                            {
                                await eventBus.PublishAsync(@event);
                            }

                            message.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            message.Error = ex.ToString();
                            _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing outbox background worker.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
