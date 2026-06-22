using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;

namespace Modules.One.Application.Commands;

public record UpdateWorkspaceCommand(
    Guid OrganizationId, 
    Guid RequesterUserId, 
    string Name, 
    string Slug, 
    bool IsSystemAdmin) : ICommand
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
        if (!request.IsSystemAdmin)
        {
            var membership = await _repository.GetMembershipAsync(request.RequesterUserId, request.OrganizationId, ct);
            if (membership == null || membership.Role != "ADMIN")
            {
                throw new InvalidOperationException("Unauthorized to update workspace.");
            }
        }

        var organization = await _repository.GetOrganizationByIdAsync(request.OrganizationId, ct);
        if (organization == null || !organization.IsActive)
        {
            throw new InvalidOperationException("Workspace not found.");
        }

        var cleanSlug = request.Slug.Trim().ToLowerInvariant();
        if (organization.Slug != cleanSlug)
        {
            var isUnique = await _repository.IsSlugUniqueAsync(cleanSlug, request.OrganizationId, ct);
            if (!isUnique)
            {
                throw new BusinessRuleValidationException(new GenericBusinessRule("This workspace slug is already taken by another organization."));
            }
        }

        organization.UpdateDetails(request.Name, request.Slug);

        await _repository.SaveChangesAsync(ct);
    }
}
