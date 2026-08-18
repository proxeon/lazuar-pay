using System;
using System.Linq;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using Modules.Lhdn.Domain;
using Modules.CRM.Contracts;
using Modules.Commerce.Contracts;

namespace Modules.Lhdn.Infrastructure.Services;

internal static class LhdnBuyerMapper
{
    public static bool IsStubTin(string? tin) => MyInvoisBuyerRules.IsStubTin(tin);

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

        // Checkout snapshot wins over a live CRM row that may belong to another
        // buyer who previously used the same inbox.
        var tin = FirstNonEmpty(display?.Tin, profile?.Tin);
        if (string.IsNullOrWhiteSpace(tin) || IsStubTin(tin))
            return false;

        buyerTin = tin;
        buyerName = FirstNonEmpty(display?.CompanyName, display?.Name, profile?.Company_name, profile?.Full_name, "Customer");

        var rawIdType = FirstNonEmpty(display?.IdType, profile?.Id_type);
        idType = rawIdType?.ToUpperInvariant() switch
        {
            "NRIC" => SubmitDocumentRequestDtoBuyer_id_type.NRIC,
            "PASSPORT" => SubmitDocumentRequestDtoBuyer_id_type.PASSPORT,
            "ARMY" => SubmitDocumentRequestDtoBuyer_id_type.ARMY,
            _ => SubmitDocumentRequestDtoBuyer_id_type.BRN
        };
        idValue = FirstNonEmpty(display?.IdValue, profile?.Id_value);
        if (string.IsNullOrWhiteSpace(idValue) || string.Equals(idValue, "NA", StringComparison.OrdinalIgnoreCase))
            return false;

        var line1 = FirstNonEmpty(display?.AddressLine1, profile?.Billing_address?.Line1);
        if (!string.IsNullOrWhiteSpace(line1))
        {
            var state = FirstNonEmpty(display?.StateCode, profile?.Billing_address?.State_code) ?? "17";
            if (!Enum.TryParse<LhdnAddressDtoState_code>("_" + state, out var stateCode))
                stateCode = LhdnAddressDtoState_code._17;

            address = new LhdnAddressDto
            {
                Line1 = line1,
                Line2 = FirstNonEmpty(display?.AddressLine2, profile?.Billing_address?.Line2),
                Line3 = profile?.Billing_address?.Line3,
                City = FirstNonEmpty(display?.City, profile?.Billing_address?.City, "NA"),
                Postal_code = FirstNonEmpty(display?.PostalCode, profile?.Billing_address?.Postal_code, "00000"),
                State_code = stateCode,
                Country_code = Iso3166Country.NormalizeToAlpha3(
                    FirstNonEmpty(display?.CountryCode, profile?.Billing_address?.Country_code, "MYS"))
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
