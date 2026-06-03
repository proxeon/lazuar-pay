// apps/lazuar-api/BuildingBlocks/Infrastructure/InboxConsumerJob.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace BuildingBlocks.Infrastructure;

public abstract class InboxConsumerJob<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    protected InboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger logger)
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
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // 1. Resolve table metadata dynamically from the active DbContext EF model
                var entityType = db.Model.FindEntityType(typeof(InboxMessage));
                var schema = entityType?.GetSchema() ?? "public";
                var tableName = entityType?.GetTableName() ?? "InboxMessages";

                // 2. Open an explicit transaction
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

                // 3. Query PostgreSQL with pessimistic locking (FOR UPDATE SKIP LOCKED)
                var sql = $"""
                    SELECT * FROM "{schema}"."{tableName}"
                    WHERE "ProcessedAt" IS NULL
                    ORDER BY "ReceivedAt"
                    LIMIT 20
                    FOR UPDATE SKIP LOCKED;
                """;

                var messages = await db.Set<InboxMessage>()
                    .FromSqlRaw(sql)
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

                            var inboxEvent = JsonSerializer.Deserialize(message.Data, eventType);
                            if (inboxEvent is INotification notification)
                            {
                                await mediator.Publish(notification, stoppingToken);
                            }

                            message.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            message.Error = ex.ToString();
                            _logger.LogError(ex, "Failed to process inbox message {Id}", message.Id);
                        }
                    }

                    // 4. Save modifications and commit transaction
                    await db.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing inbox background worker.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
