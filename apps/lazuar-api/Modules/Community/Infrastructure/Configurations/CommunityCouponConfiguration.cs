using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.Configurations;

public class CommunityCouponConfiguration : IEntityTypeConfiguration<CommunityCoupon>
{
    public void Configure(EntityTypeBuilder<CommunityCoupon> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(10, 2);
    }
}
