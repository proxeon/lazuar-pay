---
number: "266"
id: B07-I24
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 266 — B07-I24 — Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I24 — P2 — Register rate-limit key trusts first `X-Forwarded-For` hop; empty key allows

**Where.** `AuthEndpoints.cs:169–183`; `PublicRegisterRateLimiter.cs:21–24`.

**What.** Spoof a new IP → new bucket. Empty key → allow. In-process `ConcurrentDictionary`; multi-instance resets. Hygiene, not a WAF. Tests only cover 11th acquire on one key (`PublicRegisterRateLimiterTests.cs:10–21`).

## Evaluation (current tree, 2026-08-18)

### What the bug is
Register is limited by an in-process token bucket keyed from the request. The audit’s two hygiene holes: (1) the key used the **first** `X-Forwarded-For` hop, so a client could send `X-Forwarded-For: <fresh>` and get a new bucket; (2) `TryAcquireAsync` treated a blank key as **allow**. Combined with a `ConcurrentDictionary` that is not shared across API instances, this is not a WAF — it is a polite in-proc limiter that is easy to reset.

### Still present?
**PARTIAL**

Empty key is **denied** now (and tested). That slice likely landed with **121** (`fix/121-login-rate-limit`) on the sibling limiter:

```19:24:apps/lazuar-api/Modules/One/Infrastructure/Services/PublicRegisterRateLimiter.cs
    public async Task<bool> TryAcquireAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
```

First-hop XFF is **unchanged**. Register still builds `email:{email}|ip:{ip}` via `ResolveRegisterClientKey`:

```236:249:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs
    internal static string ResolveRegisterClientKey(HttpContext ctx, string email)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
            {
                ip = first;
            }
        }

        ip ??= "unknown";
        return $"email:{email}|ip:{ip}";
    }
```

Spoofing the first hop still mints a new bucket per email+ip. Dictionary is still in-process (`PublicRegisterRateLimiter.cs:17`). Login/forgot/resend now share this key helper + `PublicAuthRateLimiter` (also empty-deny). Tests now cover the 11th acquire **and** empty deny (`PublicRegisterRateLimiterTests`).

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs:52–64, 236–249` — acquire + XFF key.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/PublicRegisterRateLimiter.cs` — bucket; empty deny.
- `apps/lazuar-api/Modules/One/Infrastructure/Services/PublicAuthRateLimiter.cs` — same empty-deny pattern for login.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/PublicRegisterRateLimiterTests.cs` — `Blocks_After_Budget`, `Empty_Key_Is_Denied`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/PublicAuthRateLimiterTests.cs` — same.
- `issues/121-p1-b07-i13-login-is-unauthenticated-and-unlimited.md` — sibling limiter.

### Tests
- Existing: `Blocks_After_Budget` (11th same key false; other email+ip true), `Empty_Key_Is_Denied`.
- Those **would fail** if empty keys allowed again. They would **not** fail if XFF first-hop spoofing still works — no test calls `ResolveRegisterClientKey`.
- First regression: unit-test `ResolveRegisterClientKey` with `X-Forwarded-For: 1.1.1.1, 9.9.9.9` and a known `RemoteIpAddress` (the trusted proxy peer) and assert the key uses the **rightmost / socket** IP, not `1.1.1.1`. Keep empty-deny tests.

### Reproduction today
Arrange: 10× `POST /one/public/register` as `a@b.co` with `X-Forwarded-For: 203.0.113.1` → 11th is 429 `Retry-After: 600`. Act: 11th again with `X-Forwarded-For: 198.51.100.2` (same socket IP). Assert: **200 or 400 business**, not 429 — new bucket. `TryAcquireAsync("")` is false in-process. Two API replicas each have their own 10.

### Blast radius
Public signup (and, via the same helper, login/forgot/resend). Credential stuffing / inbox bombing hygiene, not money, not PII disclosure by itself. Empty-key allow is closed so a missing IP no longer unlimited-registers. Remaining: anyone who can set XFF (clients that are not behind a stripping proxy) bypass per spoofed hop; multi-instance multiplies the budget. Frequency: every unauthenticated auth call.

### Suggested fix
Stop trusting the first XFF hop. Prefer `Connection.RemoteIpAddress` (after `UseForwardedHeaders` with a known proxy) or the **last** untrusted hop — never the client-supplied leftmost value. Keep empty-key deny. Do not pretend this is a WAF; document in-proc + per-instance. Optional: add `ResolveRegisterClientKey` tests. No TypeSpec regen.

### Evaluation notes
Empty-allow was the cheap half; **121** likely closed it. Residual XFF is still P2 hygiene. `ip ??= "unknown"` means a request with no remote IP and no XFF shares one `email:x|ip:unknown` bucket (fail-closed-ish), which is fine. Not 161–200 fail-closed.

