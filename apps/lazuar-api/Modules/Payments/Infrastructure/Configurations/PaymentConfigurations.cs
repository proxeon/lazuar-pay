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
    }
}

public class PaymentWebhookLogConfig : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.HasKey(x => x.Id);

        // Guarantee idempotency at the database level!
        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}
