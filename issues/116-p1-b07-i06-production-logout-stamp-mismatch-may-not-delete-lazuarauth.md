---
number: "116"
id: B07-I06
severity: P1
status: resolved
resolved_branch: fix/116-logout-cookie-domain
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 116 — B07-I06 — Production logout / stamp-mismatch may not delete `lazuar_auth`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/116-logout-cookie-domain`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I06 — P1 — Production logout / stamp-mismatch may not delete `lazuar_auth`

**Where.** Set: `AuthEndpoints.cs:206–215` (`Domain = ".lazuar.com"` outside dev). Delete: `:105, :144, :151` — `Cookies.Delete("lazuar_auth")` with default options. Platform: set `Domain + Path` (`PlatformAuthEndpoints.cs:135–145`); delete Path only (`:68, 87, 94`).

**What.** Cookie delete must match Domain/Path/Secure/SameSite. In Production, Sign out can return `logged_out` while the browser keeps sending the JWT. Stamp mismatch on `/auth/me` tries the same broken delete and 401s **that** request; the next navigation still has the cookie, `/auth/me` 401s again, user appears logged out in ops **only because `/auth/me` checks the stamp**. Every other API still accepts the cookie until expiry (B07-I07). Combined, “I changed my password / I logged out” is not a session kill in prod.

