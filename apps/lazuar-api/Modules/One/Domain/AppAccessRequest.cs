using BuildingBlocks.Domain;
using Modules.One.Domain.Events;

namespace Modules.One.Domain;

public class AppAccessRequest : Entity, IAggregateRoot
{
    public Guid Id { get; private set; }
    public Guid GlobalUserId { get; private set; }
    public List<string> RequestedApps { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private AppAccessRequest() { }
#pragma warning restore CS8618

    public AppAccessRequest(Guid globalUserId, IEnumerable<string> requestedApps)
    {
        Id = Guid.CreateVersion7();
        GlobalUserId = globalUserId;
        RequestedApps = requestedApps.Select(a => a.ToUpperInvariant()).ToList();
        Status = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new AppAccessRequestedDomainEvent(Id, GlobalUserId, RequestedApps));
    }

    public void Approve()
    {
        if (Status != "PENDING") throw new InvalidOperationException("Only pending requests can be approved.");
        Status = "APPROVED";
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new AppAccessApprovedDomainEvent(Id, GlobalUserId, RequestedApps));
    }

    public void Reject()
    {
        if (Status != "PENDING") throw new InvalidOperationException("Only pending requests can be rejected.");
        Status = "REJECTED";
        UpdatedAt = DateTime.UtcNow;
    }
}
