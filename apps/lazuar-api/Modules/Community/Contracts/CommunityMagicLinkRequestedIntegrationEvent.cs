using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record CommunityMagicLinkRequestedIntegrationEvent(
    Guid OrganizationId,
    Guid ClientProfileId,
    string MagicLinkUrl) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
