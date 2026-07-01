using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Infrastructure;

public class CommunicationsDbContext : PlatformDbContext
{
    public DbSet<MessageTemplate> MessageTemplates { get; set; } = null!;
    public DbSet<SuppressionEntry> SuppressionEntries { get; set; } = null!;
    
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public CommunicationsDbContext(
        DbContextOptions<CommunicationsDbContext> options,
        IExecutionContextAccessor executionContext,
        IMediator mediator,
        DatabaseJobTrigger jobTrigger)
        : base(options, executionContext, mediator, jobTrigger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("communications");

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

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.OccurredOn }).HasFilter("\"ProcessedAt\" IS NULL");
        });

        modelBuilder.Entity<SuppressionEntry>(builder =>
        {
            builder.ToTable("SuppressionEntries");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            builder.Property(x => x.Email).HasMaxLength(320);
            builder.Property(x => x.Reason).HasMaxLength(20);
            builder.Property(x => x.Source).HasMaxLength(100);
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ProcessedAt, x.ReceivedAt }).HasFilter("\"ProcessedAt\" IS NULL");
        });
    }
}
