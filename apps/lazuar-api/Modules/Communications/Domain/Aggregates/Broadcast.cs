using System;
using BuildingBlocks.Domain;

namespace Modules.Communications.Domain.Aggregates;

/// <summary>
/// A bulk marketing email send to a tenant's active subscribers. Credits are reserved in a
/// <c>CreditHold</c> up front (TotalRecipients × per-recipient cost) and consumed per
/// recipient as the fan-out worker dispatches; suppressed/failed recipients' credits are
/// released back to the wallet on completion.
/// </summary>
public class Broadcast : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public string Subject { get; private set; }
    public string EmailBody { get; private set; }

    /// <summary>DRAFT, QUEUED, SENDING, COMPLETED, FAILED.</summary>
    public string Status { get; private set; } = "DRAFT";

    public int TotalRecipients { get; private set; }
    public int SentCount { get; private set; }
    public int SuppressedCount { get; private set; }
    public int FailedCount { get; private set; }

    public Guid? CreditHoldId { get; private set; }
    public int CreditsReserved { get; private set; }
    public int CreditsUsed { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    #pragma warning disable CS8618
    private Broadcast() { }
    #pragma warning restore CS8618

    public Broadcast(Guid organizationId, string subject, string emailBody)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.");
        if (string.IsNullOrWhiteSpace(emailBody)) throw new ArgumentException("EmailBody is required.");

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Subject = subject;
        EmailBody = emailBody;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Queue(int totalRecipients, Guid creditHoldId, int creditsReserved)
    {
        if (Status != "DRAFT") throw new InvalidOperationException("Broadcast already queued.");
        TotalRecipients = totalRecipients;
        CreditHoldId = creditHoldId;
        CreditsReserved = creditsReserved;
        Status = "QUEUED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSending()
    {
        if (Status != "QUEUED") throw new InvalidOperationException("Broadcast is not queued.");
        Status = "SENDING";
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordSent(int creditsPerRecipient)
    {
        SentCount++;
        CreditsUsed += creditsPerRecipient;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordSuppressed() => SuppressedCount++;
    public void RecordFailed() => FailedCount++;

    public void MarkCompleted()
    {
        Status = "COMPLETED";
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = "FAILED";
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
