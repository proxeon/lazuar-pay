---
number: "275"
id: B07-I35
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 275 — B07-I35 — One README still documents `CLIENT` membership and omits `AuditEvents`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I35 — P2 — One README still documents `CLIENT` membership and omits `AuditEvents`

**Where.** `Modules/One/README.md:22, 33–34, 59–68`.

**What.** Drift. Public-register paragraph (`:10–11`) is correct.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`Modules/One/README.md` still teaches two things the code does not do. `TenantMembership.Role` is documented as `ADMIN` / `CLIENT`, and the consumed-events paragraph says a paid subscription “may grant a `TenantMembership` with the `CLIENT` role for portal access.” Invite allow-list is `ADMIN` / `MEMBER` / `VIEWER`; tests reject `CLIENT` (`Invite_DisallowedRole_Throws`). JWT `CLIENT` is the global human role, not a membership. No handler greps as granting membership `CLIENT` on a Commerce lifecycle event. The schema section lists One tables through webhooks/outbox and **omits** `one.AuditEvents`, even though `AuditRecorder` writes that table and `GET /one/workspaces/{id}/audit` reads it. The public-register paragraph (`README.md:10–11`) is still accurate. This is how the next agent “aligns invite with the README” and re-introduces `CLIENT` as staff.

### Still present?
**DOCS / HONESTY ONLY**

```22:22:apps/lazuar-api/Modules/One/README.md
* **`TenantMembership`**: The junction entity linking a `GlobalUser` to an `Organization` with a specific `Role` (e.g., `ADMIN`, `CLIENT`).
```

```33:34:apps/lazuar-api/Modules/One/README.md
### Consumed
* Subscription / portal membership activation is driven by live Commerce lifecycle integration events (not the deleted Community module). When a public user pays for a subscription, `One` may grant a `TenantMembership` with the `CLIENT` role for portal access to that workspace — confirm handlers in code rather than historical Community event names.
```

Schema list (`README.md:59–68`) names `GlobalUsers`, `Organizations`, `TenantMemberships`, `TenantAppEntitlements`, `WorkspaceInvitations`, `ApiCredentials`, webhook tables, outbox/inbox — no `AuditEvents`. Domain comment on the entity still says `"ADMIN", "CLIENT"` (`TenantMembership.cs:10`). Live roles are `WorkspaceStaffRoles` (`ADMIN`/`MEMBER`/`VIEWER`/`SUPER_ADMIN`). Invite tests still reject `CLIENT`.

### Related files
- `apps/lazuar-api/Modules/One/README.md` — the file to edit.
- `apps/lazuar-api/Modules/One/Domain/TenantMembership.cs` — same `CLIENT` example in a code comment.
- `apps/lazuar-api/Modules/One/Domain/WorkspaceStaffRoles.cs` — actual allow-list.
- `apps/lazuar-api/Modules/One/Domain/AuditEvent.cs` — missing from the schema list.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/One/InviteUserToWorkspaceCommandHandlerTests.cs` — `Invite_DisallowedRole_Throws` including `CLIENT`; **keep**.
- Issue 259 (B07-I15) — dual JWT `CLIENT` vs staff roles (runtime teaching hole, not this README).

### Tests
- Existing: invite role tests lock `CLIENT` as illegal staff. No README honesty test (and none should scrape markdown unless you want a brittle check).
- No test fails if the README stays wrong. `Invite_DisallowedRole_Throws` **would** fail if someone “fixed” invite to match the README.
- First regression (optional): a comment/README lint is overkill; the real guard is keep the `CLIENT` invite test.

### Reproduction today
Open `apps/lazuar-api/Modules/One/README.md` §§4–5 and §8. Compare to `WorkspaceStaffRoles` and `OneDbContext` audit `DbSet`. Grep `CLIENT` membership grants under `Modules/One` — hits are comments and JWT issue, not a Commerce handler. Invite `CLIENT` still throws.

### Blast radius
Authors and agents, not buyers. Worst case is re-introducing `CLIENT` as a staff role or looking for a subscription→membership handler that does not exist. Audit docs omission hides LP-167 identity story (`member.invited` / `member.accepted` / missing `invitation.revoked`).

### Suggested fix
Rewrite the membership bullet to `ADMIN` / `MEMBER` / `VIEWER` (plus provision-only `SUPER_ADMIN`). Delete or rewrite the consumed-events `CLIENT` sentence: buyer portal is Commerce magic-link, not a One membership. Add `one.AuditEvents` to §8. Fix the `TenantMembership.cs` comment in the same PR. Do not change invite tests. Do not TypeSpec. Do not invent a CLIENT-membership grant to make the README true.

### Evaluation notes
Honesty P2. Pair with 259 when teaching the dual model; this file is the written lie, 259 is the cookie/body mismatch. 274 is the other “comments will resurrect a default” pattern. Residual after 161–200.

