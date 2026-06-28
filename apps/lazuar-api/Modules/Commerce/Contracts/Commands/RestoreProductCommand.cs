using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record RestoreProductCommand(Guid OrganizationId, Guid ProductId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
