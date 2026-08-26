---
number: "267"
id: B07-I27
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 267 — B07-I27 — Cookie `OnMessageReceived` always wins over Authorization JWT

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I27 — P2 — Cookie `OnMessageReceived` always wins over Authorization JWT

**Where.** `AuthAndCorsExtensions.cs:52–64`.

**What.** Documented dual-realm. Integrators debugging with Bearer + a leftover ops cookie will not see their Bearer identity. Not a steal.

## Evaluation (current tree, 2026-08-18)

### What the bug is
JWT bearer auth installs `OnMessageReceived` that, on every request, copies `lazuar_auth` (or `lazuar_admin_auth` on `/api/v1/platform`) into `context.Token` when the cookie exists. It never looks at whether `Authorization: Bearer …` is already present. Dual-realm is intentional (SPA cookie vs integrator Bearer). The foot-gun: a browser or a curl that sends **both** a leftover Hub cookie and a Bearer JWT authenticates as the **cookie** identity. Not a steal of another tenant by itself — you still need that cookie — but it makes M2M debugging lie.

### Still present?
**STILL BROKEN**

```52:64:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs
                    OnMessageReceived = context =>
                    {
                        // Dual cookie realm: platform admin vs product console.
                        var isPlatformRoute = context.Request.Path.StartsWithSegments("/api/v1/platform");
                        var cookieName = isPlatformRoute ? "lazuar_admin_auth" : "lazuar_auth";

                        if (context.Request.Cookies.TryGetValue(cookieName, out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
```

No `Authorization` guard. API keys are a **different** middleware (`ApiKeyAuthenticationMiddleware`) that runs on `sk_*` prefixes and sets `context.User` directly — those are not JWT `OnMessageReceived`. `AuthCookieTests` only check Domain/Path/Secure, not scheme precedence. I found no test that Bearer wins, loses, or ties with a cookie.

### Related files
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs:33–66` — JWT + OnMessageReceived.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs:265–289` — sets `lazuar_auth`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/AuthCookieTests.cs` — cookie options only.
- `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` — sk_ keys bypass this JWT event.
- `issues/116-p1-b07-i06-production-logout-stamp-mismatch-may-not-delete-lazuarauth.md` — leftover cookie in prod.
- `issues/259-p2-b07-i15-dual-role-model-register-body-admin-vs-cookie-client.md` — what that cookie identity is.

### Tests
- Existing: `ProductionDelete_MatchesSetDomainAndFlags`, `Development_OmitsDomain`. JWT validation tests (if any) do not combine Cookie + Authorization.
- Nothing would fail if cookie kept winning, or if someone inverted the precedence.
- First regression: a host-level test (TestServer) that sets `lazuar_auth` for user A and `Authorization: Bearer <user B JWT>` on `/one/auth/me` (not an `sk_` key) and asserts the chosen identity. Product choice: Bearer should win when present and not an API key, matching “integrators debugging.”

### Reproduction today
Arrange: log into ops (cookie user A). Act: from the same browser origin or a curl that forwards cookies, `GET /api/v1/one/auth/me` with `Authorization: Bearer <user B JWT>`. Assert: response is user A (cookie). Repeat with `Authorization: Bearer sk_test_…` → API-key middleware, not this event. Platform path uses `lazuar_admin_auth` only (`:55–56`), so a Hub cookie does not walk into `/api/v1/platform` unless that admin cookie is also set (audit already noted this).

### Blast radius
Integrators and support, not buyers. Wrong identity on mixed requests can look like a scope/tenant bug and can authorize as the leftover staff session (refunds, keys, invites) while the human thought they were the Bearer. Not a remote steal without the cookie. Worse if **116** left `lazuar_auth` undeleted in production. Frequency: every dual-header debug session.

### Suggested fix
If `Authorization` starts with `Bearer ` and the token is **not** an `sk_live_`/`sk_test_` key, do not assign `context.Token` from the cookie. Keep cookie-only SPA requests as they are. Keep the platform/hub cookie name split. Do not TypeSpec-regen. Do not treat API keys here — they already short-circuit in middleware.

### Evaluation notes
Still P2, documented dual-realm. Residual after **116/117** (logout/stamp): a cookie that should have died still wins over Bearer. **259** explains why that cookie is `CLIENT` until tenant injection. Not 161–200 fail-closed.

