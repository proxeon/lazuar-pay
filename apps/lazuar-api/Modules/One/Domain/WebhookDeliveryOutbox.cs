// apps/lazuar-api/Modules/One/Domain/WebhookDeliveryOutbox.cs
using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class WebhookDeliveryOutbox : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid EndpointId { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string Status { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private WebhookDeliveryOutbox() { }
#pragma warning restore CS8618

    public WebhookDeliveryOutbox(Guid organizationId, Guid endpointId, string eventType, string payload)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        EndpointId = endpointId;
        EventType = eventType;
        Payload = payload;
        AttemptCount = 0;
        NextAttemptAt = DateTime.UtcNow;
        Status = "PENDING";
        CreatedAt = DateTime.UtcNow;
    }

    public void RecordSuccess()
    {
        Status = "SUCCESS";
        AttemptCount++;
    }

    public void RecordFailure(string error)
    {
        AttemptCount++;
        LastError = error;
        if (AttemptCount >= 5)
        {
            Status = "FAILED";
        }
        else
        {
            NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, AttemptCount));
        }
    }

    public void ResetForRetry()
    {
        AttemptCount = 0;
        Status = "PENDING";
        NextAttemptAt = DateTime.UtcNow;
    }
}
