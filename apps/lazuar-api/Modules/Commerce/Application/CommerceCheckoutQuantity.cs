using System;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

/// <summary>
/// Checkout quantity. FIXED one-time and FIXED recurring (mo/yr) may be 1–99. PWYW stays 1.
/// </summary>
public static class CommerceCheckoutQuantity
{
    public const int Min = 1;
    public const int Max = 99;

    public static bool AllowsAdjustment(Product product) =>
        string.Equals(product.PricingModel, "FIXED", StringComparison.OrdinalIgnoreCase)
        && product.Interval is "one_time" or "mo" or "yr";

    public static int NormalizeOrThrow(int? raw, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var quantity = raw ?? Min;
        if (quantity < Min || quantity > Max)
        {
            throw new InvalidOperationException($"Quantity must be between {Min} and {Max}.");
        }

        if (!AllowsAdjustment(product) && quantity != 1)
        {
            throw new InvalidOperationException(
                "Quantity other than 1 is only allowed for fixed-price one-time or recurring products.");
        }

        return quantity;
    }
}
