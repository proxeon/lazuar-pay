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
        
        // Index for fast lookup by Global Identity
        builder.HasIndex(x => x.GlobalUserId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ConsentedToMarketing).HasDefaultValue(true);
    }
}
