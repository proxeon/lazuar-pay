using BuildingBlocks.Application;

namespace Modules.Payments.Application.Commands;

public record ProcessGatewayWebhookCommand(
    Guid TenantId,
    string GatewayType,
    string RawBody,
    Dictionary<string, string> Headers) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
