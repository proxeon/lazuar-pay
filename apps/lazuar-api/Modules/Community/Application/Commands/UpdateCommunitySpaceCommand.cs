using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record UpdateCommunitySpaceCommand(
    Guid OrganizationId,
    Guid SpaceId,
    List<Guid> ProductIds,
    string Name,
    string? TelegramLink,
    string? ZoomLink) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateCommunitySpaceCommandHandler : ICommandHandler<UpdateCommunitySpaceCommand>
{
    private readonly ICommunitySpaceRepository _repository;

    public UpdateCommunitySpaceCommandHandler(ICommunitySpaceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateCommunitySpaceCommand request, CancellationToken ct)
    {
        var space = await _repository.GetByIdAsync(request.OrganizationId, request.SpaceId, ct);

        if (space == null)
        {
            throw new InvalidOperationException("Community space not found.");
        }

        space.UpdateDetails(request.Name, request.TelegramLink, request.ZoomLink, request.ProductIds);

        await _repository.SaveChangesAsync(ct);
    }
}
