// apps/lazuar-api/Modules/One/Application/Commands/RemoveWorkspaceMemberCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;

namespace Modules.One.Application.Commands;

[AgentTool("Revoke staff or admin access from the workspace.", "CORE", "high", "SUPER_ADMIN", "ADMIN")]
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
        var requesterMembership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        if (requesterMembership == null || requesterMembership.Role != "ADMIN")
            throw new InvalidOperationException("Unauthorized to remove users.");

        var membership = await _repository.GetMembershipAsync(request.TargetUserId, request.OrganizationId, ct);
        if (membership == null) throw new InvalidOperationException("User is not a member of this workspace.");

        _repository.RemoveTenantMembership(membership);
        await _repository.SaveChangesAsync(ct);
    }
}
