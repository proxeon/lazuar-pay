namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// Persistence port for the LHDN → One webhook registry migrator (Dapper in prod; in-memory in tests).
/// </summary>
public interface IWebhookSubscriptionMigrationStore
{
    /// <summary>
    /// Keyset page of <b>active</b> legacy rows ordered by <see cref="LegacyWebhookSubscriptionRow.Id"/>.
    /// Pass <paramref name="afterId"/> = last id of previous page (null for first page).
    /// </summary>
    Task<IReadOnlyList<LegacyWebhookSubscriptionRow>> GetActiveLegacyBatchAsync(
        Guid? afterId,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find existing One endpoint by exact <c>OrganizationId</c> + <c>Url</c> (idempotency key).
    /// </summary>
    Task<OneWebhookEndpointProbe?> FindByOrgAndUrlAsync(
        Guid organizationId,
        string url,
        CancellationToken cancellationToken = default);

    Task<bool> OrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert into <c>one.TenantWebhookEndpoints</c>. Returns <c>false</c> when a concurrent
    /// insert already created the same Org+Url (idempotent race).
    /// </summary>
    Task<bool> TryInsertAsync(
        MigratedTenantWebhookEndpointInsert row,
        CancellationToken cancellationToken = default);
}

/// <summary>Insert DTO for a migrated webhook endpoint (raw SQL; built from domain ctor).</summary>
public sealed class MigratedTenantWebhookEndpointInsert
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    /// <summary>JSON array string for jsonb column, e.g. <c>["invoice.valid","invoice.invalid"]</c>.</summary>
    public string EnabledEventsJson { get; init; } = "[]";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
