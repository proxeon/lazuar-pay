using System;
using System.Linq;
using Lazuar.ApiTypes;
using Modules.CRM.Contracts;
using Modules.Commerce.Contracts;

namespace Modules.Lhdn.Infrastructure.Services;

internal static class LhdnBuyerMapper
{
    internal static readonly string[] StubTins =
    {
        "C1234567890",
        "IG1234567890",
        "EI00000000010"
    };

    public static bool IsStubTin(string? tin) =>
        !string.IsNullOrWhiteSpace(tin)
        && StubTins.Contains(tin.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool TryCreatePayloadBuyer(
        ClientProfileDto? profile,
        CommerceCustomerDisplay? display,
        out string buyerName,
        out string buyerTin,
        out SubmitDocumentRequestDtoBuyer_id_type idType,
        out string idValue,
        out LhdnAddressDto address)
    {
        buyerName = "";
        buyerTin = "";
        idType = SubmitDocumentRequestDtoBuyer_id_type.BRN;
        idValue = "NA";
        address = new LhdnAddressDto
        {
            Line1 = "NA",
            City = "NA",
            Postal_code = "00000",
            State_code = LhdnAddressDtoState_code._17,
            Country_code = "MYS"
        };

        var tin = FirstNonEmpty(profile?.Tin, display?.Tin);
        if (string.IsNullOrWhiteSpace(tin) || IsStubTin(tin))
            return false;

        buyerTin = tin;
        buyerName = FirstNonEmpty(profile?.Company_name, display?.CompanyName, profile?.Full_name, display?.Name, "Customer");

        var rawIdType = FirstNonEmpty(profile?.Id_type, display?.IdType);
        idType = rawIdType?.ToUpperInvariant() switch
        {
            "NRIC" => SubmitDocumentRequestDtoBuyer_id_type.NRIC,
            "PASSPORT" => SubmitDocumentRequestDtoBuyer_id_type.PASSPORT,
            "ARMY" => SubmitDocumentRequestDtoBuyer_id_type.ARMY,
            _ => SubmitDocumentRequestDtoBuyer_id_type.BRN
        };
        idValue = FirstNonEmpty(profile?.Id_value, display?.IdValue);
        if (string.IsNullOrWhiteSpace(idValue) || string.Equals(idValue, "NA", StringComparison.OrdinalIgnoreCase))
            return false;

        var line1 = FirstNonEmpty(profile?.Billing_address?.Line1, display?.AddressLine1);
        if (!string.IsNullOrWhiteSpace(line1))
        {
            var state = FirstNonEmpty(profile?.Billing_address?.State_code, display?.StateCode) ?? "17";
            if (!Enum.TryParse<LhdnAddressDtoState_code>("_" + state, out var stateCode))
                stateCode = LhdnAddressDtoState_code._17;

            address = new LhdnAddressDto
            {
                Line1 = line1,
                Line2 = FirstNonEmpty(profile?.Billing_address?.Line2, display?.AddressLine2),
                Line3 = profile?.Billing_address?.Line3,
                City = FirstNonEmpty(profile?.Billing_address?.City, display?.City, "NA"),
                Postal_code = FirstNonEmpty(profile?.Billing_address?.Postal_code, display?.PostalCode, "00000"),
                State_code = stateCode,
                Country_code = FirstNonEmpty(profile?.Billing_address?.Country_code, display?.CountryCode, "MYS")
            };
        }

        return true;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }
}
