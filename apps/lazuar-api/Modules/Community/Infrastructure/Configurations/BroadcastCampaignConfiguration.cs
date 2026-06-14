using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Infrastructure.Configurations;

public class BroadcastCampaignConfiguration : IEntityTypeConfiguration<BroadcastCampaign>
{
    public void Configure(EntityTypeBuilder<BroadcastCampaign> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Channel).IsRequired().HasMaxLength(20);
        builder.Property(x => x.TargetStatus).HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
    }
}
