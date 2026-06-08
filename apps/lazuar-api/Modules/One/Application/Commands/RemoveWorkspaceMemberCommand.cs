using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record RemoveWorkspaceMemberCommand(Guid OrganizationId, Guid RequesterUserId, Guid TargetUserId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RemoveWorkspaceMemberCommandHandler : ICommandHandler<RemoveWorkspaceMemberCommand>
{
    private readonly IOneRepository _repository;

    public RemoveWorkspaceMemberCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RemoveWorkspaceMemberCommand request, CancellationToken ct)
    {
        var hasAdminAccess = await _repository.HasMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        if (!hasAdminAccess) throw new InvalidOperationException("Unauthorized to remove users.");

        var membership = await _repository.GetMembershipAsync(request.TargetUserId, request.OrganizationId, ct);
        if (membership == null) throw new InvalidOperationException("User is not a member of this workspace.");

        _repository.RemoveTenantMembership(membership);
        await _repository.SaveChangesAsync(ct);
    }
}
