using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands;

public record CreateCommunitySpaceCommand(
    Guid OrganizationId,
    List<Guid> ProductIds,
    string Name,
    string? TelegramLink,
    string? ZoomLink) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateCommunitySpaceCommandHandler : ICommandHandler<CreateCommunitySpaceCommand, Guid>
{
    private readonly ICommunitySpaceRepository _repository;

    public CreateCommunitySpaceCommandHandler(ICommunitySpaceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateCommunitySpaceCommand request, CancellationToken ct)
    {
        var space = new CommunitySpace(
            request.OrganizationId,
            request.ProductIds,
            request.Name,
            request.TelegramLink,
            request.ZoomLink
        );

        _repository.Add(space);
        await _repository.SaveChangesAsync(ct);

        return space.Id;
    }
}
