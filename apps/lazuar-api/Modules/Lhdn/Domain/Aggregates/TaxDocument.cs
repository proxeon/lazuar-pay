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
    public string RawXmlContent { get; private set; }
    public string? LhdnUuid { get; private set; }
    public string? SubmissionUid { get; private set; }
    public string? LongId { get; private set; }
    public string ValidationStatus { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TaxDocument() { }
#pragma warning restore CS8618

    public TaxDocument(Guid organizationId, string internalReferenceId, string documentHash, string rawXmlContent)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        InternalReferenceId = internalReferenceId;
        DocumentHash = documentHash;
        RawXmlContent = rawXmlContent;
        ValidationStatus = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSubmitted(string submissionUid, string? lhdnUuid)
    {
        SubmissionUid = submissionUid;
        LhdnUuid = lhdnUuid;
        ValidationStatus = "SUBMITTED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsValid(string longId)
    {
        LongId = longId;
        ValidationStatus = "VALID";
        ValidatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsInvalid(string error)
    {
        ValidationStatus = "INVALID";
        ErrorMessage = error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        ValidationStatus = "FAILED";
        ErrorMessage = error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        CheckRule(new CancelWindowMustBeValidRule(ValidatedAt));
        ValidationStatus = "CANCELLED";
        UpdatedAt = DateTime.UtcNow;
    }
}
