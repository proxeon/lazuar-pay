using System;
using System.Globalization;
using System.Text.Json;
using Modules.Payments.Infrastructure.Gateways;

namespace Modules.Payments.Infrastructure;

/// <summary>
/// Reads M2M checkout <c>amount</c> as <see cref="decimal"/> so IEEE-754
/// <c>double</c> on the generated DTO cannot shift sen.
/// </summary>
internal static class IntegrationCheckoutAmount
{
    public static bool TryRead(JsonElement root, string? currency, out decimal amount, out string? error)
    {
        amount = 0;
        if (!root.TryGetProperty("amount", out var el)
            && !root.TryGetProperty("Amount", out el))
        {
            error = "Amount is required.";
            return false;
        }

        if (el.ValueKind == JsonValueKind.Number)
        {
            if (!el.TryGetDecimal(out amount))
            {
                error = "Amount must be a decimal number.";
                return false;
            }
        }
        else if (el.ValueKind == JsonValueKind.String)
        {
            if (!decimal.TryParse(
                    el.GetString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out amount))
            {
                error = "Amount must be a decimal number.";
                return false;
            }
        }
        else
        {
            error = "Amount must be a decimal number.";
            return false;
        }

        if (!IsZeroDecimal(currency)
            && decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            error = "Amount must have at most 2 decimal places.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsZeroDecimal(string? currency) =>
        GatewayCommon.IsZeroDecimalCurrency(currency);
}
