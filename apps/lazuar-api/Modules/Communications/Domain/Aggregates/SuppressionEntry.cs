using System;
using BuildingBlocks.Domain;

namespace Modules.Communications.Domain.Aggregates;

/// <summary>
/// A recipient that must not receive email: unsubscribed, hard-bounced, or complained.
/// Org-scoped. The (OrganizationId, Email) pair is unique so repeated webhook events
/// don't duplicate rows.
/// </summary>
public class SuppressionEntry : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Normalized lowercase email.</summary>
    public string Email { get; private set; }

    /// <summary>UNSUBSCRIBE, BOUNCE, or COMPLAINT.</summary>
    public string Reason { get; private set; }

    /// <summary>Free-text provenance (e.g. "resend_webhook", "unsubscribe_link").</summary>
    public string? Source { get; private set; }

    public DateTime CreatedAt { get; private set; }

    #pragma warning disable CS8618
    private SuppressionEntry() { }
    #pragma warning restore CS8618

    public SuppressionEntry(Guid organizationId, string email, string reason, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.");

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Email = email.Trim().ToLowerInvariant();
        Reason = reason;
        Source = source;
        CreatedAt = DateTime.UtcNow;
    }
}
