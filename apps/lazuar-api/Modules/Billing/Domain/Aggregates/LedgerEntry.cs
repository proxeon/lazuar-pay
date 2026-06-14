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
    public string? TaxInvoiceId { get; private set; }
    public string? LhdnValidationStatus { get; private set; }
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

    public void UpdateLhdnStatus(string? taxInvoiceId, string status)
    {
        TaxInvoiceId = taxInvoiceId;
        LhdnValidationStatus = status;
    }

    public void ValidateBalanced()
    {
        var netBaseAmount = _lines.Sum(l => l.BaseCurrencyAmount);
        if (netBaseAmount != 0)
        {
            throw new InvalidOperationException($"Ledger entry {Id} is unbalanced. Net base currency amount: {netBaseAmount}");
        }
    }
}
