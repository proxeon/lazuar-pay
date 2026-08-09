namespace Lazuar.Api.Jobs.ApiKeyMigration;

/// <summary>
/// Persistence port for the legacy API key migrator (Dapper in prod; in-memory in tests).
/// </summary>
public interface IApiKeyMigrationStore
{
    /// <summary>
    /// Keyset page of legacy rows ordered by <see cref="LegacyDeveloperApiKeyRow.Id"/>.
    /// Pass <paramref name="afterId"/> = last id of previous page (null for first page).
    /// </summary>
    Task<IReadOnlyList<LegacyDeveloperApiKeyRow>> GetLegacyBatchAsync(
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<OneCredentialProbe?> FindByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default);

    Task<OneCredentialProbe?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> OrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert into <c>one.ApiCredentials</c>. Returns <c>false</c> when
    /// <c>ON CONFLICT (KeyHash) DO NOTHING</c> skipped the row (race).
    /// </summary>
    Task<bool> TryInsertAsync(
        MigratedApiCredentialInsert row,
        CancellationToken cancellationToken = default);
}

/// <summary>Insert DTO for a migrated credential (raw SQL; not EF aggregate).</summary>
public sealed class MigratedApiCredentialInsert
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public string KeyHash { get; init; } = string.Empty;
    public string KeyHint { get; init; } = string.Empty;
    public string Scopes { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
