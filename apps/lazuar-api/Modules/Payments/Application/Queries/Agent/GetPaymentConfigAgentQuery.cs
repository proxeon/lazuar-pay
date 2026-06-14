// apps/lazuar-api/Modules/Payments/Application/Queries/Agent/GetPaymentConfigAgentQuery.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;

namespace Modules.Payments.Application.Queries.Agent;

[AgentTool("Check the current payment gateway configuration.", "CORE", "low", "SUPER_ADMIN", "ADMIN")]
public record GetPaymentConfigAgentQuery(Guid OrganizationId) : IQuery<AgentPaymentConfigResult>;

public record AgentPaymentConfigResult(string GatewayType, bool IsActive, bool HasApiKey, bool HasWebhookSecret);

public class GetPaymentConfigAgentQueryHandler : IQueryHandler<GetPaymentConfigAgentQuery, AgentPaymentConfigResult>
{
    private readonly ITenantPaymentConfigRepository _repository;

    public GetPaymentConfigAgentQueryHandler(ITenantPaymentConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task<AgentPaymentConfigResult> Handle(GetPaymentConfigAgentQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetActiveByTenantIdAsync(request.OrganizationId, cancellationToken);

        if (config == null)
        {
            return new AgentPaymentConfigResult("NONE", false, false, false);
        }

        return new AgentPaymentConfigResult(
            config.GatewayType,
            config.IsActive,
            !string.IsNullOrWhiteSpace(config.ApiKey),
            !string.IsNullOrWhiteSpace(config.WebhookSecret));
    }
}
