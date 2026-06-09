using BuildingBlocks.Domain;

namespace Modules.One.Domain.Events;

public record PasswordResetRequestedDomainEvent(Guid UserId, string Email, string PlainToken) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
