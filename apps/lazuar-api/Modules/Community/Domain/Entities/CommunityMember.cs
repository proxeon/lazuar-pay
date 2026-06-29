using System;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Entities;

public class CommunityMember : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid CommunitySpaceId { get; private set; }
    public Guid ClientProfileId { get; private set; }
    public string Status { get; private set; }
    public DateTime JoinedAt { get; private set; }

#pragma warning disable CS8618
    private CommunityMember() { }
#pragma warning restore CS8618

    public CommunityMember(Guid organizationId, Guid communitySpaceId, Guid clientProfileId, string status)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        CommunitySpaceId = communitySpaceId;
        ClientProfileId = clientProfileId;
        Status = status;
        JoinedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
