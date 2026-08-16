using System;

namespace Modules.Billing.Contracts;

/// <summary>
/// Per-org commercial document prefixes. Year is baked into the sequence prefix
/// (<c>RCPT-2026</c> → <c>RCPT-2026-00001</c>). LHDN UUID is never a series value.
/// </summary>
public static class DocumentSeries
{
    public const string Receipt = "RCPT";
    public const string Quote = "QT";
    public const string Invoice = "INV";
    public const string CreditNote = "CN";

    public static string Prefix(string series, DateTime? utcNow = null) =>
        $"{series}-{(utcNow ?? DateTime.UtcNow):yyyy}";

    public static string ReceiptPrefix(DateTime? utcNow = null) => Prefix(Receipt, utcNow);
    public static string QuotePrefix(DateTime? utcNow = null) => Prefix(Quote, utcNow);
    public static string InvoicePrefix(DateTime? utcNow = null) => Prefix(Invoice, utcNow);
    public static string CreditNotePrefix(DateTime? utcNow = null) => Prefix(CreditNote, utcNow);

    public static bool StartsWithSeries(string? number, string series) =>
        !string.IsNullOrWhiteSpace(number)
        && number.StartsWith(series + "-", StringComparison.OrdinalIgnoreCase);

    public static bool IsReceiptNumber(string? number) => StartsWithSeries(number, Receipt);
    public static bool IsQuoteNumber(string? number) => StartsWithSeries(number, Quote);
    public static bool IsInvoiceNumber(string? number) => StartsWithSeries(number, Invoice);
    public static bool IsCreditNoteNumber(string? number) => StartsWithSeries(number, CreditNote);

    public static bool LooksLikeGuid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _);

    /// <summary>Customer-facing "No:" — never a raw UUID.</summary>
    public static string CustomerFacingNumber(string? customerDocumentNumber, string? taxInvoiceId)
    {
        if (!string.IsNullOrWhiteSpace(customerDocumentNumber))
            return customerDocumentNumber;

        if (!string.IsNullOrWhiteSpace(taxInvoiceId) && !LooksLikeGuid(taxInvoiceId))
            return taxInvoiceId;

        return "PENDING";
    }
}
