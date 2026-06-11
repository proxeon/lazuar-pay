using BuildingBlocks.Domain;
using Modules.One.Domain.Events;

namespace Modules.One.Domain;

public class WorkspaceInvitation : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; private set; }
    public string Role { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private WorkspaceInvitation() { }
#pragma warning restore CS8618

    public WorkspaceInvitation(Guid organizationId, string email, string role, string tokenHash, string plainToken, DateTime expiresAt)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Email = email.Trim().ToLowerInvariant();
        Role = role.ToUpperInvariant();
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        Status = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new WorkspaceInvitationCreatedDomainEvent(Id, OrganizationId, Email, Role, plainToken));
    }

    public void Accept()
    {
        if (Status != "PENDING") throw new InvalidOperationException("Invitation is no longer pending.");
        if (DateTime.UtcNow > ExpiresAt) throw new InvalidOperationException("Invitation has expired.");

        Status = "ACCEPTED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status = "REVOKED";
        UpdatedAt = DateTime.UtcNow;
    }
}
