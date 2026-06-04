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
    private readonly DatabaseJobTrigger _jobTrigger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    protected OutboxPublisherJob(IServiceScopeFactory scopeFactory, ILogger logger, DatabaseJobTrigger jobTrigger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _jobTrigger = jobTrigger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int messagesProcessed = 0;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                
                // Resolve the singleton InMemoryEventBus specifically to execute the actual local dispatching
                var eventBus = scope.ServiceProvider.GetRequiredService<InMemoryEventBus>();

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

                messagesProcessed = messages.Count;

                if (messagesProcessed > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var typeOfEvent = TypeResolver.Resolve(message.Type);
                            if (typeOfEvent == null) throw new InvalidOperationException($"Type '{message.Type}' cannot be resolved by the TypeResolver.");

                            var integrationEvent = JsonSerializer.Deserialize(message.Data, typeOfEvent);
                            if (integrationEvent is IIntegrationEvent @event)
                            {
                                await eventBus.PublishAsync(@event);
                                message.ProcessedAt = DateTime.UtcNow;
                            }
                            else
                            {
                                throw new InvalidOperationException($"Message {message.Id} is not a valid IIntegrationEvent.");
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

            // Phase 2 Optimization: If we processed a full batch (20), there might be more right now. Loop instantly!
            if (messagesProcessed == 20)
            {
                await Task.Yield();
                continue;
            }

            // Otherwise, wait until the DbContext triggers us, or fallback after 5 seconds
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(_pollInterval);
                await _jobTrigger.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) { /* Timeout reached naturally or stopped */ }
        }
    }
}
