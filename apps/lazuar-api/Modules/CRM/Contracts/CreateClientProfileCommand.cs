using System;
using BuildingBlocks.Application;

namespace Modules.CRM.Contracts;

public record CreateClientProfileCommand(
    Guid OrganizationId,
    string FullName,
    string Email,
    string Phone,
    Guid? GlobalUserId = null) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
