using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record UpdateWorkspaceCommand(
    Guid OrganizationId,
    Guid RequesterUserId,
    string Name,
    string Slug,
    string? LogoUrl = null,
    string? PrimaryColor = null,
    bool UpdateBranding = false) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateWorkspaceCommandHandler : ICommandHandler<UpdateWorkspaceCommand>
{
    private readonly IOneRepository _repository;

    public UpdateWorkspaceCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateWorkspaceCommand request, CancellationToken ct)
    {
        var membership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        var requester = await _repository.GetUserByIdAsync(request.RequesterUserId, ct);
        var canUpdate = WorkspaceStaffRoles.CanManageMembers(membership?.Role) || requester?.IsSystemAdmin == true;
        if (!canUpdate)
        {
            throw new InvalidOperationException("Unauthorized to update workspace.");
        }

        var organization = await _repository.GetOrganizationByIdAsync(request.OrganizationId, ct);
        if (organization == null || !organization.IsActive)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        organization.UpdateDetails(request.Name, request.Slug);
        if (request.UpdateBranding)
        {
            organization.UpdateBranding(request.LogoUrl, request.PrimaryColor);
        }

        await _repository.SaveChangesAsync(ct);
    }
}
