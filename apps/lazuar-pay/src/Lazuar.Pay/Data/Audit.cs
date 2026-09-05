using System.Text.Json;

namespace Lazuar.Pay.Data;

/// <summary>
/// plans/031/05: builds audit rows with the acting actor and a non-sensitive detail
/// snapshot, so every audit row answers who acted and what changed. Actor is the One user
/// id resolved by MemberGate (read from <c>RequestLog.ActorItemKey</c>) or "psp:&lt;provider&gt;"
/// for webhook-driven events. Detail is a small serialized snapshot — provider/last4/
/// prefix/amounts, never secrets, tokens, or raw payloads.
/// </summary>
public static class Audit
{
    private static readonly JsonSerializerOptions DetailJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static AuditEventRow New(string orgId, string action, string? actor, object? detail) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OrgId = orgId,
        Action = action,
        At = DateTimeOffset.UtcNow,
        Actor = actor,
        Detail = detail is null ? null : JsonSerializer.Serialize(detail, DetailJson)
    };
}
