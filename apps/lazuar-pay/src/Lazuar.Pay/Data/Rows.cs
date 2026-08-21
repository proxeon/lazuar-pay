namespace Lazuar.Pay.Data;

public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>null = unknown (fail closed for SST). true/false when merchant set it.</summary>
    public bool? SstRegistered { get; set; }
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
