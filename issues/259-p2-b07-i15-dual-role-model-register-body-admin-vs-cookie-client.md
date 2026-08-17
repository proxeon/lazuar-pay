---
number: "259"
id: B07-I15
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 259 — B07-I15 — Dual role model + register body `ADMIN` vs cookie `CLIENT`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I15 — P2 — Dual role model + register body `ADMIN` vs cookie `CLIENT`

**Where.** `AuthEndpoints.cs:71, 93, 197`; `TenantSecurityMiddleware.cs:83–88`; `TenantMembership.cs:10` comment; `Modules/One/README.md:22, 33–34`.

**What.** Teachability hole. Scalar without `X-Tenant-Id` fails `OrgAdmin`. README still says membership roles `ADMIN` / `CLIENT` and that a paid subscription “may grant a `CLIENT` membership.” No such handler exists. Next agent who “aligns invite with the README” will try to re-introduce `CLIENT` as staff; invite tests currently reject that string — **keep those tests**.

