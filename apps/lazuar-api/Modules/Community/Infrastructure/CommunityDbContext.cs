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
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.Entities;

namespace Modules.Community.Infrastructure;

public class CommunityDbContext : PlatformDbContext
{
    public DbSet<CommunitySpace> CommunitySpaces { get; set; } = null!;
    public DbSet<CommunityMember> CommunityMembers { get; set; } = null!;

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

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var guidListConverter = new ValueConverter<IReadOnlyCollection<Guid>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<Guid>>(v, jsonOptions) ?? new List<Guid>()
        );

        var guidListComparer = new ValueComparer<IReadOnlyCollection<Guid>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        modelBuilder.Entity<CommunitySpace>(builder =>
        {
            builder.ToTable("CommunitySpaces");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductIds)
                .HasField("_productIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(guidListConverter, guidListComparer)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<CommunityMember>(builder =>
        {
            builder.ToTable("CommunityMembers");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.CommunitySpaceId, x.ClientProfileId }).IsUnique();
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
