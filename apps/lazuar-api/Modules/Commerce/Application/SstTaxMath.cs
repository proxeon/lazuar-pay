namespace Modules.Commerce.Application;

public static class SstTaxMath
{
    public const string NotApplicable = "06";
    public const string ServiceTax = "02";

    /// <summary>
    /// Round exclusive SST on <paramref name="netAmount"/> (one unit, or a custom-quote line).
    /// Callers that have seats must pass the unit net, not the line net — see GrossBreakdown.
    /// </summary>
    public static (string TaxType, decimal TaxAmount) Compute(
        string? requestedType,
        decimal ratePercent,
        decimal netAmount,
        bool merchantHasSstRegistration)
    {
        if (!merchantHasSstRegistration
            || !string.Equals(requestedType, ServiceTax, StringComparison.OrdinalIgnoreCase)
            || ratePercent <= 0
            || netAmount <= 0)
        {
            return (NotApplicable, 0m);
        }

        var tax = Math.Round(netAmount * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return (ServiceTax, tax);
    }
}
