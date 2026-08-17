using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Domain.Entities;

namespace Modules.Payments.Infrastructure.Configurations;

public class TenantPaymentConfigurationConfig : IEntityTypeConfiguration<TenantPaymentConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantPaymentConfiguration> builder)
    {
        builder.HasKey(x => x.Id);

        // Ensure one active configuration per gateway type per tenant
        builder.HasIndex(x => new { x.OrganizationId, x.GatewayType }).IsUnique();

        builder.Property(x => x.GatewayType).HasMaxLength(50);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.Environment).HasMaxLength(8).HasDefaultValue("test");
    }
}

public class PaymentWebhookLogConfig : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.HasKey(x => x.Id);

        // Event-delivery idempotency is per tenant (shared CHIP/Xendit credentials).
        builder.HasIndex(x => new { x.OrganizationId, x.Provider, x.EventId }).IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Provider, x.BusinessKey })
            .IsUnique()
            .HasFilter("\"BusinessKey\" IS NOT NULL");

        builder.Property(x => x.OutboxMessageId);
    }
}

public class IntegrationCheckoutSessionConfig : IEntityTypeConfiguration<IntegrationCheckoutSession>
{
    public void Configure(EntityTypeBuilder<IntegrationCheckoutSession> builder)
    {
        builder.ToTable("IntegrationCheckoutSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.RequestFingerprint).HasMaxLength(128);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(120);
        builder.Property(x => x.SuccessUrl).IsRequired();
        builder.Property(x => x.CancelUrl).IsRequired();
        builder.Property(x => x.GatewayName).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderSessionId).HasMaxLength(200);
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(200);
        builder.Property(x => x.CheckoutUrl);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasIndex(x => new { x.OrganizationId, x.Id });

        builder.HasIndex(x => new { x.OrganizationId, x.ProviderSessionId })
            .HasFilter("\"ProviderSessionId\" IS NOT NULL");

        builder.HasIndex(x => x.ExpiresAt)
            .HasFilter("\"Status\" = 'open'");
    }
}
