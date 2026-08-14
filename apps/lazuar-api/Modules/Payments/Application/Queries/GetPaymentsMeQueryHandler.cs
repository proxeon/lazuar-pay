using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Application.Queries;

public class GetPaymentsMeQueryHandler : IQueryHandler<GetPaymentsMeQuery, PaymentsMeResult>
{
    private readonly ITenantPaymentConfigRepository _configs;

    public GetPaymentsMeQueryHandler(ITenantPaymentConfigRepository configs)
    {
        _configs = configs;
    }

    public async Task<PaymentsMeResult> Handle(GetPaymentsMeQuery request, CancellationToken cancellationToken)
    {
        var configs = await _configs.GetAllByTenantIdAsync(request.OrganizationId, cancellationToken);
        var active = configs
            .Where(c => c.IsActive && !string.IsNullOrWhiteSpace(c.ApiKey))
            .ToList();

        var names = active
            .Select(c => c.GatewayType.Trim().ToUpperInvariant())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

        return new PaymentsMeResult(
            request.OrganizationId,
            request.OrganizationId,
            request.IsTestMode,
            request.CredentialId,
            request.KeyName,
            request.Scopes,
            active.Count > 0,
            names);
    }
}
