using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Lhdn.Application.Services;

public record TinValidationResponse(bool IsValid, string Tin, string? TaxpayerName);

public interface ITaxpayerValidationService
{
    Task<TinValidationResponse> ValidateTinAsync(Guid organizationId, string tin, string idType, string idValue, CancellationToken ct = default);
}
