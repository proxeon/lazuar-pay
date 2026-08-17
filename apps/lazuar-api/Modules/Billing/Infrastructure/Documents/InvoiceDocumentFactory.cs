using System;
using System.Collections.Generic;
using System.Linq;
using Modules.Billing.Contracts;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Domain.ValueObjects;
using Modules.Commerce.Contracts;
using Modules.One.Contracts;

namespace Modules.Billing.Infrastructure.Documents;

public static class InvoiceDocumentFactory
{
    public static InvoiceDocumentModel CreateHeader(
        string documentType,
        string invoiceNumber,
        DateTime issueDate,
        TenantBillingProfile? profile,
        WorkspaceSnapshotDto? workspace,
        CommerceCustomerDisplay? customer,
        byte[]? logoBytes,
        string? lhdnUuid = null,
        string? lhdnQrLink = null)
    {
        return new InvoiceDocumentModel
        {
            DocumentType = documentType,
            InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? "PENDING" : invoiceNumber,
            IssueDate = issueDate,
            CompanyName = FirstNonEmpty(profile?.LegalName, workspace?.Name, "Merchant"),
            CompanyTin = NullIfWhiteSpace(profile?.Tin) ?? "",
            Notes = OfficialReceiptDisclaimer(documentType),
            CompanyRegistrationNumber = NullIfWhiteSpace(profile?.RegistrationNumber),
            CompanySstNumber = NullIfWhiteSpace(profile?.SstRegistrationNumber),
            CompanyAddress = FormatSellerAddress(profile?.Address),
            CompanyLogo = logoBytes,
            CustomerName = FirstNonEmpty(customer?.CompanyName, customer?.Name, "Customer"),
            CustomerEmail = customer?.Email ?? "",
            CustomerTin = NullIfWhiteSpace(customer?.Tin),
            CustomerCompanyName = NullIfWhiteSpace(customer?.CompanyName),
            CustomerAddress = FormatBuyerAddress(customer),
            LhdnUuid = lhdnUuid,
            LhdnQrLink = lhdnQrLink
        };
    }

    public static string FormatSellerAddress(TenantBillingAddress? address)
    {
        if (address == null) return "";

        return JoinLines(
            address.Line1,
            address.Line2,
            address.Line3,
            JoinInline(address.PostalCode, address.City),
            address.StateCode);
    }

    public static string FormatBuyerAddress(CommerceCustomerDisplay? customer)
    {
        if (customer == null) return "";

        return JoinLines(
            customer.AddressLine1,
            customer.AddressLine2,
            JoinInline(customer.PostalCode, customer.City),
            customer.StateCode);
    }

    private static string JoinLines(params string?[] parts) =>
        string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p))!);

    private static string JoinInline(params string?[] parts) =>
        string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))!);

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static string? OfficialReceiptDisclaimer(string documentType)
    {
        if (string.Equals(documentType, "Official Receipt", StringComparison.OrdinalIgnoreCase))
        {
            return "This Official Receipt confirms payment. It is not a validated MyInvois tax invoice.";
        }

        if (string.Equals(documentType, "Invoice", StringComparison.OrdinalIgnoreCase))
        {
            return "This invoice is pending MyInvois validation. It is not a validated tax invoice.";
        }

        return null;
    }
}
