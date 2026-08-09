namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>Stable result codes for a single legacy key row.</summary>
public static class MigrationRowCodes
{
    public const string Inserted = "inserted";
    public const string WouldInsert = "would_insert";
    public const string AlreadyMigrated = "already_migrated";
    public const string HashCollisionDifferentOrg = "hash_collision_different_org";
    public const string InsertConflict = "insert_conflict";
    public const string QuarantineEmptyHash = "quarantine_empty_hash";
    public const string QuarantineOrphanOrg = "quarantine_orphan_org";
    public const string QuarantineUnknownScopesOnly = "quarantine_unknown_scopes_only";
}

/// <summary>Per-row migrator outcome (never includes plaintext key material).</summary>
public sealed record MigrationRowOutcome(
    Guid SourceId,
    string Code,
    Guid? TargetId = null,
    bool IdRemapped = false,
    string? Detail = null);
