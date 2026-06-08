using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ArchiveWorkspaceCommand(Guid OrganizationId, Guid RequesterUserId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ArchiveWorkspaceCommandHandler : ICommandHandler<ArchiveWorkspaceCommand>
{
    private readonly IOneRepository _repository;

    public ArchiveWorkspaceCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ArchiveWorkspaceCommand request, CancellationToken ct)
    {
        var membership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
        if (membership == null || membership.Role != "ADMIN")
        {
            throw new InvalidOperationException("Unauthorized to archive workspace.");
        }

        var organization = await _repository.GetOrganizationByIdAsync(request.OrganizationId, ct);
        if (organization == null || !organization.IsActive)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        organization.Archive();

        await _repository.SaveChangesAsync(ct);
    }
}
