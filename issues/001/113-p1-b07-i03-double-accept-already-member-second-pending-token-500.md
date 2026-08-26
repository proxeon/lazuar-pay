---
number: "113"
id: B07-I03
severity: P1
status: resolved
resolved_branch: fix/113-double-accept-500
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 113 — B07-I03 — Double-accept / already-member / second pending token → 500

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/113-double-accept-500`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I03 — P1 — Double-accept / already-member / second pending token → 500

**Where.** `AcceptWorkspaceInvitationCommand.cs:36–41` always inserts `TenantMembership`. Unique index `TenantMemberships (GlobalUserId, OrganizationId)` (`OneDbContext.cs:73`). `GlobalExceptionHandler.cs:52–62` maps non-`InvalidOperationException` to 500 and **echoes `exception.Message`**.

**Triggers.**

1. Two concurrent POSTs of the same still-PENDING token (the SPA Map prevents this in one tab; two browsers do not).
2. Two PENDING invites for the same email (non-unique index, B07-I04); first accept 200, second token 500.
3. User already a member (provision `EnsureOwnerAsync`, or they accepted the other invite) and a PENDING invite remains.

Replay of a single ACCEPTED row is **400**, not 500. The SPA’s “status >= 500 means already accepted” (`AcceptInvitePage.tsx:40–46`) is a bandage that also fires on real outages (B07-I08).

**Tests.** `AcceptWorkspaceInvitationCommandHandlerTests` covers happy, expired, wrong email. **No** already-member, **no** second pending, **no** concurrent, **no** replay-after-accept.

