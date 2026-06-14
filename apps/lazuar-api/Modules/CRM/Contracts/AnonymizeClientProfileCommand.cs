using System;
using BuildingBlocks.Application;

namespace Modules.CRM.Contracts;

public record AnonymizeClientProfileCommand(Guid OrganizationId, Guid ClientProfileId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
