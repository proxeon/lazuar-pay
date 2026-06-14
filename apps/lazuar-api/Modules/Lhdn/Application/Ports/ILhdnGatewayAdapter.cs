using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Lhdn.Application.Ports;

public record LhdnSubmissionResult(bool Success, string? SubmissionUid, string? Uuid, string? ErrorMessage);

public record LhdnDocumentStatusResult(bool Success, string? Status, string? Uuid, string? LongId, string? ErrorMessage);

public interface ILhdnGatewayAdapter
{
    Task<string> GetTokenAsync(Guid organizationId, string clientId, string clientSecret, bool isIntermediary, string? tenantTin, CancellationToken ct = default);
    
    Task<LhdnSubmissionResult> SubmitDocumentAsync(string token, string payloadJson, CancellationToken ct = default);
    
    Task<LhdnDocumentStatusResult> GetDocumentStatusAsync(string token, string submissionUid, CancellationToken ct = default);
}
