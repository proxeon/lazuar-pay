using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Entities;

/// <summary>
/// Idempotency record for credit deductions. The unique index on
/// (OrganizationId, IdempotencyKey) guarantees a retried deduction cannot double-charge.
/// </summary>
public class CreditDeductionIdempotencyLog : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string IdempotencyKey { get; private set; }
    public int Amount { get; private set; }
    public string Reference { get; private set; }
    public DateTime DeductedAt { get; private set; }

    #pragma warning disable CS8618
    private CreditDeductionIdempotencyLog() { }
    #pragma warning restore CS8618

    public CreditDeductionIdempotencyLog(Guid organizationId, string idempotencyKey, int amount, string reference)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("IdempotencyKey is required.");

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Reference = reference;
        DeductedAt = DateTime.UtcNow;
    }
}
