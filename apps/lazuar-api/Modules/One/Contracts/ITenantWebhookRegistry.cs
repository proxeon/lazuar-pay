using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.One.Contracts;

public record TenantWebhookEndpointSnapshot(
    Guid Id,
    string Url,
    bool IsActive,
    IReadOnlyList<string> EnabledEvents,
    DateTime CreatedAt);

public record TenantWebhookRegisterResult(
    Guid Id,
    string Url,
    bool IsActive,
    IReadOnlyList<string> EnabledEvents);

/// <summary>
/// Cross-module façade for workspace webhook endpoints owned by One.
/// Product modules (e.g. Lhdn) register and list through this instead of One.Application.
/// </summary>
public interface ITenantWebhookRegistry
{
    Task<TenantWebhookRegisterResult> RegisterAsync(
        Guid organizationId,
        string url,
        IReadOnlyList<string>? enabledEvents,
        CancellationToken ct = default);

    Task<IReadOnlyList<TenantWebhookEndpointSnapshot>> ListAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task<TenantWebhookEndpointSnapshot?> GetByIdAsync(
        Guid organizationId,
        Guid endpointId,
        CancellationToken ct = default);

    Task DisableAsync(
        Guid organizationId,
        Guid endpointId,
        CancellationToken ct = default);

    Task DisableByUrlAsync(
        Guid organizationId,
        string url,
        CancellationToken ct = default);
}
