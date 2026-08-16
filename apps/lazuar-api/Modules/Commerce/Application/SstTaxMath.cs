namespace Modules.Commerce.Application;

public static class SstTaxMath
{
    public const string NotApplicable = "06";
    public const string ServiceTax = "02";

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
