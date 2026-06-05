using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Messaging.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Modules.Messaging.Infrastructure;

public class MessagingDbContext : PlatformDbContext
{
    public DbSet<TenantReplica> TenantReplicas { get; set; } = null!;
    public DbSet<MessageTemplate> MessageTemplates { get; set; } = null!;
    public DbSet<AutomationRule> AutomationRules { get; set; } = null!;
    public DbSet<AutomationQueue> AutomationQueue { get; set; } = null!;
    
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    public MessagingDbContext(
        DbContextOptions<MessagingDbContext> options, 
        IExecutionContextAccessor executionContext, 
        IMediator mediator, 
        DatabaseJobTrigger jobTrigger) : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("messaging");

        modelBuilder.Entity<TenantReplica>(builder => 
        { 
            builder.ToTable("TenantReplicas"); 
            builder.HasKey(x => x.Id); 
        });

        modelBuilder.Entity<MessageTemplate>(builder =>
        {
            builder.ToTable("MessageTemplates");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.OrganizationId);

            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
            };

            var stringListConverter = new ValueConverter<IReadOnlyCollection<string>, string>(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()
            );
            
            var stringListComparer = new ValueComparer<IReadOnlyCollection<string>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
            );

            builder.Property(x => x.RequiredVariables)
                   .HasConversion(stringListConverter, stringListComparer)
                   .HasColumnType("jsonb");
                   
            builder.Property(x => x.OptionalVariables)
                   .HasConversion(stringListConverter, stringListComparer)
                   .HasColumnType("jsonb");
        });

        modelBuilder.Entity<AutomationRule>(builder =>
        {
            builder.ToTable("AutomationRules");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.TriggerType });
            
            builder.HasOne<MessageTemplate>()
                   .WithMany()
                   .HasForeignKey(x => x.TemplateId)
                   .OnDelete(DeleteBehavior.SetNull)
                   .IsRequired(false);
        });

        modelBuilder.Entity<AutomationQueue>(builder =>
        {
            builder.ToTable("AutomationQueue");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.Status, x.ScheduledAt });
            builder.HasIndex(x => x.OrganizationId);

            builder.HasOne<AutomationRule>()
                   .WithMany()
                   .HasForeignKey(x => x.AutomationRuleId)
                   .OnDelete(DeleteBehavior.SetNull)
                   .IsRequired(false);
        });

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
