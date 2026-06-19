using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Aggregates;

public class WebhookSubscription : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Url { get; private set; }
    public string Secret { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private WebhookSubscription() { }
#pragma warning restore CS8618

    public WebhookSubscription(Guid organizationId, string url, string secret)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Url = url;
        Secret = secret;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
