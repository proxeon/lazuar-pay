using System;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

/// <summary>
/// Checkout quantity for product initiate. Recurring and PWYW are always 1 (seats are LP-060).
/// </summary>
public static class CommerceCheckoutQuantity
{
    public const int Min = 1;
    public const int Max = 99;

    public static bool AllowsAdjustment(Product product) =>
        string.Equals(product.PricingModel, "FIXED", StringComparison.OrdinalIgnoreCase)
        && string.Equals(product.Interval, "one_time", StringComparison.OrdinalIgnoreCase);

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
                "Quantity other than 1 is only allowed for fixed-price one-time products.");
        }

        return quantity;
    }
}
