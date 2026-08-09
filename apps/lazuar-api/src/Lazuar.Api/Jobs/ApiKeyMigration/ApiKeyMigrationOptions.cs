namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// One-shot legacy API key migrator configuration.
/// Bind from section <see cref="SectionName"/>; env overrides
/// <c>API_KEY_MIGRATION_ENABLED</c> / <c>API_KEY_MIGRATION_DRY_RUN</c>.
/// </summary>
public sealed class ApiKeyMigrationOptions
{
    public const string SectionName = "ApiKeyMigration";

    /// <summary>When false (default), the hosted migrator is not registered.</summary>
    public bool Enabled { get; set; }

    /// <summary>When true (default), evaluate rows but do not insert into One.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Page size when scanning <c>lhdn.DeveloperApiKeys</c>.</summary>
    public int BatchSize { get; set; } = 500;
}
