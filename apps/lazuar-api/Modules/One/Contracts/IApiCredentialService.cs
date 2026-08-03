using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.One.Contracts;

public record ApiCredentialGenerateResult(
    Guid Id,
    string Name,
    string Prefix,
    string Hint,
    DateTime CreatedAt,
    string PlainKey,
    string Scopes);

public record ApiCredentialSnapshot(
    Guid Id,
    string Name,
    string Prefix,
    string Hint,
    bool IsActive,
    DateTime CreatedAt,
    string Scopes);

/// <summary>
/// Cross-module façade for platform API credentials owned by One.
/// Product modules (e.g. Lhdn) call this instead of owning keys locally.
/// </summary>
public interface IApiCredentialService
{
    Task<ApiCredentialGenerateResult> GenerateAsync(
        Guid organizationId,
        string name,
        bool isTestMode,
        Guid? createdByUserId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiCredentialSnapshot>> ListAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task RevokeAsync(
        Guid organizationId,
        Guid credentialId,
        CancellationToken ct = default);
}
