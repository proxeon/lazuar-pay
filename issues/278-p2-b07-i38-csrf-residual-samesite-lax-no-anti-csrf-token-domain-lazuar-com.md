---
number: "278"
id: B07-I38
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 278 — B07-I38 — CSRF residual: SameSite=Lax, no anti-CSRF token, Domain `.lazuar.com`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I38 — P2 — CSRF residual: SameSite=Lax, no anti-CSRF token, Domain `.lazuar.com`

**Where.** `AuthEndpoints.cs:206–213`.

**What.** Lax blocks most cross-site POST. Same-site sibling apps on `*.lazuar.com` can POST with the cookie. 008 H11. Hub path-based deploy (`hub.lazuar.com` + `/portal`) is same-site by definition.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Merchant session is an HttpOnly cookie `lazuar_auth` with `SameSite=Lax`, `Secure` outside Development, and `Domain=.lazuar.com` outside Development. There is no anti-CSRF token, no `Origin`/`Referer` allow-list on cookie POSTs, and no double-submit cookie. Lax blocks most **cross-site** POSTs from a foreign eTLD+1 (the classic bank CSRF). It does **not** block: (1) same-site sibling hosts on `*.lazuar.com` (a compromised or curious page on another Lazuar subdomain POSTs to `hub.lazuar.com/api/v1/...` and the cookie goes); (2) the path-based hub itself (`hub.lazuar.com` + `/portal`) which is same-site by definition; (3) top-level GET navigations (accept-invite, which is why Lax is also load-bearing). Cookie set/delete now goes through `AuthCookie` (116’s Domain-match fix); the CSRF residual is unchanged. 008 H11.

### Still present?
**STILL BROKEN**

```40:46:apps/lazuar-api/Modules/One/Infrastructure/Services/AuthCookie.cs
    private static CookieOptions BaseOptions(bool isDev) => new()
    {
        HttpOnly = true,
        Secure = !isDev,
        SameSite = SameSiteMode.Lax,
        Domain = isDev ? null : ".lazuar.com"
    };
```

`IssueCookie` uses `AuthCookie.MerchantOptions` (`AuthEndpoints.cs:286–288`). Tests **lock** Lax + `.lazuar.com` (`AuthCookieTests.ProductionDelete_MatchesSetDomainAndFlags`). Grep of `AntiForgery` / `ValidateAntiForgery` / `csrf` in API + ops is empty (portal locale cookie is unrelated Lax). CORS is origin-listed in repo appsettings (124 / B07-I25 is the empty-origins foot-gun); credentialed CORS does not stop a same-site sibling that does not need CORS.

### Related files
- `apps/lazuar-api/Modules/One/Infrastructure/Services/AuthCookie.cs` — flags.
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` `IssueCookie`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/AuthCookieTests.cs` — locks the residual.
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` — CORS + cookie authentication.
- `deploy/prod/env.example` — `App__OpsUrl=https://hub.lazuar.com`, `App__ClientUrl=https://hub.lazuar.com/portal` (same host).
- `apps/lazuar-ops/src/lib/api-client.ts` — `credentials: "include"`, no CSRF header.
- Issue 116 (logout Domain match, resolved) — same cookie helper; do not break delete options.

### Tests
- Existing: `AuthCookieTests.ProductionDelete_MatchesSetDomainAndFlags` (Domain/Secure/HttpOnly/Lax); `AuthCookieTests.Development_OmitsDomain`; `CorsOriginsGuardTests` (124).
- `AuthCookieTests` would **fail** if you flipped SameSite to Strict (and logout Domain mismatch would return). Nothing fails because there is no CSRF token.
- First regression: state-changing cookie POST from a disallowed `Origin` is 403; same-origin ops POST still 200; `AuthCookie` delete options still match set.

### Reproduction today
Arrange: Production-like cookie (`Domain=.lazuar.com`, Lax) while logged into hub. Act: from another page on `https://docs.lazuar.com` (or any `*.lazuar.com` you control) `fetch('https://hub.lazuar.com/api/v1/one/workspaces/{id}/invites', { method:'POST', credentials:'include', headers:{'Content-Type':'application/json','X-Tenant-Id':...}, body:...})`. Assert: browser sends `lazuar_auth`; API authorizes if CORS allows that origin **or** if the attacker page is same-site and does not need CORS (form POST / navigate). Cross-site `evil.com` POST should not include the cookie (Lax). Hub `/portal` can POST to `/api/v1` on the same host with the cookie.

### Blast radius
Any cookie-authenticated mutating One/Commerce route (invite, archive, keys, refunds if those accept cookies). Not an unauthenticated steal. Production hub is one host + path, so portal XSS becomes ops CSRF. Sibling-subdomain risk depends on what else you put on `*.lazuar.com`. Frequency: only if a same-site page is hostile or XSS’d.

### Suggested fix
Keep Lax (Strict would break the email GET → accept flow unless you move the token out of the first navigation). Add a narrow Origin/Referer allow-list on cookie POSTs that matches `App:CorsOrigins` / `App:OpsUrl`, or a real anti-CSRF token the ops client sends. Do not invent a homemade e-mandate or a half cookie. Do not drop `Domain=.lazuar.com` without re-testing 116 logout. Do not TypeSpec. Do not mark this fixed by “we use Lax.”

### Evaluation notes
Still P2 (008 H11). Not a duplicate of 124 (CORS allow-any) or 116 (delete Domain). Path-based hub makes “same-site sibling” = portal XSS. Residual after 161–200: cookie helper extracted, CSRF not.

