using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

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
        if (membership == null || membership.Role != "ADMIN")
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
