using System;
using Modules.Payments.Application.Exceptions;

namespace Modules.Payments.Application.Services;

public static class CheckoutAmountRules
{
    public const decimal MyrMinimum = 2.00m;
    public const decimal DefaultMinimum = 0.50m;

    public static decimal MinimumFor(string currency) =>
        string.Equals(currency, "MYR", StringComparison.OrdinalIgnoreCase)
            ? MyrMinimum
            : DefaultMinimum;

    public static void ValidateAmountAndCurrency(decimal amount, string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw PaymentIntegrationException.CurrencyInvalid(
                "Currency must be a 3-letter ISO 4217 code.");
        }

        if (amount <= 0)
        {
            throw PaymentIntegrationException.AmountInvalid("Amount must be greater than zero.");
        }

        var code = currency.Trim().ToUpperInvariant();
        if (string.Equals(code, "MYR", StringComparison.Ordinal)
            && decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw PaymentIntegrationException.AmountInvalid("Amount must have at most 2 decimal places.");
        }
        var min = MinimumFor(code);
        if (amount < min)
        {
            throw PaymentIntegrationException.AmountBelowMinimum(min, code);
        }
    }
}
