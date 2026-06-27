using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.CRM.Contracts;

public record ResolveClientProfileCommand(
    Guid OrganizationId,
    string FullName,
    string Email,
    string Phone,
    string? Tin = null,
    string? IdType = null,
    string? IdValue = null,
    BillingAddressDto? BillingAddress = null
) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
