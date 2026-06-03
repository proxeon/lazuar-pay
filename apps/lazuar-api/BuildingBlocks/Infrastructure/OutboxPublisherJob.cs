// apps/lazuar-api/BuildingBlocks/Infrastructure/OutboxPublisherJob.cs
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

                var entityType = db.Model.FindEntityType(typeof(OutboxMessage));
                var schema = entityType?.GetSchema() ?? "public";
                var tableName = entityType?.GetTableName() ?? "OutboxMessages";

                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

                var sql = $"""
                    SELECT * FROM "{schema}"."{tableName}"
                    WHERE "ProcessedAt" IS NULL
                    ORDER BY "OccurredOn"
                    LIMIT 20
                    FOR UPDATE SKIP LOCKED;
                """;

                var messages = await db.Set<OutboxMessage>()
                    .FromSqlRaw(sql)
                    .ToListAsync(stoppingToken);

                if (messages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var typeOfEvent = Type.GetType(message.Type);
                            if (typeOfEvent == null)
                            {
                                throw new InvalidOperationException($"Type '{message.Type}' cannot be resolved.");
                            }

                            var integrationEvent = JsonSerializer.Deserialize(message.Data, typeOfEvent);
                            
                            if (integrationEvent is IIntegrationEvent @event)
                            {
                                await eventBus.PublishAsync(@event);
                                message.ProcessedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                throw new InvalidOperationException($"Message {message.Id} is not a valid IIntegrationEvent. Domain Events should not be serialized to the Outbox.");
                            }
                        }
                        catch (Exception ex)
                        {
                            message.Error = ex.ToString();
                            _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
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
