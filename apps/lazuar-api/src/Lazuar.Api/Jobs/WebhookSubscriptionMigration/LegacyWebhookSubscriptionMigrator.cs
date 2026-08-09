using System.Text.Json;
using Modules.One.Domain;

namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// Pure orchestration: copy active <c>lhdn.WebhookSubscriptions</c> → <c>one.TenantWebhookEndpoints</c>.
/// Idempotent on OrganizationId + Url. Preserves Lhdn <c>Secret</c> as One <c>SecretKey</c> (no remint).
/// Sets <c>EnabledEvents</c> to invoice.* only (R40 / R41) so e-invoice receivers do not get commerce events.
/// </summary>
public sealed class LegacyWebhookSubscriptionMigrator
{
    /// <summary>R40 lock: migrated LHDN-only URLs accept invoice lifecycle events only.</summary>
    public static readonly IReadOnlyList<string> LhdnInvoiceEnabledEvents =
    [
        "invoice.valid",
        "invoice.invalid"
    ];

    private static readonly JsonSerializerOptions EnabledEventsJsonOptions = new()
    {
        // Match EF jsonb shape: plain JSON string array.
        WriteIndented = false
    };

    private readonly IWebhookSubscriptionMigrationStore _store;

    public LegacyWebhookSubscriptionMigrator(IWebhookSubscriptionMigrationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<WebhookSubscriptionMigrationReport> RunAsync(
        WebhookSubscriptionMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var batchSize = options.BatchSize > 0 ? options.BatchSize : 500;
        var report = new WebhookSubscriptionMigrationReport { DryRun = options.DryRun };
        Guid? afterId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _store.GetActiveLegacyBatchAsync(afterId, batchSize, cancellationToken);
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
        LegacyWebhookSubscriptionRow row,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.Secret))
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineEmptySecret,
                Detail: "empty_or_blank_secret");
        }

        string normalizedUrl;
        try
        {
            // Same rules as new One writes; allow loopback HTTP for local/dev fixtures.
            normalizedUrl = WebhookUrlValidator.NormalizeAndValidate(row.Url, allowHttpLoopback: true);
        }
        catch (InvalidOperationException ex)
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineInvalidUrl,
                Detail: TruncateDetail(ex.Message));
        }

        var existing = await _store.FindByOrgAndUrlAsync(
            row.OrganizationId,
            normalizedUrl,
            cancellationToken);
        if (existing is not null)
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.AlreadyMigrated,
                TargetId: existing.Id);
        }

        if (!await _store.OrganizationExistsAsync(row.OrganizationId, cancellationToken))
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.QuarantineOrphanOrg,
                Detail: "organization_missing_on_one");
        }

        // Domain ctor: preserves secret (does NOT mint). Mints new endpoint Id.
        // Do not use CreateWebhookEndpointCommand — that remints whsec_ secrets.
        var secret = row.Secret.Trim();
        var endpoint = new TenantWebhookEndpoint(
            row.OrganizationId,
            normalizedUrl,
            secret,
            isActive: true,
            LhdnInvoiceEnabledEvents);

        var insert = new MigratedTenantWebhookEndpointInsert
        {
            Id = endpoint.Id,
            OrganizationId = endpoint.OrganizationId,
            Url = endpoint.Url,
            SecretKey = endpoint.SecretKey,
            IsActive = endpoint.IsActive,
            EnabledEventsJson = JsonSerializer.Serialize(
                endpoint.EnabledEvents.ToList(),
                EnabledEventsJsonOptions),
            // Prefer source created time for audit continuity; UpdatedAt = domain now.
            CreatedAt = row.CreatedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)
                : row.CreatedAt.ToUniversalTime(),
            UpdatedAt = endpoint.UpdatedAt
        };

        if (dryRun)
        {
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.WouldInsert,
                TargetId: insert.Id);
        }

        var inserted = await _store.TryInsertAsync(insert, cancellationToken);
        if (!inserted)
        {
            // Race: another process inserted same Org+Url between probe and insert.
            var raced = await _store.FindByOrgAndUrlAsync(
                row.OrganizationId,
                normalizedUrl,
                cancellationToken);
            return new MigrationRowOutcome(
                row.Id,
                MigrationRowCodes.InsertConflict,
                TargetId: raced?.Id ?? insert.Id,
                Detail: "not_exists_race_org_url");
        }

        return new MigrationRowOutcome(
            row.Id,
            MigrationRowCodes.Inserted,
            TargetId: insert.Id);
    }

    private static string TruncateDetail(string message)
    {
        const int max = 200;
        if (string.IsNullOrEmpty(message))
        {
            return "invalid_url";
        }

        var oneLine = message.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return oneLine.Length <= max ? oneLine : oneLine[..max];
    }
}
