using System;
using Modules.Billing.Domain;

namespace Modules.Billing.Infrastructure.Services;

/// <summary>
/// Credit Notes vs Tax Invoices allow-lists. Chargebacks are reversals;
/// Hub/top-up/commission/$0 headers are neither sales nor CNs.
/// </summary>
internal static class LedgerTypeFilters
{
    public static bool Matches(string? typeFilter, string referenceType)
    {
        if (string.IsNullOrWhiteSpace(typeFilter))
        {
            return true;
        }

        if (string.Equals(typeFilter, "sales", StringComparison.OrdinalIgnoreCase))
        {
            return referenceType is LedgerReferenceTypes.GatewayPayment
                or LedgerReferenceTypes.ManualEnrollment;
        }

        if (string.Equals(typeFilter, "reversals", StringComparison.OrdinalIgnoreCase))
        {
            return referenceType is LedgerReferenceTypes.GatewayRefund
                or LedgerReferenceTypes.LhdnCancellation
                or LedgerReferenceTypes.SystemCreditChargeback
                or LedgerReferenceTypes.GatewayDispute
                or LedgerReferenceTypes.SystemSaasFeeReverse;
        }

        return true;
    }

    public static string SalesSqlIn() =>
        $"('{LedgerReferenceTypes.GatewayPayment}', '{LedgerReferenceTypes.ManualEnrollment}')";

    public static string ReversalsSqlIn() =>
        $"('{LedgerReferenceTypes.GatewayRefund}', '{LedgerReferenceTypes.LhdnCancellation}', '{LedgerReferenceTypes.SystemCreditChargeback}', '{LedgerReferenceTypes.GatewayDispute}', '{LedgerReferenceTypes.SystemSaasFeeReverse}')";
}
