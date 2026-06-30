using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Billing.Contracts.Commands;

public record UpdateTenantBillingProfileCommand(
    Guid OrganizationId,
    string LegalName,
    string Tin,
    string? RegistrationNumber,
    string? SstRegistrationNumber,
    string? LogoUrl,
    TenantBillingAddressDto? Address) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
