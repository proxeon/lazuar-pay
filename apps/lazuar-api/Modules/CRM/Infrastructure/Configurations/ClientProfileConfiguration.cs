using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.CRM.Domain;

namespace Modules.CRM.Infrastructure.Configurations;

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfileEntity>
{
    public void Configure(EntityTypeBuilder<ClientProfileEntity> builder)
    {
        builder.ToTable("ClientProfiles");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OrganizationId, x.Email, x.Phone }).IsUnique();
        builder.HasIndex(x => x.GlobalUserId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Tin).HasMaxLength(20);
        builder.Property(x => x.IdType).HasMaxLength(10);
        builder.Property(x => x.IdValue).HasMaxLength(50);
        builder.Property(x => x.ConsentedToMarketing).HasDefaultValue(false);

        builder.OwnsOne(x => x.Address, a =>
        {
            a.Property(p => p.Line1).HasColumnName("AddressLine1").HasMaxLength(150);
            a.Property(p => p.Line2).HasColumnName("AddressLine2").HasMaxLength(150);
            a.Property(p => p.Line3).HasColumnName("AddressLine3").HasMaxLength(150);
            a.Property(p => p.City).HasColumnName("City").HasMaxLength(50);
            a.Property(p => p.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
            a.Property(p => p.StateCode).HasColumnName("StateCode").HasMaxLength(10);
            a.Property(p => p.CountryCode).HasColumnName("CountryCode").HasMaxLength(10);
        });
    }
}
