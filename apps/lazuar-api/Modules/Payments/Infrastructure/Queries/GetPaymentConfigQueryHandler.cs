using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using Modules.Payments.Application.Queries;
using Lazuar.ApiTypes;

namespace Modules.Payments.Infrastructure.Queries;

public class GetPaymentConfigQueryHandler : IQueryHandler<GetPaymentConfigQuery, PaymentConfigDto?>
{
    private readonly PaymentsDbContext _context;

    public GetPaymentConfigQueryHandler(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentConfigDto?> Handle(GetPaymentConfigQuery request, CancellationToken ct)
    {
        var config = await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId, ct);

        if (config == null) return null;

        return new PaymentConfigDto
        {
            Gateway_type = config.GatewayType,
            Api_key = MaskSecret(config.ApiKey),
            Merchant_id = config.MerchantId,
            Webhook_secret = MaskSecret(config.WebhookSecret),
            Secret_key = MaskSecret(config.ApiKey),
            Is_active = config.IsActive,
            Estimated_fee_percentage = (double)config.EstimatedFeePercentage,
            Fixed_fee = (double)config.FixedFee,
            Tax_rate = (double)config.TaxRate
        };
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "••••";
        return "••••••••" + value[^4..];
    }
}
