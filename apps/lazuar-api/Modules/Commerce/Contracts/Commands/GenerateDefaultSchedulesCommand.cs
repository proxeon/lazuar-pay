using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record GenerateDefaultSchedulesCommand(Guid OrganizationId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
