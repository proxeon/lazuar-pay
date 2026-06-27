using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.Entities;

namespace Modules.Community.Infrastructure;

public class CommunityDbContext : PlatformDbContext
{
    public DbSet<BroadcastCampaign> BroadcastCampaigns { get; set; } = null!;
    public DbSet<MessageTemplate> MessageTemplates { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public CommunityDbContext(
        DbContextOptions<CommunityDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger)
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("community");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
