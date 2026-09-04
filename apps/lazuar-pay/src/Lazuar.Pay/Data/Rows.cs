namespace Lazuar.Pay.Data;

public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    /// <summary>Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.</summary>
    public string? ActiveProvider { get; set; }
    /// <summary>Per-org One <c>whsec_</c>. Process <c>Pay:OneWebhookSecret</c> is the one-shop fallback.</summary>
    public string? OneWebhookCiphertext { get; set; }
}

public sealed class CheckoutRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string Interval { get; set; } = "one_off";
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? PspRedirectUrl { get; set; }
    public string? PayerName { get; set; }
    public string? PayerEmail { get; set; }
    public string? ProductId { get; set; }
    public string? Provider { get; set; }
    public string? ProviderSessionId { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? SlotKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? WatchClaimedAt { get; set; }
}

/// <summary>Shared pay-link URL. MaxPayers null is unlimited. Each payer is a child checkout.</summary>
public sealed class PaymentLinkRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required string Provider { get; set; }
    public string? ProductId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    /// <summary>Null means unlimited payers. 1 is one person. N is a cap.</summary>
    public int? MaxPayers { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class IdempotencyKeyRow
{
    public required string OrgId { get; set; }
    public required string Key { get; set; }
    public required string CheckoutId { get; set; }
}

public sealed class ProductRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PriceRow
{
    public required string Id { get; set; }
    public required string ProductId { get; set; }
    public required string Currency { get; set; }
    public required decimal Amount { get; set; }
    public required string Interval { get; set; }
}

public sealed class GatewayCredentialRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string Ciphertext { get; set; }
    public string? Last4 { get; set; }
    public string? WebhookCiphertext { get; set; }
    public string? PublicMerchantId { get; set; }
    public string Environment { get; set; } = "test";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PspWebhookEventRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string EventId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class ChargeRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public required string Provider { get; set; }
    public string? ProviderRef { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
}

public sealed class SubscriptionRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public string? PayerId { get; set; }
    public required string Status { get; set; }
    public required string Interval { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? PastDueAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RefundRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public string? ChargeId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public required string Provider { get; set; }
    public string? ProviderRef { get; set; }
    public string Reason { get; set; } = "merchant";
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class JournalEntryRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class JournalLineRow
{
    public required string Id { get; set; }
    public required string EntryId { get; set; }
    public required string Account { get; set; }
    public required string Dc { get; set; }
    public required decimal Amount { get; set; }
}

public sealed class DocumentRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string CheckoutId { get; set; }
    public string? Number { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DocumentSequenceRow
{
    public required string OrgId { get; set; }
    public required string Series { get; set; }
    public required int YearMyt { get; set; }
    public int LastN { get; set; }
}

public sealed class PayerRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
}

public sealed class AuditEventRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string Action { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class MailOutboxRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public string? ToEmail { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OneWebhookEventRow
{
    public required string Id { get; set; }
    public required string DeliveryId { get; set; }
    public required string EventType { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class OrgWebhookEndpointRow
{
    public required string OrgId { get; set; }
    public required string Url { get; set; }
    public required string SecretCiphertext { get; set; }
    public string? SecretPrefix { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OrgWebhookDeliveryRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string EventId { get; set; }
    public required string EventType { get; set; }
    public required string PayloadJson { get; set; }
    public required string Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public int? LastHttpStatus { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
