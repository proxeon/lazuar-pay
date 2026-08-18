---
number: "264"
id: B07-I22
severity: P2
status: open
source: plans/009-bugs/07-one-identity-invites-keys.md
head: "297ba98"
---

# 264 — B07-I22 — Entitlements query error skips empty-state and renders a hollow shell

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/07-one-identity-invites-keys.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B07-I22 — P2 — Entitlements query error skips empty-state and renders a hollow shell

**Where.** `App.tsx:81–89, 123–157`.

**What.** `useQuery` error → `data` undefined → not `length === 0` → chrome with `[]` entitlements and whatever `ops_active_workspace_id` still says. Not the LP-184 empty-state. Not Access Denied. A failed One query looks like a logged-in product with no workspace switcher.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops layout waits on `/one/auth/me`, then `useQuery` `/one/me/entitlements`. On query **error**, `data` is `undefined`, `isEntitlementsLoading` is false, `entitlements?.length === 0` is false, so the LP-184 EmptyWorkspaceState does not run. The layout used to render the full chrome with `entitlements || []` and a stale `ops_active_workspace_id`, so every page 403’d and there was no Retry/Create. A failed One read looked like a logged-in product with no switcher.

### Still present?
**ALREADY FIXED**

Issue **147** (`B09-U18`, `fix/147-entitlements-error`). Current `OpsLayout` branches on `isEntitlementsError` **before** empty-state and chrome, does not treat the stale localStorage id as active, and offers Retry + Log out:

```144:166:apps/lazuar-ops/src/App.tsx
  if (user && isEntitlementsError) {
    return (
      <div className="flex h-screen w-full flex-col items-center justify-center bg-[#f5f5f5] gap-4 px-6">
        <p className="text-[13px] text-[#71717a] text-center max-w-sm">
          Could not load your workspaces. The last workspace id is not used until this succeeds.
        </p>
        <button
          type="button"
          onClick={() => { void refetchEntitlements(); }}
          className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest"
        >
          Retry
        </button>
        <button
          type="button"
          onClick={handleLogout}
          className="h-9 px-6 text-[11px] font-bold uppercase tracking-widest text-[#71717a]"
        >
          Log out
        </button>
      </div>
    );
  }
```

Empty state still only runs when `entitlements?.length === 0` (`:168–175`). Loading still blocks chrome (`:140–142`). `workspaceRoleOf` is only used after a successful list (`:183`).

### Related files
- `apps/lazuar-ops/src/App.tsx` — `isEntitlementsError` gate, empty state, layout.
- `apps/lazuar-ops/src/components/EmptyWorkspaceState.tsx` — LP-184 create path (success + empty only).
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs:154–178` — `GET /me/entitlements`.
- `issues/147-p1-b09-u18-entitlements-query-failure-skips-empty-state.md` — P1 twin, resolved.
- `issues/120-p1-b07-i12-superadmin-synthetic-entitlements-vs-real-403.md` — different empty-vs-denied story.

### Tests
- Existing: none in ops. Issue **325** still holds — no RTL that `isError` shows Retry instead of Sidebar.
- API entitlements tests (if any) would not catch this UI hole; nothing in `apps/lazuar-ops` would fail if the error branch were deleted.
- First regression: render `OpsLayout` with a mocked entitlements query `isError: true` and `localStorage.ops_active_workspace_id` set; assert the Retry copy, no Sidebar, no EmptyWorkspaceState, no child route.

### Reproduction today
Arrange: valid `lazuar_auth`, `localStorage.ops_active_workspace_id` = some guid, make `GET /one/me/entitlements` 500. Act: open `/commerce/dashboard`. Assert: full-screen “Could not load your workspaces…” + Retry + Log out; dashboard chrome does not mount; Retry refetches. When the query later returns `[]`, EmptyWorkspaceState appears. When it returns rows, the switcher appears and the stored id is validated (`App.tsx:108–118`).

### Blast radius
Was: every merchant whose entitlements read failed (deploy, 500, network) looked locked out of a hollow console; stale tenant header 403 storm. Now: explicit error. No money. Residual: still no automated UI test, so a future refactor can drop the branch silently.

### Suggested fix
None for the product hole. Add the RTL test above if touching App.tsx. Do not treat error as empty (that would offer Create during an outage). No TypeSpec regen.

### Evaluation notes
Duplicate of resolved **147**. Leave YAML `open`. **120** is still a separate superadmin 403 story. Not 161–200 fail-closed.

