using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;

namespace Modules.Payments.Application.Queries.Agent;

[AgentTool("Check the current payment gateway configurations for the workspace.", "CORE", "low", "SUPER_ADMIN", "ADMIN")]
public record GetPaymentConfigAgentQuery(Guid OrganizationId) : IQuery<AgentPaymentConfigResult>;

public record AgentPaymentConfigResult(string[] ConfiguredGateways);

public class GetPaymentConfigAgentQueryHandler : IQueryHandler<GetPaymentConfigAgentQuery, AgentPaymentConfigResult>
{
    private readonly ITenantPaymentConfigRepository _repository;

    public GetPaymentConfigAgentQueryHandler(ITenantPaymentConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task<AgentPaymentConfigResult> Handle(GetPaymentConfigAgentQuery request, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllByTenantIdAsync(request.OrganizationId, cancellationToken);
        var gateways = configs
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.ApiKey))
            .Select(c => c.GatewayType)
            .ToArray();

        return new AgentPaymentConfigResult(gateways);
    }
}
