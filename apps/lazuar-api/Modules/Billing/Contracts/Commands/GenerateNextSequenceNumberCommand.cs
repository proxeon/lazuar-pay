using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record GenerateNextSequenceNumberCommand(Guid OrganizationId, string Prefix) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
