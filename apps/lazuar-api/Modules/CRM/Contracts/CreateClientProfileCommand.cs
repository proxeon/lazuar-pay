using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.CRM.Contracts;

public record CreateClientProfileCommand(
    Guid OrganizationId,
    string FullName,
    string Email,
    string Phone,
    string? Tin = null,
    string? IdType = null,
    string? IdValue = null,
    BillingAddressDto? BillingAddress = null,
    Guid? GlobalUserId = null,
    bool ConsentedToMarketing = false) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
