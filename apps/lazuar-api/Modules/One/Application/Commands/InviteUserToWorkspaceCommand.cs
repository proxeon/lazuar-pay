// apps/lazuar-api/Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs
using BuildingBlocks.Application;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.Ops.Contracts;

namespace Modules.One.Application.Commands;

[AgentTool("Invite new staff or admins to the current tenant workspace.", "CORE", "medium", "SUPER_ADMIN", "ADMIN")]
public record InviteUserToWorkspaceCommand(Guid OrganizationId, Guid InviterUserId, string Email, string Role) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class InviteUserToWorkspaceCommandHandler : ICommandHandler<InviteUserToWorkspaceCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IAuditRecorder? _auditRecorder;

    public InviteUserToWorkspaceCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _auditRecorder = auditRecorder;
    }

    public async Task<Guid> Handle(InviteUserToWorkspaceCommand request, CancellationToken ct)
    {
        var membership = await _repository.GetMembershipAsync(request.InviterUserId, request.OrganizationId, ct);
        var inviter = await _repository.GetUserByIdAsync(request.InviterUserId, ct);
        var canInvite = WorkspaceStaffRoles.CanManageMembers(membership?.Role) || inviter?.IsSystemAdmin == true;
        if (!canInvite)
            throw new InvalidOperationException("Unauthorized to invite users.");

        var role = WorkspaceStaffRoles.NormalizeInvitedRole(request.Role);

        var pending = await _repository.GetPendingInvitationAsync(request.OrganizationId, request.Email, ct);
        if (pending != null)
            throw new InvalidOperationException("A pending invitation already exists for this email.");

        var existingMember = await _repository.GetUserByEmailAsync(request.Email, ct);
        if (existingMember != null)
        {
            if (await _repository.HasMembershipAsync(existingMember.Id, request.OrganizationId, ct))
                throw new InvalidOperationException("User is already a member of this workspace.");
        }

        var token = _tokenGenerator.GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddDays(7);

        var invitation = new WorkspaceInvitation(
            request.OrganizationId,
            request.Email,
            role,
            token.TokenHash,
            token.PlainToken,
            expiry);

        _repository.AddWorkspaceInvitation(invitation);
        await _repository.SaveChangesAsync(ct);

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                request.OrganizationId,
                "member.invited",
                "invitation",
                invitation.Id.ToString(),
                new { email = invitation.Email, role },
                request.InviterUserId,
                inviter?.Email,
                ct);
        }

        return invitation.Id;
    }
}
