using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record MarkCheckoutAsPaidOfflineCommand(Guid OrganizationId, Guid SessionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
