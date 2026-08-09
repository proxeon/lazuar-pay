using Microsoft.Extensions.Options;

namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// One-shot hosted runner for <see cref="LegacyWebhookSubscriptionMigrator"/> (R41).
/// Registered only when <see cref="WebhookSubscriptionMigrationOptions.Enabled"/> is true.
/// Does not block host startup. Delivery path is One durable dispatcher (R42/R43).
/// </summary>
public sealed class LegacyWebhookSubscriptionMigrationHostedService : IHostedService, IDisposable
{
    private readonly LegacyWebhookSubscriptionMigrator _migrator;
    private readonly IOptions<WebhookSubscriptionMigrationOptions> _options;
    private readonly ILogger<LegacyWebhookSubscriptionMigrationHostedService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public LegacyWebhookSubscriptionMigrationHostedService(
        LegacyWebhookSubscriptionMigrator migrator,
        IOptions<WebhookSubscriptionMigrationOptions> options,
        ILogger<LegacyWebhookSubscriptionMigrationHostedService> logger)
    {
        _migrator = migrator;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
        {
            _logger.LogInformation(
                "Webhook subscription migration hosted service registered but Enabled=false; skipping.");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Webhook subscription migration scheduled (DryRun={DryRun}, BatchSize={BatchSize}).",
            opts.DryRun,
            opts.BatchSize);

        _runTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        if (_runTask is null)
        {
            return;
        }

        try
        {
            await _runTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook subscription migration stop waited on a faulted run.");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Let EF module migrations finish before scanning.
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            var opts = _options.Value;
            var report = await _migrator.RunAsync(opts, cancellationToken);

            _logger.LogInformation(
                "Webhook subscription migration finished. DryRun={DryRun} Processed={Processed} Inserted={Inserted} WouldInsert={WouldInsert} AlreadyMigrated={AlreadyMigrated} Quarantined={Quarantined} InsertConflict={InsertConflict}",
                report.DryRun,
                report.Processed,
                report.Inserted,
                report.WouldInsert,
                report.AlreadyMigrated,
                report.Quarantined,
                report.InsertConflict);

            foreach (var outcome in report.Outcomes)
            {
                // Source Id + code only — never secrets or full URLs in warning paths if avoidable.
                // Detail may include validator message (no secrets); Url host is not logged.
                if (outcome.Code is MigrationRowCodes.QuarantineInvalidUrl
                    or MigrationRowCodes.QuarantineEmptySecret
                    or MigrationRowCodes.QuarantineOrphanOrg
                    or MigrationRowCodes.InsertConflict)
                {
                    _logger.LogWarning(
                        "Webhook subscription migration row SourceId={SourceId} Code={Code} TargetId={TargetId} Detail={Detail}",
                        outcome.SourceId,
                        outcome.Code,
                        outcome.TargetId,
                        outcome.Detail);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Webhook subscription migration cancelled.");
        }
        catch (Exception ex)
        {
            // Failure must leave Lhdn fire-and-forget valid — never throw unobserved into host.
            _logger.LogError(
                ex,
                "Webhook subscription migration failed. lhdn.WebhookSubscriptions table left unchanged.");
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
