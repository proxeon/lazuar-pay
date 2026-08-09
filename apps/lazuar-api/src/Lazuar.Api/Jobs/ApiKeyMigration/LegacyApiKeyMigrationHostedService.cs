using Microsoft.Extensions.Options;

namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// One-shot hosted runner for <see cref="LegacyApiKeyMigrator"/>.
/// Registered only when <see cref="ApiKeyMigrationOptions.Enabled"/> is true.
/// Does not block host startup; dual-read middleware is untouched.
/// </summary>
public sealed class LegacyApiKeyMigrationHostedService : IHostedService, IDisposable
{
    private readonly LegacyApiKeyMigrator _migrator;
    private readonly IOptions<ApiKeyMigrationOptions> _options;
    private readonly ILogger<LegacyApiKeyMigrationHostedService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public LegacyApiKeyMigrationHostedService(
        LegacyApiKeyMigrator migrator,
        IOptions<ApiKeyMigrationOptions> options,
        ILogger<LegacyApiKeyMigrationHostedService> logger)
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
            _logger.LogInformation("API key migration hosted service registered but Enabled=false; skipping.");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "API key migration scheduled (DryRun={DryRun}, BatchSize={BatchSize}). Dual-read remains enabled.",
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
            _logger.LogWarning(ex, "API key migration stop waited on a faulted run.");
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
                "API key migration finished. DryRun={DryRun} Processed={Processed} Inserted={Inserted} WouldInsert={WouldInsert} AlreadyMigrated={AlreadyMigrated} HashCollisionDifferentOrg={HashCollisionDifferentOrg} Quarantined={Quarantined} IdRemapped={IdRemapped} PartialScopes={PartialScopes} InsertConflict={InsertConflict}",
                report.DryRun,
                report.Processed,
                report.Inserted,
                report.WouldInsert,
                report.AlreadyMigrated,
                report.HashCollisionDifferentOrg,
                report.Quarantined,
                report.IdRemapped,
                report.PartialScopes,
                report.InsertConflict);

            foreach (var outcome in report.Outcomes)
            {
                // Log source Id + code only — never plaintext keys or full hashes.
                if (outcome.Code is MigrationRowCodes.HashCollisionDifferentOrg
                    or MigrationRowCodes.QuarantineEmptyHash
                    or MigrationRowCodes.QuarantineOrphanOrg
                    or MigrationRowCodes.QuarantineUnknownScopesOnly
                    or MigrationRowCodes.InsertConflict)
                {
                    _logger.LogWarning(
                        "API key migration row SourceId={SourceId} Code={Code} TargetId={TargetId} IdRemapped={IdRemapped} Detail={Detail}",
                        outcome.SourceId,
                        outcome.Code,
                        outcome.TargetId,
                        outcome.IdRemapped,
                        outcome.Detail);
                }
                else if (outcome.IdRemapped
                    || (outcome.Detail is not null
                        && outcome.Detail.StartsWith("dropped_scopes:", StringComparison.Ordinal)))
                {
                    _logger.LogInformation(
                        "API key migration row SourceId={SourceId} Code={Code} TargetId={TargetId} IdRemapped={IdRemapped} Detail={Detail}",
                        outcome.SourceId,
                        outcome.Code,
                        outcome.TargetId,
                        outcome.IdRemapped,
                        outcome.Detail);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("API key migration cancelled.");
        }
        catch (Exception ex)
        {
            // Failure must leave dual-read valid — never throw unobserved into host.
            _logger.LogError(ex, "API key migration failed. Dual-read middleware is unchanged.");
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
