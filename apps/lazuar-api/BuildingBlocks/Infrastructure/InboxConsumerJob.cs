using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

public abstract class InboxConsumerJob<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly DatabaseJobTrigger _jobTrigger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    protected InboxConsumerJob(IServiceScopeFactory scopeFactory, ILogger logger, DatabaseJobTrigger jobTrigger)
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
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var entityType = db.Model.FindEntityType(typeof(InboxMessage));
                var schema = entityType?.GetSchema() ?? "public";
                var tableName = entityType?.GetTableName() ?? "InboxMessages";

                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

                var sql = $"""
                    SELECT * FROM "{schema}"."{tableName}"
                    WHERE "ProcessedAt" IS NULL
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
                    ORDER BY "ReceivedAt"
                    LIMIT 20
                    FOR UPDATE SKIP LOCKED;
                """;

                var messages = await db.Set<InboxMessage>()
                    .FromSqlRaw(sql)
                    .ToListAsync(stoppingToken);

                messagesProcessed = messages.Count;

                if (messagesProcessed > 0)
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var eventType = TypeResolver.Resolve(message.Type);
                            if (eventType == null) throw new InvalidOperationException($"Type '{message.Type}' cannot be resolved by the TypeResolver.");

                            var inboxEvent = JsonSerializer.Deserialize(message.Data, eventType);
                            if (inboxEvent is INotification notification)
                            {
                                await mediator.Publish(notification, stoppingToken);
                            }

                            MessageProcessingResultApplier.ApplySuccess(message, DateTime.UtcNow);
                        }
                        catch (Exception ex)
                        {
                            MessageProcessingResultApplier.ApplyFailure(message, ex, DateTime.UtcNow);
                            _logger.LogError(ex, "Failed to process inbox message {Id} (attempt {Attempt})", message.Id, message.AttemptCount);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing inbox background worker.");
            }

            if (messagesProcessed > 0)
            {
                await Task.Yield();
                continue;
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(_pollInterval);
                await _jobTrigger.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }
}
