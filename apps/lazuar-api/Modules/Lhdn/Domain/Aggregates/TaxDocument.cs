using System;
using BuildingBlocks.Domain;
using Modules.Lhdn.Domain.Rules;

namespace Modules.Lhdn.Domain.Aggregates;

public class TaxDocument : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string InternalReferenceId { get; private set; }
    public string DocumentHash { get; private set; }
    public string? LhdnUuid { get; private set; }
    public string? LongId { get; private set; }
    public string ValidationStatus { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TaxDocument() { }
#pragma warning restore CS8618

    public TaxDocument(Guid organizationId, string internalReferenceId, string documentHash)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        InternalReferenceId = internalReferenceId;
        DocumentHash = documentHash;
        ValidationStatus = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSubmitted()
    {
        ValidationStatus = "SUBMITTED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsValid(string lhdnUuid, string longId)
    {
        LhdnUuid = lhdnUuid;
        LongId = longId;
        ValidationStatus = "VALID";
        ValidatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsInvalid()
    {
        ValidationStatus = "INVALID";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        CheckRule(new CancelWindowMustBeValidRule(ValidatedAt));
        ValidationStatus = "CANCELLED";
        UpdatedAt = DateTime.UtcNow;
    }
}
