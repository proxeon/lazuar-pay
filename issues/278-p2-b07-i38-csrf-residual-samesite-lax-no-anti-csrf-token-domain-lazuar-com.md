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

