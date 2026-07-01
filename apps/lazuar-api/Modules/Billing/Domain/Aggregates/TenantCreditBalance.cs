using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;
using Modules.Billing.Domain.Entities;

namespace Modules.Billing.Domain.Aggregates;

/// <summary>
/// The wallet aggregate tracking prepaid usage credits for external API consumption.
/// </summary>
public class TenantCreditBalance : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    
    /// <summary>
    /// Current available credits. Deduction throws on insufficient balance; the wallet's
    /// xmin system column provides optimistic concurrency (configured in BillingDbContext)
    /// so concurrent deductions cannot overdraw the wallet.
    /// </summary>
    public int AvailableCredits { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<CreditLedger> _transactions = new();
    public IReadOnlyCollection<CreditLedger> Transactions => _transactions.AsReadOnly();

#pragma warning disable CS8618
    private TenantCreditBalance() { }
#pragma warning restore CS8618

    public TenantCreditBalance(Guid organizationId)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        AvailableCredits = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TopUp(int credits, string reference)
    {
        if (credits <= 0) throw new ArgumentException("Top up amount must be positive.");
        
        AvailableCredits += credits;
        _transactions.Add(new CreditLedger(Id, credits, reference));
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deduct(int credits, string reference)
    {
        if (credits <= 0) throw new ArgumentException("Deduction amount must be positive.");

        if (AvailableCredits < credits)
            throw new BusinessRuleValidationException(
                new GenericBusinessRule($"402: Insufficient credits. Available: {AvailableCredits}, requested: {credits}."));

        AvailableCredits -= credits;
        _transactions.Add(new CreditLedger(Id, -credits, reference));
        UpdatedAt = DateTime.UtcNow;
    }
}
