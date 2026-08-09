namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// Minimal projection of an existing <c>one.TenantWebhookEndpoints</c> row for idempotency checks.
/// Does not surface <c>SecretKey</c> (avoid accidental logging).
/// </summary>
public sealed class OneWebhookEndpointProbe
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Url { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
