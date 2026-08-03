using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Ops.Domain;

namespace Modules.Ops.Infrastructure;

public class OpsDbContext : PlatformDbContext
{
    public DbSet<OpsConversation> Conversations { get; set; } = null!;
    public DbSet<OpsMessage> Messages { get; set; } = null!;

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public OpsDbContext(
        DbContextOptions<OpsDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger) : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("ops");

        modelBuilder.Entity<OpsConversation>(builder =>
        {
            builder.ToTable("Conversations");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);
            builder.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<OpsMessage>(builder =>
        {
            builder.ToTable("Messages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.ConversationId });
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.NextAttemptAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
