using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Entities;

/// <summary>
/// Child of the org-scoped wallet. No OrganizationId — query only via
/// TenantCreditBalanceId from a tenant-filtered wallet.
/// </summary>
public class CreditLedger : Entity
{
    public Guid Id { get; private set; }
    public Guid TenantCreditBalanceId { get; private set; }
    public int Amount { get; private set; }
    public string Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private CreditLedger() { }
#pragma warning restore CS8618

    internal CreditLedger(Guid tenantCreditBalanceId, int amount, string reference)
    {
        Id = Guid.CreateVersion7();
        TenantCreditBalanceId = tenantCreditBalanceId;
        Amount = amount;
        Reference = reference;
        CreatedAt = DateTime.UtcNow;
    }
}
