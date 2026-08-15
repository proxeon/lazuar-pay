using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record RequestPortalMagicLinkCommand(
    string TenantSlug,
    string Email) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
