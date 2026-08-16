using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RevokeWorkspaceInvitationCommand(Guid OrganizationId, Guid RequesterUserId, Guid InvitationId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RevokeWorkspaceInvitationCommandHandler : ICommandHandler<RevokeWorkspaceInvitationCommand>
{
    private readonly IOneRepository _repository;

    public RevokeWorkspaceInvitationCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RevokeWorkspaceInvitationCommand request, CancellationToken ct)
    {
        var membership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        var requester = await _repository.GetUserByIdAsync(request.RequesterUserId, ct);
        if (!WorkspaceStaffRoles.CanManageMembers(membership?.Role) && requester?.IsSystemAdmin != true)
        {
            throw new InvalidOperationException("Unauthorized to manage invitations.");
        }

        var invitation = await _repository.GetInvitationByIdAsync(request.InvitationId, ct);
        if (invitation == null || invitation.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Invitation not found.");
        }

        if (invitation.Status != "PENDING")
        {
            throw new InvalidOperationException("Only pending invitations can be revoked.");
        }

        invitation.Revoke();

        await _repository.SaveChangesAsync(ct);
    }
}
