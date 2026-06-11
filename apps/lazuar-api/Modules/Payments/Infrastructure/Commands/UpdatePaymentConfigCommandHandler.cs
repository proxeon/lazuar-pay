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

        if (config == null)
        {
            config = new TenantPaymentConfiguration(
                request.OrganizationId,
                request.GatewayType,
                request.ApiKey,
                request.WebhookSecret,
                request.MerchantId,
                request.IsActive,
                request.EstimatedFeePercentage,
                request.FixedFee,
                request.TaxRate);
            _context.TenantPaymentConfigurations.Add(config);
        }
        else
        {
            var finalApiKey = string.IsNullOrEmpty(request.ApiKey) || request.ApiKey.Contains("••••") ? config.ApiKey : request.ApiKey.Trim();
            var finalWebhookSecret = string.IsNullOrEmpty(request.WebhookSecret) || request.WebhookSecret.Contains("••••") ? config.WebhookSecret : request.WebhookSecret.Trim();
            var finalSecretKey = string.IsNullOrEmpty(request.SecretKey) || request.SecretKey.Contains("••••") ? config.ApiKey : request.SecretKey.Trim();
            
            config.UpdateCredentials(
                request.GatewayType,
                request.GatewayType == "STRIPE" ? finalSecretKey : finalApiKey,
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
