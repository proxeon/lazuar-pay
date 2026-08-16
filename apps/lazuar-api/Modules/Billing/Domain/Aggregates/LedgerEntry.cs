using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;
using Modules.Billing.Domain.Entities;

namespace Modules.Billing.Domain.Aggregates;

public class LedgerEntry : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public DateTime Timestamp { get; private set; }
    public string ReferenceType { get; private set; }
    public string ReferenceId { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// Legacy dual-use field (receipt #, LHDN UUID, consolidation ref). Prefer
    /// <see cref="CustomerDocumentNumber"/> and <see cref="LhdnDocumentUuid"/> for new code.
    /// Kept for back-compat with existing rows and LHDN correlation.
    /// </summary>
    public string? TaxInvoiceId { get; private set; }

    /// <summary>Immutable customer-facing receipt / invoice number (never overwritten by LHDN).</summary>
    public string? CustomerDocumentNumber { get; private set; }

    /// <summary>MyInvois document UUID once submitted/validated.</summary>
    public string? LhdnDocumentUuid { get; private set; }

    /// <summary>LHDN lifecycle status (B2C_RECEIPT, VALID, CANCELLED, CONSOLIDATED_PENDING, …).</summary>
    public string? LhdnValidationStatus { get; private set; }

    /// <summary>B2C consolidation eligibility: PENDING / CONSOLIDATED / NOT_REQUIRED / IGNORED.</summary>
    public string? ConsolidationStatus { get; private set; }

    public string CustomerType { get; private set; }

    private readonly List<LedgerLine> _lines = new();
    public IReadOnlyCollection<LedgerLine> Lines => _lines.AsReadOnly();

#pragma warning disable CS8618
    private LedgerEntry() { }
#pragma warning restore CS8618

    public LedgerEntry(Guid organizationId, string referenceType, string referenceId, string? description = null, string customerType = "B2C")
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Timestamp = DateTime.UtcNow;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Description = description;
        CustomerType = customerType;
    }

    public void AddLine(string accountType, decimal amount, string currency, decimal baseCurrencyAmount, string baseCurrency, string taxTypeCode = "06", string msicCode = "004")
    {
        var line = new LedgerLine(Id, accountType, amount, currency, baseCurrencyAmount, baseCurrency, taxTypeCode, msicCode);
        _lines.Add(line);
    }

    /// <summary>
    /// Assigns the immutable customer receipt number for B2C sales and marks the entry
    /// eligible for monthly LHDN consolidation.
    /// </summary>
    public void AssignB2cReceipt(string receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("Receipt number is required.", nameof(receiptNumber));

        if (CustomerDocumentNumber is null)
            CustomerDocumentNumber = receiptNumber;

        // Keep TaxInvoiceId in sync for legacy readers during transition.
        TaxInvoiceId ??= receiptNumber;
        LhdnValidationStatus = LhdnValidationStatuses.B2cReceipt;
        ConsolidationStatus = ConsolidationStatuses.Pending;
    }

    /// <summary>
    /// Customer-facing Hub SaaS invoice number. Does not mark the row as a tenant B2C receipt
    /// and does not start MyInvois consolidation.
    /// </summary>
    public void AssignPlatformDocumentNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));

        CustomerDocumentNumber ??= invoiceNumber;
        TaxInvoiceId ??= invoiceNumber;
    }

    /// <summary>
    /// Immutable customer-facing commercial number (INV / CN / other). Does not start B2C consolidation.
    /// </summary>
    public void AssignCustomerDocumentNumber(string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("Document number is required.", nameof(documentNumber));

        CustomerDocumentNumber ??= documentNumber;
        TaxInvoiceId ??= documentNumber;
    }

    /// <summary>B2B tax-invoice number allocated on pay, before any MyInvois UUID exists.</summary>
    public void AssignB2bInvoice(string invoiceNumber)
    {
        AssignCustomerDocumentNumber(invoiceNumber);
        MarkConsolidationNotRequired();
    }

    /// <summary>Marks B2B (or other non-consolidatable) sales so the consolidation job skips them.</summary>
    public void MarkConsolidationNotRequired()
    {
        ConsolidationStatus = ConsolidationStatuses.NotRequired;
    }

    public void MarkConsolidationIgnored(string reasonStatus = LhdnValidationStatuses.IgnoredNoRevenue)
    {
        ConsolidationStatus = ConsolidationStatuses.Ignored;
        LhdnValidationStatus = reasonStatus;
    }

    /// <summary>
    /// Marks this B2C receipt as included in a consolidation batch.
    /// Does not overwrite <see cref="CustomerDocumentNumber"/>.
    /// </summary>
    public void MarkConsolidatedPending(string consolidationRef)
    {
        ConsolidationStatus = ConsolidationStatuses.Consolidated;
        LhdnValidationStatus = LhdnValidationStatuses.ConsolidatedPending;
        // Legacy correlation: batch internal ref still stored on TaxInvoiceId for LHDN linkage.
        TaxInvoiceId = consolidationRef;
    }

    /// <summary>
    /// Updates LHDN lifecycle fields only. Never overwrites <see cref="CustomerDocumentNumber"/>.
    /// </summary>
    public void UpdateLhdnStatus(string? lhdnDocumentUuid, string status)
    {
        if (!string.IsNullOrWhiteSpace(lhdnDocumentUuid))
        {
            LhdnDocumentUuid = lhdnDocumentUuid;
            // Legacy dual-use: TaxInvoiceId held UUID after validation for PDF QR.
            TaxInvoiceId = lhdnDocumentUuid;
        }

        LhdnValidationStatus = status;
    }

    // This guarantees that it is impossible for Lazuar to lose track of a
    // single cent.
    //
    // NOTE: Double-entry bookkeeping is a 500-year-old accounting rule: Every
    // financial transaction has equal and opposite reactions. Debits and
    // Credits must always equal zero.
    public void ValidateBalanced()
    {
        var netBaseAmount = _lines.Sum(l => l.BaseCurrencyAmount);
        if (netBaseAmount != 0)
        {
            throw new InvalidOperationException($"Ledger entry {Id} is unbalanced. Net base currency amount: {netBaseAmount}");
        }
    }
}
