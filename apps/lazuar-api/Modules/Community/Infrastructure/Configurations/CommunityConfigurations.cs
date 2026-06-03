using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.Entities;

namespace Modules.Community.Infrastructure.Configurations;

public class CommunityPlanConfiguration : IEntityTypeConfiguration<CommunityPlan>
{
    public void Configure(EntityTypeBuilder<CommunityPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
        
        builder.Property(x => x.Price).HasPrecision(10, 2);

        // Store value objects and primitive collections as JSONB in PostgreSQL
        builder.Property(x => x.Features).HasColumnType("jsonb");
        builder.Property(x => x.Faq).HasColumnType("jsonb");
    }
}

public class CommunitySubscriptionConfiguration : IEntityTypeConfiguration<CommunitySubscription>
{
    public void Configure(EntityTypeBuilder<CommunitySubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrganizationId);
        
        // CRUCIAL: Do NOT define foreign keys to ClientProfile. Treat as raw Guid.
        builder.HasIndex(x => x.ClientProfileId);
        builder.Property(x => x.ClientProfileId).IsRequired();

        // PlanId is local to this module, so we can configure the foreign key
        builder.HasOne<CommunityPlan>()
               .WithMany()
               .HasForeignKey(x => x.PlanId)
               .OnDelete(DeleteBehavior.Restrict);

        // Child collection: PaymentRecords
        builder.HasMany(x => x.PaymentRecords)
               .WithOne()
               .HasForeignKey(x => x.SubscriptionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(10, 2);
        
        builder.HasIndex(x => x.ExternalReference)
               .HasFilter("\"ExternalReference\" IS NOT NULL")
               .IsUnique();
    }
}
