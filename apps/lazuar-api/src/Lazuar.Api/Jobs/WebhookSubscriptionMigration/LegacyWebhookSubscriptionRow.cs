namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// Read model for an active row in <c>lhdn.WebhookSubscriptions</c>.
/// </summary>
public sealed class LegacyWebhookSubscriptionRow
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
