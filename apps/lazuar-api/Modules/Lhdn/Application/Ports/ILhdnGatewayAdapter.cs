using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Lhdn.Application.Ports;

public record LhdnSubmissionResult(bool Success, string? SubmissionUid, string? Uuid, string? ErrorMessage);

public record LhdnDocumentStatusResult(bool Success, string? Status, string? Uuid, string? LongId, string? ErrorMessage);

public record LhdnTinValidationResult(bool Success, bool IsValid, string? TaxpayerName, string? ErrorMessage);

public interface ILhdnGatewayAdapter
{
    Task<string> GetTokenAsync(Guid organizationId, string clientId, string clientSecret, bool isIntermediary, string? tenantTin, CancellationToken ct = default);
    
    Task<LhdnSubmissionResult> SubmitDocumentAsync(string clientId, string token, string payloadJson, bool isIntermediary, string? tenantTin, CancellationToken ct = default);
    
    Task<LhdnDocumentStatusResult> GetDocumentStatusAsync(string clientId, string token, string submissionUid, bool isIntermediary, string? tenantTin, CancellationToken ct = default);

    Task<LhdnTinValidationResult> ValidateTaxpayerTinAsync(string clientId, string token, string tin, string idType, string idValue, bool isIntermediary, string? tenantTin, CancellationToken ct = default);
}
