using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record InviteUserToWorkspaceCommand(Guid OrganizationId, Guid InviterUserId, string Email, string Role) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class InviteUserToWorkspaceCommandHandler : ICommandHandler<InviteUserToWorkspaceCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;

    public InviteUserToWorkspaceCommandHandler(IOneRepository repository, ITokenGeneratorService tokenGenerator)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Guid> Handle(InviteUserToWorkspaceCommand request, CancellationToken ct)
    {
        var hasAdminAccess = await _repository.HasMembershipAsync(request.InviterUserId, request.OrganizationId, ct);
        if (!hasAdminAccess) throw new InvalidOperationException("Unauthorized to invite users.");

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
            request.Role, 
            token.TokenHash, 
            token.PlainToken, 
            expiry);

        _repository.AddWorkspaceInvitation(invitation);
        await _repository.SaveChangesAsync(ct);

        return invitation.Id;
    }
}
