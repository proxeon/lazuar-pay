using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.Entities;
using Modules.Community.Domain.ValueObjects;

namespace Modules.Community.Infrastructure.Configurations;

public class CommunityPlanConfiguration : IEntityTypeConfiguration<CommunityPlan>
{
    public void Configure(EntityTypeBuilder<CommunityPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
        builder.Property(x => x.Price).HasPrecision(10, 2);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var featuresConverter = new ValueConverter<IReadOnlyCollection<string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()
        );

        var faqConverter = new ValueConverter<IReadOnlyCollection<FaqItem>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<FaqItem>>(v, jsonOptions) ?? new List<FaqItem>()
        );

        var featuresComparer = new ValueComparer<IReadOnlyCollection<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        var faqComparer = new ValueComparer<IReadOnlyCollection<FaqItem>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(x => x.Features)
            .HasConversion(featuresConverter, featuresComparer)
            .HasColumnType("jsonb");

        builder.Property(x => x.Faq)
            .HasConversion(faqConverter, faqComparer)
            .HasColumnType("jsonb");
    }
}

public class CommunitySubscriptionConfiguration : IEntityTypeConfiguration<CommunitySubscription>
{
    public void Configure(EntityTypeBuilder<CommunitySubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.ClientProfileId);
        builder.Property(x => x.ClientProfileId).IsRequired();

        builder.HasOne<CommunityPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.PaymentRecords)
            .WithOne()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ReminderLogs)
            .WithOne()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.PendingCouponId)
            .HasFilter("\"PendingCouponId\" IS NOT NULL");
    }
}

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(10, 2);
        
        builder.HasIndex(x => new { x.SubscriptionId, x.ExternalReference })
            .HasFilter("\"ExternalReference\" IS NOT NULL")
            .IsUnique();
    }
}

public class CommunityReminderScheduleConfiguration : IEntityTypeConfiguration<CommunityReminderSchedule>
{
    public void Configure(EntityTypeBuilder<CommunityReminderSchedule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.OrganizationId, x.DaysRelativeToDue });
        builder.Property(x => x.TemplateId).IsRequired();
        builder.HasOne<CommunityPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}

public class ReminderDispatchLogConfiguration : IEntityTypeConfiguration<ReminderDispatchLog>
{
    public void Configure(EntityTypeBuilder<ReminderDispatchLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SubscriptionId, x.ScheduleId, x.TargetRenewalDate }).IsUnique();
    }
}

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
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
    }
}
