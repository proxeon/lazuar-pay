---
number: "272"
id: B07-I32
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 272 — B07-I32 — Genesis rotates superadmin password from env every boot

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I32 — P2 — Genesis rotates superadmin password from env every boot

**Where.** `SystemGenesisBootstrapperJob.cs:75–79`.

**What.** Convenient. A leaked `PLATFORM_ADMIN_PASSWORD` in the runtime env is a standing password reset. Dev `appsettings.Development.json:17–18` has `PLATFORM_ADMIN_EMAILS` / `PLATFORM_ADMIN_PASSWORD` in the repo. Dev-only.

