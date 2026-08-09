namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>Stable result codes for a single LHDN webhook subscription row.</summary>
public static class MigrationRowCodes
{
    public const string Inserted = "inserted";
    public const string WouldInsert = "would_insert";
    public const string AlreadyMigrated = "already_migrated";
    public const string InsertConflict = "insert_conflict";
    public const string QuarantineInvalidUrl = "quarantine_invalid_url";
    public const string QuarantineEmptySecret = "quarantine_empty_secret";
    public const string QuarantineOrphanOrg = "quarantine_orphan_org";
}

/// <summary>
/// Per-row migrator outcome. Never includes secrets or full signing material.
/// </summary>
public sealed record MigrationRowOutcome(
    Guid SourceId,
    string Code,
    Guid? TargetId = null,
    string? Detail = null);
