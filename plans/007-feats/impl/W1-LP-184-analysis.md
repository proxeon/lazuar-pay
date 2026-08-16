# W1-LP-184 — Self-serve workspace create

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-184`. Tracker: *Self-serve workspace create* — Lazuar **P**.  
**Not this ID:** Time-to-first-checkout checklist (`LP-183`). Public pricing (`LP-006`). Member invite **UI** (API exists; later). Provision/Connect (`LP-143` **Y**).

**Invariant:** A signed-in human can create a **new** workspace (slug + name) without SQL or superadmin, including when they have **zero** entitlements. Register already creates workspace #1.

---

## 0. Scope lock

In scope:

- `POST /one/workspaces` + `CreateWorkspaceCommand` (exists)
- Ops `CreateWorkspaceModal` (exists)
- Empty-entitlements dead-end in `App.tsx`
- Slug rules aligned with register (`OrganizationSlugMustBeValidRule` / reserved list)
- Optional TOS checkbox on **register** (same ticket only if one field)

Out of scope:

- Invite members UI
- Email verification
- Delete/archive workspace UX (API exists)
- Provision secret path (Aura)
- Superadmin entitlement grants

---

## 1. Verdict

Create-workspace is **implemented** for people who already have a workspace. It is **broken** for the empty-entitlement user — the only screen is “Access Denied.”

| Path | Status |
|------|--------|
| `POST /one/public/register` creates user + workspace + ADMIN + 5 apps | **Y** |
| `POST /one/workspaces` cookie auth | **Y** |
| Switcher → “Create New Workspace” | **Y** — `PageLayout` + modal |
| Modal `provision_apps`: OPS, BILLING, PAYMENTS, CRM, LHDN | **Y** — same core set |
| Zero entitlements | **Dead-end** — logout only |
| Modal slug validation | **Weaker** than `LoginPage` (no reserved list / length) |
| TOS at register | **N** |

Tracker **P**: first workspace via signup **Y**; self-serve is not complete.

---

## 2. Current files

| Path | Role |
|------|------|
| `AuthEndpoints.cs` `POST /public/register` | First workspace |
| `RegisterPublicUserCommand.cs` | `CoreModules` + slug unique |
| `WorkspaceEndpoints.cs` `POST /workspaces` | `CreateWorkspaceCommand` |
| `CreateWorkspaceCommand.cs` | Slug unique; owner ADMIN; entitlements from body |
| `CreateWorkspaceModal.tsx` | Name → naive slug; **no** reserved-slug check |
| `LoginPage.tsx` | `RESERVED_SLUGS` + `slugify` + 3–63 |
| `App.tsx` ~117–130 | `entitlements.length === 0` → Access Denied |
| `packages/api-spec/modules/one/models/workspace.tsp` | `CreateWorkspaceRequestDto` |

---

## 3. Gaps

### G1 — Zero entitlements cannot create (P0)

Invite-only users, archived sole workspace, or a failed register side-effect land on a brick wall. The POST exists; the shell never mounts `PageLayout` / modal.

### G2 — Slug rules diverge

API will reject reserved slugs; modal shows a generic `error.detail`. Register UI already validates.

### G3 — No TOS on create/register

18-pricing called this out. One checkbox on register is enough; do not block LP-184 on legal pages.

**Not gaps**

- First workspace on signup.  
- Provision API for machines.

---

## 4. Minimal changes

### 4.1 Must — empty state

Replace Access Denied in `App.tsx` with a centered **Create your workspace** that mounts `CreateWorkspaceModal` (or inlined same fields). On success: `onWorkspaceSelect(id)` + dashboard (same as modal in `PageLayout`). Keep logout.

Do **not** require an existing entitlement to call `POST /one/workspaces` (handler already only needs `UserId`).

### 4.2 Must — share slug helpers

Extract `slugify` / `RESERVED_SLUGS` / `validateSlug` from `LoginPage` to `lib/workspace-slug.ts`. Use in `CreateWorkspaceModal` + register.

### 4.3 Should

- Register TOS checkbox (“I agree to Terms / Privacy”) — links to portal legal pages. Server may still omit persistence this wave; client-required is the bar.  
- Duplicate-slug error: show the API message (already `toast.error(err.message)`).

### 4.4 Do not

- Let `API_CLIENT` create workspaces.  
- Auto-create on empty entitlements without a name.  
- Change provision secret behavior.

---

## 5. Tests

API (if not already):

| Case | Expect |
|------|--------|
| Authenticated user, no memberships, `POST /workspaces` | 200 id; membership ADMIN |
| Duplicate slug | 400 already taken |
| Reserved slug | 400 (API rule) |
| Unauthenticated | 401 |

UI: empty-entitlement screen renders create, not only logout (manual / component if harness exists).

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Workspace spam | Same as public register; rate-limit later |
| Orphan Connect workspaces | Aura docs; not this ticket |

---

## 7. Acceptance

1. User with zero entitlements can create a workspace in Ops and land on the dashboard.  
2. User with ≥1 workspace can still create another from the switcher.  
3. Slug rules match register.  
4. Register still creates workspace #1.  
5. Tests §5 API cases pass.  
6. Tracker **P → Y**.

---

## 8. Implement order

1. Empty-state create  
2. Shared slug validation  
3. Optional TOS checkbox  
