using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using Modules.Payments.Application.Commands;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Infrastructure.Commands;

public class UpdatePaymentConfigCommandHandler : ICommandHandler<UpdatePaymentConfigCommand>
{
    private readonly PaymentsDbContext _context;

    public UpdatePaymentConfigCommandHandler(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdatePaymentConfigCommand request, CancellationToken ct)
    {
        var config = await _context.TenantPaymentConfigurations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId && c.GatewayType == request.GatewayType.ToUpperInvariant(), ct);

        // Determine values (prevent overwriting with masked UI placeholders like ••••)
        var finalApiKey = string.IsNullOrEmpty(request.ApiKey) || request.ApiKey.Contains("••••") 
            ? config?.ApiKey 
            : request.ApiKey.Trim();

        var finalWebhookSecret = string.IsNullOrEmpty(request.WebhookSecret) || request.WebhookSecret.Contains("••••") 
            ? config?.WebhookSecret 
            : request.WebhookSecret.Trim();

        var finalSecretKey = string.IsNullOrEmpty(request.SecretKey) || request.SecretKey.Contains("••••") 
            ? config?.ApiKey // In Stripe, SecretKey replaces ApiKey
            : request.SecretKey.Trim();

        // SMART MAPPING FOR BILLPLZ
        // Users frequently put the 128-char X-Signature key into the "Secret Key" field in the UI.
        // If it's Billplz and the SecretKey is a long hex string, map it to WebhookSecret automatically.
        if (request.GatewayType.ToUpperInvariant() == "BILLPLZ")
        {
            if (!string.IsNullOrEmpty(request.SecretKey) && !request.SecretKey.Contains("••••") && request.SecretKey.Length > 60)
            {
                finalWebhookSecret = request.SecretKey.Trim();
            }
        }

        var resolvedGatewayKey = request.GatewayType.ToUpperInvariant() == "STRIPE" ? finalSecretKey : finalApiKey;

        if (config == null)
        {
            config = new TenantPaymentConfiguration(
                request.OrganizationId,
                request.GatewayType,
                resolvedGatewayKey,
                finalWebhookSecret,
                request.MerchantId,
                request.IsActive,
                request.EstimatedFeePercentage,
                request.FixedFee,
                request.TaxRate);
            _context.TenantPaymentConfigurations.Add(config);
        }
        else
        {
            config.UpdateCredentials(
                request.GatewayType,
                resolvedGatewayKey,
                finalWebhookSecret,
                request.MerchantId,
                request.IsActive,
                request.EstimatedFeePercentage,
                request.FixedFee,
                request.TaxRate);
        }
        
        await _context.SaveChangesAsync(ct);
    }
}
