using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Application.Ports;
using Lazuar.ApiTypes;

namespace Modules.Payments.Infrastructure.Queries;

public class GetPaymentConfigQueryHandler : IQueryHandler<GetPaymentConfigQuery, IEnumerable<PaymentConfigDto>>
{
    private readonly ITenantPaymentConfigRepository _repository;
    private readonly ISecretVault _secretVault;

    public GetPaymentConfigQueryHandler(ITenantPaymentConfigRepository repository, ISecretVault secretVault)
    {
        _repository = repository;
        _secretVault = secretVault;
    }

    public async Task<IEnumerable<PaymentConfigDto>> Handle(GetPaymentConfigQuery request, CancellationToken ct)
    {
        var configs = await _repository.GetAllByTenantIdAsync(request.OrganizationId, ct);

        return configs.Select(config =>
        {
            var hasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey);
            var hasWebhook = !string.IsNullOrWhiteSpace(config.WebhookSecret);
            var apiHint = _secretVault.HintLast4(config.ApiKey);
            var webhookHint = _secretVault.HintLast4(config.WebhookSecret);

            return new PaymentConfigDto
            {
                Gateway_type = config.GatewayType,
                // Never return stored ciphertext or plaintext secrets.
                Api_key = null,
                Merchant_id = config.MerchantId,
                Webhook_secret = null,
                Secret_key = null,
                Is_active = config.IsActive,
                Has_api_key = hasApiKey,
                Api_key_hint = apiHint,
                Has_webhook_secret = hasWebhook,
                Webhook_secret_hint = webhookHint,
                // Stripe secret is stored in ApiKey column.
                Has_secret_key = hasApiKey,
                Secret_key_hint = apiHint
            };
        });
    }
}
