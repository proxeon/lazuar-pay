using BuildingBlocks.Domain;

namespace Modules.Messaging.Domain;

public class TenantReplica : Entity, IAggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public bool IsActive { get; private set; }

    private TenantReplica()
    {
        // Private constructor required for Entity Framework
    }

    public TenantReplica(Guid id, string name, string slug, bool isActive)
    {
        Id = id;
        Name = name;
        Slug = slug;
        IsActive = isActive;
    }

    public void Update(string name, string slug, bool isActive)
    {
        Name = name;
        Slug = slug;
        IsActive = isActive;
    }
}
