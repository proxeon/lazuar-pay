using System;
using BuildingBlocks.Application;

namespace Modules.Lhdn.Contracts;

/// <summary>
/// Copy merchant stationery (legal name, TIN, address) onto an existing MyInvois config.
/// No-op when the tenant has not created LHDN config yet. Never touches client secret or cert.
/// </summary>
public record SyncSupplierStationeryCommand(
    Guid OrganizationId,
    string LegalName,
    string Tin,
    string? AddressLine1,
    string? City,
    string? State,
    string? Postal,
    string? Country) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
