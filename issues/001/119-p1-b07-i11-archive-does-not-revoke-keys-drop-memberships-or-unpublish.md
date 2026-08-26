---
number: "119"
id: B07-I11
severity: P1
status: resolved
resolved_branch: fix/119-archive-revoke
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 119 — B07-I11 — Archive does not revoke keys, drop memberships, or unpublish

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/119-archive-revoke`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I11 — P1 — Archive does not revoke keys, drop memberships, or unpublish

**Where.** `ArchiveWorkspaceCommand.cs:23–38`; `Organization.Archive` (`Organization.cs:140–146`); grep of `OrganizationArchivedDomainEvent` is the record + `Archive()` only.

**What.** `IsActive = false`. `HasTenantAccess` still true. `sk_live_` still authenticates. `/me/entitlements` for mortals still lists the org. Public branding GET filters `IsActive` (`OneQueryService.cs:48`); the console does not.

