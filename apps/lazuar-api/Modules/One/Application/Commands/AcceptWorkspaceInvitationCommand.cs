using BuildingBlocks.Application;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record AcceptWorkspaceInvitationCommand(Guid UserId, string Token) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class AcceptWorkspaceInvitationCommandHandler : ICommandHandler<AcceptWorkspaceInvitationCommand>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IAuditRecorder? _auditRecorder;

    public AcceptWorkspaceInvitationCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _auditRecorder = auditRecorder;
    }

    public async Task Handle(AcceptWorkspaceInvitationCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid user session.");

        var inputHash = _tokenGenerator.HashToken(request.Token);
        var invitation = await _repository.GetInvitationByHashAsync(inputHash, ct);

        if (invitation == null || invitation.Status != "PENDING" || invitation.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Invitation is invalid or expired.");

        if (user.Email != invitation.Email)
            throw new InvalidOperationException("This invitation belongs to a different email address.");

        if (await _repository.HasMembershipAsync(user.Id, invitation.OrganizationId, ct))
        {
            invitation.Accept();
            await _repository.SaveChangesAsync(ct);
            throw new InvalidOperationException("User is already a member of this workspace.");
        }

        invitation.Accept();

        var membership = new TenantMembership(user.Id, invitation.OrganizationId, invitation.Role);
        _repository.AddTenantMembership(membership);

        await _repository.SaveChangesAsync(ct);

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                invitation.OrganizationId,
                "member.accepted",
                "invitation",
                invitation.Id.ToString(),
                new { email = user.Email, role = invitation.Role },
                user.Id,
                user.Email,
                ct);
        }
    }
}
