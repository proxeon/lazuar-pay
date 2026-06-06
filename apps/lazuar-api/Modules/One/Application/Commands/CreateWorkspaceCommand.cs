using BuildingBlocks.Application;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record CreateWorkspaceCommand(Guid CreatorUserId, string Name, string Slug) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateWorkspaceCommandHandler : ICommandHandler<CreateWorkspaceCommand, Guid>
{
    private readonly IOneRepository _repository;

    public CreateWorkspaceCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateWorkspaceCommand request, CancellationToken ct)
    {
        var organization = new Organization(request.Name, request.Slug);
        _repository.AddOrganization(organization);

        var membership = new TenantMembership(request.CreatorUserId, organization.Id, "ADMIN");
        _repository.AddTenantMembership(membership);

        await _repository.SaveChangesAsync(ct);
        return organization.Id;
    }
}
