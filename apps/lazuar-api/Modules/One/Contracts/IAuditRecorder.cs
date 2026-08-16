using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.One.Contracts;

/// <summary>
/// Fire-and-forget workspace audit. Implementations must never throw to callers.
/// </summary>
public interface IAuditRecorder
{
    Task RecordAsync(
        Guid organizationId,
        string action,
        string entityType,
        string entityId,
        object? metadata = null,
        Guid? actorUserId = null,
        string? actorEmail = null,
        CancellationToken ct = default);
}
