using System.Collections.Concurrent;

namespace Lazuar.Pay.Checkouts;

/// <summary>In-memory fixture store. Not a ledger. Replace when money is real.</summary>
public sealed class CheckoutStore
{
    readonly ConcurrentDictionary<string, CheckoutSession> _byId = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.Ordinal);

    public CheckoutSession Create(CheckoutSession session, string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var key = session.OrgId + "\n" + idempotencyKey;
            if (_idempotency.TryGetValue(key, out var existingId) &&
                _byId.TryGetValue(existingId, out var existing))
            {
                return existing;
            }

            _byId[session.Id] = session;
            _idempotency.TryAdd(key, session.Id);
            return session;
        }

        _byId[session.Id] = session;
        return session;
    }

    public CheckoutSession? Get(string id) =>
        _byId.TryGetValue(id, out var session) ? session : null;
}
