namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>Aggregate counters + optional outcome list for a migrator run.</summary>
public sealed class ApiKeyMigrationReport
{
    public bool DryRun { get; init; }
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int WouldInsert { get; set; }
    public int AlreadyMigrated { get; set; }
    public int HashCollisionDifferentOrg { get; set; }
    public int InsertConflict { get; set; }
    public int Quarantined { get; set; }
    public int IdRemapped { get; set; }
    public int PartialScopes { get; set; }

    public List<MigrationRowOutcome> Outcomes { get; } = [];

    public void Add(MigrationRowOutcome outcome)
    {
        Outcomes.Add(outcome);
        Processed++;

        switch (outcome.Code)
        {
            case MigrationRowCodes.Inserted:
                Inserted++;
                break;
            case MigrationRowCodes.WouldInsert:
                WouldInsert++;
                break;
            case MigrationRowCodes.AlreadyMigrated:
                AlreadyMigrated++;
                break;
            case MigrationRowCodes.HashCollisionDifferentOrg:
                HashCollisionDifferentOrg++;
                break;
            case MigrationRowCodes.InsertConflict:
                InsertConflict++;
                break;
            case MigrationRowCodes.QuarantineEmptyHash:
            case MigrationRowCodes.QuarantineOrphanOrg:
            case MigrationRowCodes.QuarantineUnknownScopesOnly:
                Quarantined++;
                break;
        }

        if (outcome.IdRemapped)
        {
            IdRemapped++;
        }

        if (outcome.Detail is not null
            && outcome.Detail.StartsWith("dropped_scopes:", StringComparison.Ordinal))
        {
            PartialScopes++;
        }
    }
}
