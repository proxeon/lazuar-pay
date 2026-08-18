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
    
    /// <summary>
    /// Flags documents submitted via sandbox API keys. Used by the UI to filter out 
    /// test documents from production accounting ledgers.
    /// </summary>
    public bool IsTestMode { get; private set; }
    
    public DateTime? ValidatedAt { get; private set; }
    public DateTime? NextPollAt { get; private set; }
    public int PollAttempts { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TaxDocument() { }
#pragma warning restore CS8618

    public TaxDocument(Guid organizationId, string internalReferenceId, string documentHash, string rawXmlContent, bool isTestMode = false)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        InternalReferenceId = internalReferenceId;
        DocumentHash = documentHash;
        RawXmlContent = rawXmlContent;
        ValidationStatus = "PENDING";
        IsTestMode = isTestMode;
        PollAttempts = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsSubmitted(string submissionUid, string? lhdnUuid)
    {
        SubmissionUid = submissionUid;
        LhdnUuid = lhdnUuid;
        ValidationStatus = "SUBMITTED";
        PollAttempts = 0;
        NextPollAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DelayPendingSubmission(int delaySeconds)
    {
        NextPollAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Temporarily push <see cref="NextPollAt"/> forward so concurrent workers skip this row
    /// while gateway I/O is in flight (pair with FOR UPDATE SKIP LOCKED claim).
    /// </summary>
    public void ClaimProcessingLease(DateTime leaseUntilUtc)
    {
        NextPollAt = leaseUntilUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ScheduleNextPoll(int? explicitDelaySeconds = null)
    {
        PollAttempts++;
        if (explicitDelaySeconds.HasValue)
        {
            NextPollAt = DateTime.UtcNow.AddSeconds(explicitDelaySeconds.Value);
        }
        else
        {
            var secondsToWait = Math.Pow(3, Math.Min(PollAttempts, 10));
            NextPollAt = DateTime.UtcNow.AddSeconds(secondsToWait);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsValid(string longId, string? lhdnUuid = null)
    {
        LongId = longId;
        if (!string.IsNullOrWhiteSpace(lhdnUuid))
            LhdnUuid = lhdnUuid;
        ValidationStatus = "VALID";
        ValidatedAt = DateTime.UtcNow;
        NextPollAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsInvalid(string error)
    {
        ValidationStatus = "INVALID";
        ErrorMessage = error;
        NextPollAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        ValidationStatus = "FAILED";
        ErrorMessage = error;
        NextPollAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureCanCancel()
    {
        if (ValidationStatus != "VALID" || string.IsNullOrEmpty(LhdnUuid))
        {
            throw new InvalidOperationException($"Cannot cancel document. Current status is {ValidationStatus}. Document must be VALID before cancellation.");
        }

        CheckRule(new CancelWindowMustBeValidRule(ValidatedAt));
    }

    public void Cancel()
    {
        EnsureCanCancel();
        ValidationStatus = "CANCELLED";
        NextPollAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
