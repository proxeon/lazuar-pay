using BuildingBlocks.Domain;

namespace Modules.One.Domain.Events;

public record WorkspaceInvitationCreatedDomainEvent(Guid InvitationId, Guid OrganizationId, string Email, string Role, string PlainToken) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
