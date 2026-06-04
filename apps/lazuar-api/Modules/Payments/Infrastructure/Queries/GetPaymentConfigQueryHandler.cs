using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using Modules.Payments.Application.Queries;

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

        return new PaymentConfigDto(
            config.GatewayType,
            MaskSecret(config.ApiKey),
            config.MerchantId,
            MaskSecret(config.WebhookSecret),
            MaskSecret(config.ApiKey), 
            config.IsActive);
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "••••";
        return "••••••••" + value[^4..];
    }
}
