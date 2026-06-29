using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record DeleteCommunitySpaceCommand(Guid OrganizationId, Guid SpaceId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeleteCommunitySpaceCommandHandler : ICommandHandler<DeleteCommunitySpaceCommand>
{
    private readonly ICommunitySpaceRepository _repository;

    public DeleteCommunitySpaceCommandHandler(ICommunitySpaceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteCommunitySpaceCommand request, CancellationToken ct)
    {
        var space = await _repository.GetByIdAsync(request.OrganizationId, request.SpaceId, ct);

        if (space == null)
        {
            throw new InvalidOperationException("Community space not found.");
        }

        _repository.Remove(space);

        await _repository.SaveChangesAsync(ct);
    }
}
