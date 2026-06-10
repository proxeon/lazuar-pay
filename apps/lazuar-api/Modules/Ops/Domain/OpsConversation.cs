using BuildingBlocks.Domain;

namespace Modules.Ops.Domain;

public class OpsConversation : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Title { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private OpsConversation() { }
#pragma warning restore CS8618

    public OpsConversation(Guid id, Guid organizationId, string title)
    {
        Id = id;
        OrganizationId = organizationId;
        Title = title;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        Title = title;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
