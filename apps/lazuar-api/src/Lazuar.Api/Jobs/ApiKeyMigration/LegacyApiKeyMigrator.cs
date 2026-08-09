using Modules.One.Domain;

namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// Pure orchestration: copy <c>lhdn.DeveloperApiKeys</c> → <c>one.ApiCredentials</c>.
/// Idempotent on <c>KeyHash</c>; dual-read remains valid throughout.
/// </summary>
public sealed class LegacyApiKeyMigrator
{
    private readonly IApiKeyMigrationStore _store;

    public LegacyApiKeyMigrator(IApiKeyMigrationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ApiKeyMigrationReport> RunAsync(
        ApiKeyMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var batchSize = options.BatchSize > 0 ? options.BatchSize : 500;
        var report = new ApiKeyMigrationReport { DryRun = options.DryRun };
        Guid? afterId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _store.GetLegacyBatchAsync(afterId, batchSize, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var row in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = await ProcessRowAsync(row, options.DryRun, cancellationToken);
                report.Add(outcome);
            }

            afterId = batch[^1].Id;
            if (batch.Count < batchSize)
            {
                break;
            }
        }

        return report;
    }

    internal async Task<MigrationRowOutcome> ProcessRowAsync(
        LegacyDeveloperApiKeyRow row,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.KeyHash))
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineEmptyHash,
                Detail: "empty_or_blank_keyhash");
        }

        var keyHash = row.KeyHash.Trim();
        var existingByHash = await _store.FindByKeyHashAsync(keyHash, cancellationToken);
        if (existingByHash is not null)
        {
            if (existingByHash.OrganizationId != row.OrganizationId)
            {
                return new MigrationRowOutcome(
                    row.Id,
                    MigrationRowCodes.HashCollisionDifferentOrg,
                    TargetId: existingByHash.Id,
                    Detail: "keyhash_exists_on_one_for_different_org");
            }

            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.AlreadyMigrated,
                TargetId: existingByHash.Id);
        }

        if (!await _store.OrganizationExistsAsync(row.OrganizationId, cancellationToken))
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineOrphanOrg,
                Detail: "organization_missing_on_one");
        }

        var scopesResult = NormalizeScopes(row.Scopes);
        if (scopesResult.Quarantine)
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineUnknownScopesOnly,
                Detail: scopesResult.Detail);
        }

        var targetId = row.Id;
        var idRemapped = false;
        var existingById = await _store.FindByIdAsync(row.Id, cancellationToken);
        if (existingById is not null
            && !string.Equals(existingById.KeyHash, keyHash, StringComparison.Ordinal))
        {
            targetId = Guid.CreateVersion7();
            idRemapped = true;
        }

        var insert = new MigratedApiCredentialInsert
        {
            Id = targetId,
            OrganizationId = row.OrganizationId,
            Name = row.Name ?? string.Empty,
            Prefix = row.Prefix ?? string.Empty,
            KeyHash = keyHash,
            KeyHint = CoalesceHint(row.KeyHint),
            Scopes = scopesResult.Scopes,
            IsActive = row.IsActive,
            CreatedAt = row.CreatedAt
        };

        if (dryRun)
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.WouldInsert,
                TargetId: targetId,
                IdRemapped: idRemapped,
                Detail: scopesResult.Detail);
        }

        var inserted = await _store.TryInsertAsync(insert, cancellationToken);
        if (!inserted)
        {
            // Race: another process inserted same KeyHash between probe and insert.
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.InsertConflict,
                TargetId: targetId,
                IdRemapped: idRemapped,
                Detail: "on_conflict_keyhash_do_nothing");
        }

        return new MigrationRowOutcome(
            row.Id,
            MigrationRowCodes.Inserted,
            TargetId: targetId,
            IdRemapped: idRemapped,
            Detail: scopesResult.Detail);
    }

    private static string CoalesceHint(string? keyHint)
    {
        if (string.IsNullOrWhiteSpace(keyHint))
        {
            return "****";
        }

        return keyHint.Trim();
    }

    private static (string Scopes, bool Quarantine, string? Detail) NormalizeScopes(string? raw)
    {
        var tokens = PlatformApiScopes.Split(raw);
        if (tokens.Length == 0)
        {
            return (string.Empty, Quarantine: true, Detail: "empty_scopes");
        }

        var known = new List<string>(tokens.Length);
        var unknown = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            if (!seen.Add(token))
            {
                continue;
            }

            if (PlatformApiScopes.IsKnownScope(token))
            {
                known.Add(token);
            }
            else
            {
                unknown.Add(token);
            }
        }

        if (known.Count == 0)
        {
            return (
                string.Empty,
                Quarantine: true,
                Detail: "unknown_scopes_only:" + string.Join(" ", unknown));
        }

        string? detail = null;
        if (unknown.Count > 0)
        {
            // Never invent scopes; keep known subset and record dropped tokens (not secrets).
            detail = "dropped_scopes:" + string.Join(" ", unknown);
        }

        return (string.Join(" ", known), Quarantine: false, Detail: detail);
    }
}
