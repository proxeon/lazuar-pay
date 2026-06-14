using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Entities;

namespace Modules.Lhdn.Infrastructure.Services;

public class TaxpayerValidationService : ITaxpayerValidationService
{
    private readonly LhdnDbContext _context;
    private readonly ILhdnGatewayAdapter _gatewayAdapter;
    private readonly string _hashSalt;

    public TaxpayerValidationService(LhdnDbContext context, ILhdnGatewayAdapter gatewayAdapter, IConfiguration configuration)
    {
        _context = context;
        _gatewayAdapter = gatewayAdapter;
        _hashSalt = configuration["Lhdn:TinHashSalt"] ?? "default_local_salt_replace_in_prod";
    }

    public async Task<TinValidationResponse> ValidateTinAsync(Guid organizationId, string tin, string idType, string idValue, CancellationToken ct = default)
    {
        var normalizedTin = tin.Trim().ToUpperInvariant();
        var normalizedIdType = idType.Trim().ToUpperInvariant();
        var normalizedIdValue = idValue.Trim();

        var idValueHash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_hashSalt), 
            Encoding.UTF8.GetBytes(normalizedIdValue))).ToLowerInvariant();

        var cachedResult = await _context.Set<TinValidateCache>()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId 
                                   && c.Tin == normalizedTin 
                                   && c.IdType == normalizedIdType 
                                   && c.IdValueHash == idValueHash, ct);

        if (cachedResult != null && cachedResult.ExpiresAt > DateTime.UtcNow)
        {
            return new TinValidationResponse(cachedResult.IsValid, normalizedTin, cachedResult.TaxpayerName);
        }

        var config = await _context.TenantConfigs.FirstOrDefaultAsync(c => c.OrganizationId == organizationId, ct);
        if (config == null || string.IsNullOrEmpty(config.MyInvoisClientId) || string.IsNullOrEmpty(config.MyInvoisClientSecret))
        {
            throw new InvalidOperationException("LHDN Tenant configuration missing or incomplete.");
        }

        var token = await _gatewayAdapter.GetTokenAsync(organizationId, config.MyInvoisClientId, config.MyInvoisClientSecret, config.IntermediaryMode, config.SupplierTin, ct);
        
        var validationResult = await _gatewayAdapter.ValidateTaxpayerTinAsync(token, normalizedTin, normalizedIdType, normalizedIdValue, ct);

        if (!validationResult.Success && validationResult.ErrorMessage != null)
        {
            throw new InvalidOperationException($"LHDN Validation Failed: {validationResult.ErrorMessage}");
        }

        var cacheExpiry = validationResult.IsValid ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddDays(7);

        if (cachedResult != null)
        {
            cachedResult.UpdateResult(validationResult.IsValid, validationResult.TaxpayerName, cacheExpiry);
        }
        else
        {
            var newCache = new TinValidateCache(organizationId, normalizedTin, normalizedIdType, idValueHash, validationResult.IsValid, validationResult.TaxpayerName, cacheExpiry);
            _context.Add(newCache);
        }

        await _context.SaveChangesAsync(ct);

        return new TinValidationResponse(validationResult.IsValid, normalizedTin, validationResult.TaxpayerName);
    }
}
