using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
        builder.Property(x => x.MinimumOriginalPrice).HasPrecision(10, 2);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var guidListConverter = new ValueConverter<IReadOnlyCollection<Guid>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => DeserializeSafe(v, jsonOptions)
        );

        var guidListComparer = new ValueComparer<IReadOnlyCollection<Guid>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(x => x.ApplicablePlanIds)
            .HasConversion(guidListConverter, guidListComparer)
            .HasColumnType("jsonb");
    }

    // Gracefully handle malformed database JSON to prevent API 500 crash loops
    private static List<Guid> DeserializeSafe(string json, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, options) ?? new List<Guid>();
        }
        catch (Exception)
        {
            return new List<Guid>();
        }
    }
}
