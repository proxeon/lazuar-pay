---
number: "177"
id: B10-X21
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 177 — B10-X21 — `/one/workspaces` exemption + empty ambient is a loaded gun

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X21 — P1 — `/one/workspaces` exemption + empty ambient is a loaded gun

Middleware exempts the entire prefix. Endpoints now mostly call `HasTenantAccessAsync`. That is better than 008’s IDOR. Residual:

- `POST /workspaces/{id}/invites` and `DELETE .../members/{userId}` rely on the **handler** (`CanManageMembers` or `IsSystemAdmin`), not on middleware tenant match. Path `id` is the org. A logged-in ADMIN of org A cannot invite into org B (no membership). A `SUPER_ADMIN` / system admin **can** (intentional).
- VIEWER of org A can `GET .../members` and `GET .../audit` (access, not manage). That is product.
- Any new Map under `/workspaces/` that forgets `HasTenantAccessAsync` is an IDOR **and** the architecture middleware test will still pass, because the path is on the exempt list.

`TenantIsolationArchitectureTests.TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces` **locks the exemption in**. It is a test that documents the gun.

