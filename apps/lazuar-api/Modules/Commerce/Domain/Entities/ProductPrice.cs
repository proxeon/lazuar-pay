using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class ProductPrice : Entity
{
    public const string IntervalMonth = "mo";
    public const string IntervalYear = "yr";

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Interval { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool IsDefault { get; private set; }

#pragma warning disable CS8618
    private ProductPrice() { }
#pragma warning restore CS8618

    internal static ProductPrice Create(Guid productId, string interval, decimal amount, bool isDefault)
    {
        return new ProductPrice
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Interval = NormalizeInterval(interval),
            Amount = amount,
            IsDefault = isDefault
        };
    }

    internal void Update(string interval, decimal amount, bool isDefault)
    {
        Interval = NormalizeInterval(interval);
        Amount = amount;
        IsDefault = isDefault;
    }

    internal void SetAmount(decimal amount) => Amount = amount;

    internal void MarkDefault() => IsDefault = true;

    internal void ClearDefault() => IsDefault = false;

    public static bool IsAllowedInterval(string interval)
    {
        var normalized = (interval ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is IntervalMonth or IntervalYear or "one_time";
    }

    public static string NormalizeInterval(string interval)
    {
        var normalized = (interval ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not (IntervalMonth or IntervalYear or "one_time"))
        {
            throw new InvalidOperationException("Price interval must be mo, yr, or one_time.");
        }

        return normalized;
    }
}
