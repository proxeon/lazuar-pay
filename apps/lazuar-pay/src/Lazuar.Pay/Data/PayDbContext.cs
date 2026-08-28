using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Data;

/// <summary>One context, public schema. Not nine module DbContexts.</summary>
public sealed class PayDbContext(DbContextOptions<PayDbContext> options) : DbContext(options)
{
    public DbSet<OrgSettingsRow> OrgSettings => Set<OrgSettingsRow>();
    public DbSet<CheckoutRow> Checkouts => Set<CheckoutRow>();
    public DbSet<PaymentLinkRow> PaymentLinks => Set<PaymentLinkRow>();
    public DbSet<IdempotencyKeyRow> IdempotencyKeys => Set<IdempotencyKeyRow>();
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<PriceRow> Prices => Set<PriceRow>();
    public DbSet<GatewayCredentialRow> GatewayCredentials => Set<GatewayCredentialRow>();
    public DbSet<PspWebhookEventRow> PspWebhookEvents => Set<PspWebhookEventRow>();
    public DbSet<ChargeRow> Charges => Set<ChargeRow>();
    public DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();
    public DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();
    public DbSet<JournalLineRow> JournalLines => Set<JournalLineRow>();
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();
    public DbSet<DocumentSequenceRow> DocumentSequences => Set<DocumentSequenceRow>();
    public DbSet<PayerRow> Payers => Set<PayerRow>();
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();
    public DbSet<MailOutboxRow> MailOutbox => Set<MailOutboxRow>();
    public DbSet<OneWebhookEventRow> OneWebhookEvents => Set<OneWebhookEventRow>();
    public DbSet<OrgWebhookEndpointRow> OrgWebhookEndpoints => Set<OrgWebhookEndpointRow>();
    public DbSet<OrgWebhookDeliveryRow> OrgWebhookDeliveries => Set<OrgWebhookDeliveryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("public");

        model.Entity<OrgSettingsRow>(e =>
        {
            e.ToTable("org_settings");
            e.HasKey(x => x.OrgId);
        });
        model.Entity<CheckoutRow>(e =>
        {
            e.ToTable("checkouts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicToken).IsUnique();
            e.HasIndex(x => x.OrgId);
            e.HasIndex(x => x.PaymentLinkId);
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        model.Entity<PaymentLinkRow>(e =>
        {
            e.ToTable("payment_links");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicToken).IsUnique();
            e.HasIndex(x => x.OrgId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        model.Entity<IdempotencyKeyRow>(e =>
        {
            e.ToTable("idempotency_keys");
            e.HasKey(x => new { x.OrgId, x.Key });
        });
        model.Entity<ProductRow>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrgId);
        });
        model.Entity<PriceRow>(e =>
        {
            e.ToTable("prices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        model.Entity<GatewayCredentialRow>(e =>
        {
            e.ToTable("gateway_credentials");
            e.HasKey(x => new { x.OrgId, x.Provider });
        });
        model.Entity<PspWebhookEventRow>(e =>
        {
            e.ToTable("psp_webhook_events");
            e.HasKey(x => new { x.OrgId, x.Provider, x.EventId });
        });
        model.Entity<ChargeRow>(e =>
        {
            e.ToTable("charges");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CheckoutId).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        model.Entity<SubscriptionRow>(e =>
        {
            e.ToTable("subscriptions");
            e.HasKey(x => x.Id);
        });
        model.Entity<JournalEntryRow>(e =>
        {
            e.ToTable("journal_entries");
            e.HasKey(x => x.Id);
        });
        model.Entity<JournalLineRow>(e =>
        {
            e.ToTable("journal_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        model.Entity<DocumentRow>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CheckoutId).IsUnique();
            e.HasIndex(x => new { x.OrgId, x.Number }).IsUnique();
        });
        model.Entity<DocumentSequenceRow>(e =>
        {
            e.ToTable("document_sequences");
            e.HasKey(x => new { x.OrgId, x.Series, x.YearMyt });
        });
        model.Entity<PayerRow>(e =>
        {
            e.ToTable("payers");
            e.HasKey(x => x.Id);
        });
        model.Entity<AuditEventRow>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
        });
        model.Entity<MailOutboxRow>(e =>
        {
            e.ToTable("mail_outbox");
            e.HasKey(x => x.Id);
        });
        model.Entity<OneWebhookEventRow>(e =>
        {
            e.ToTable("one_webhook_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeliveryId).IsUnique();
        });
        model.Entity<OrgWebhookEndpointRow>(e =>
        {
            e.ToTable("org_webhook_endpoints");
            e.HasKey(x => x.OrgId);
        });
        model.Entity<OrgWebhookDeliveryRow>(e =>
        {
            e.ToTable("org_webhook_deliveries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });
    }
}
