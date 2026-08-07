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
    /// <param name="scopes">
    /// Optional scope list from the closed platform catalog.
    /// Null/omitted uses LHDN document defaults; empty or unknown values are rejected.
    /// </param>
    Task<ApiCredentialGenerateResult> GenerateAsync(
        Guid organizationId,
        string name,
        bool isTestMode,
        Guid? createdByUserId = null,
        IReadOnlyList<string>? scopes = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApiCredentialSnapshot>> ListAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task RevokeAsync(
        Guid organizationId,
        Guid credentialId,
        CancellationToken ct = default);
}
