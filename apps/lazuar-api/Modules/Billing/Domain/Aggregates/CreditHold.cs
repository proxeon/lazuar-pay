using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Aggregates;

/// <summary>
/// Reserves credits for a multi-unit operation (e.g. a broadcast). Credits are moved out of
/// the wallet into the hold on creation, consumed per unit as the operation progresses, and
/// any remainder is released back to the wallet on completion. Prevents overdraw mid-operation.
/// </summary>
public class CreditHold : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public int TotalAmount { get; private set; }
    public int RemainingAmount { get; private set; }

    /// <summary>HELD, SETTLED, RELEASED.</summary>
    public string Status { get; private set; } = "HELD";

    public string CorrelationId { get; private set; }
    public string Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    #pragma warning disable CS8618
    private CreditHold() { }
    #pragma warning restore CS8618

    public CreditHold(Guid organizationId, int amount, string correlationId, string reference)
    {
        if (amount <= 0) throw new ArgumentException("Hold amount must be positive.");
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId is required.");
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Reference is required.");

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        TotalAmount = amount;
        RemainingAmount = amount;
        Status = "HELD";
        CorrelationId = correlationId;
        Reference = reference;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Consume credits for a single unit of work (e.g. one recipient send).</summary>
    public void Consume(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Consume amount must be positive.");
        if (Status != "HELD") throw new InvalidOperationException("Hold is no longer active.");
        if (RemainingAmount < amount)
            throw new BusinessRuleValidationException(
                new GenericBusinessRule($"402: Insufficient held credits. Remaining: {RemainingAmount}, requested: {amount}."));

        RemainingAmount -= amount;
        if (RemainingAmount == 0)
        {
            Status = "SETTLED";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Release remaining credits. RELEASED when remainder was returned; SETTLED when already exhausted.</summary>
    public int ReleaseRemaining()
    {
        if (Status != "HELD") throw new InvalidOperationException("Hold is no longer active.");

        var released = RemainingAmount;
        RemainingAmount = 0;
        Status = released > 0 ? "RELEASED" : "SETTLED";
        UpdatedAt = DateTime.UtcNow;
        return released;
    }
}
