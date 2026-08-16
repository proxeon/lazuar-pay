using System;

namespace Modules.Commerce.Application;

internal static class OfflinePaymentMethods
{
    public const string BankTransfer = "BANK_TRANSFER";
    public const string Cash = "CASH";
    public const string Comped = "COMPED";

    public static string Normalize(string? paymentMethod)
    {
        var normalized = (paymentMethod ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is not (BankTransfer or Cash or Comped))
        {
            throw new InvalidOperationException("Payment method must be BANK_TRANSFER, CASH, or COMPED.");
        }

        return normalized;
    }
}
