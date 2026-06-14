using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Entities;

public class LedgerLine : Entity
{
    public Guid Id { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public string AccountType { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public decimal BaseCurrencyAmount { get; private set; }
    public string BaseCurrency { get; private set; }

#pragma warning disable CS8618
    private LedgerLine() { }
#pragma warning restore CS8618

    internal LedgerLine(Guid ledgerEntryId, string accountType, decimal amount, string currency, decimal baseCurrencyAmount, string baseCurrency)
    {
        Id = Guid.CreateVersion7();
        LedgerEntryId = ledgerEntryId;
        AccountType = accountType;
        Amount = amount;
        Currency = currency;
        BaseCurrencyAmount = baseCurrencyAmount;
        BaseCurrency = baseCurrency;
    }
}
