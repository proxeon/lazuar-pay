using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Queries;
using Modules.Payments.Application.Ports;
using Lazuar.ApiTypes;

namespace Modules.Payments.Infrastructure.Queries;

public class GetPaymentConfigQueryHandler : IQueryHandler<GetPaymentConfigQuery, IEnumerable<PaymentConfigDto>>
{
    private readonly ITenantPaymentConfigRepository _repository;

    public GetPaymentConfigQueryHandler(ITenantPaymentConfigRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<PaymentConfigDto>> Handle(GetPaymentConfigQuery request, CancellationToken ct)
    {
        var configs = await _repository.GetAllByTenantIdAsync(request.OrganizationId, ct);

        return configs.Select(config => new PaymentConfigDto
        {
            Gateway_type = config.GatewayType,
            Api_key = MaskSecret(config.ApiKey),
            Merchant_id = config.MerchantId,
            Webhook_secret = MaskSecret(config.WebhookSecret),
            Secret_key = MaskSecret(config.ApiKey)
        });
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "••••";
        return "••••••••" + value[^4..];
    }
}
