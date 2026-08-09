namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>Aggregate counters + optional outcome list for a migrator run.</summary>
public sealed class WebhookSubscriptionMigrationReport
{
    public bool DryRun { get; init; }
    public int Processed { get; set; }
    public int Inserted { get; set; }
    public int WouldInsert { get; set; }
    public int AlreadyMigrated { get; set; }
    public int InsertConflict { get; set; }
    public int Quarantined { get; set; }

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
            case MigrationRowCodes.InsertConflict:
                InsertConflict++;
                break;
            case MigrationRowCodes.QuarantineInvalidUrl:
            case MigrationRowCodes.QuarantineEmptySecret:
            case MigrationRowCodes.QuarantineOrphanOrg:
                Quarantined++;
                break;
        }
    }
}
