using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record CreateSaasCheckoutCommand(Guid OrganizationId, string ReturnUrl) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
