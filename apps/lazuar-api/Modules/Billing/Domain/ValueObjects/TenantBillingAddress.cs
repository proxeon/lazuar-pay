using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.ValueObjects;

public class TenantBillingAddress : ValueObject
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string? Line3 { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string StateCode { get; }
    public string CountryCode { get; }

    public TenantBillingAddress(string line1, string? line2, string? line3, string city, string postalCode, string stateCode, string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        Line3 = line3;
        City = city;
        PostalCode = postalCode;
        StateCode = stateCode;
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "MYS" : countryCode;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Line1;
        if (Line2 != null) yield return Line2;
        if (Line3 != null) yield return Line3;
        yield return City;
        yield return PostalCode;
        yield return StateCode;
        yield return CountryCode;
    }
}
