using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Aggregates;

public class DeveloperApiKey : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; }
    public string Prefix { get; private set; }
    public string KeyHash { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private DeveloperApiKey() { }
#pragma warning restore CS8618

    public DeveloperApiKey(Guid organizationId, string name, string prefix, string keyHash)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        Prefix = prefix;
        KeyHash = keyHash;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsActive = false;
    }
}
