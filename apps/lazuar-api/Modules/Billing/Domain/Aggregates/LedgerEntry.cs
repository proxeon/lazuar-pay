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

    private readonly List<LedgerLine> _lines = new();
    public IReadOnlyCollection<LedgerLine> Lines => _lines.AsReadOnly();

    private LedgerEntry() { }

    public LedgerEntry(Guid organizationId, string referenceType, string referenceId, string? description = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Timestamp = DateTime.UtcNow;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Description = description;
    }

    public void AddLine(string accountType, decimal amount, string currency, decimal baseCurrencyAmount, string baseCurrency)
    {
        var line = new LedgerLine(Id, accountType, amount, currency, baseCurrencyAmount, baseCurrency);
        _lines.Add(line);
    }

    public void UpdateLhdnStatus(string taxInvoiceId, string status)
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
