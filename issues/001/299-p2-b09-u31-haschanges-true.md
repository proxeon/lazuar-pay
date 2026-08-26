---
number: "299"
id: B09-U31
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 299 — B09-U31 — `hasChanges || true`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U31 — `hasChanges || true` (P2)

`GeneralSettingsPage.tsx` 110–113. Save is never disabled.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops General Settings computes `hasChanges` from workspace name and slug, then ORs `true`, so the expression is always true. The Save button’s `disabled={!hasChanges || updateMutation.isPending}` therefore never disables for “no edits.” The merchant can click Save on an untouched form and fire `PUT /one/workspaces/{id}` (Admin-only; Member/Viewer/Superadmin hit the U21 unauthorized path). Logo and accent color are also writable on this page and are **not** part of the dirty check at all — the `|| true` was likely a leftover so those extra fields could save. The UX lie is “Save Changes” looks dirty on first paint.

### Still present?
**STILL BROKEN**

```110:113:apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx
  const hasChanges =
    name !== entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_name
    || slug !== originalSlug
    || true;
```

```196:196:apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx
              <button type="submit" disabled={!hasChanges || updateMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white …
```

The page now also edits `logoUrl` and `primaryColor` (136–169) and PUTs them (83–88). Those fields are ignored by `hasChanges` except that `|| true` keeps Save enabled. Contrast `MessageTemplateEditor.tsx:91`, which computes a real dirty flag.

### Related files
- `apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx` — the `|| true`.
- `apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs` — Admin-only save (U21 / issue 219-area if Superadmin).
- `apps/lazuar-ops/src/modules/commerce/components/MessageTemplateEditor.tsx` — a page that already does dirty-checking correctly.

### Tests
- Existing: none in `apps/lazuar-ops` (no test runner / no page tests). Portal only has `i18n.test.mjs` and `grossBreakdown.test.mjs`.
- Would any test fail if the bug is still there? No.
- First regression: with name/slug/logo/color equal to loaded values, the submit button is disabled; changing any one field enables it.

### Reproduction today
Sign in as workspace Admin. Open `/workspace/general`. Do not edit. Assert: Save Changes is fully opaque / clickable. Click it. Assert: success toast and a PUT even though nothing changed. (Member/Viewer: same enabled button, then an error toast.)

### Blast radius
Ops chrome only. Accidental no-op writes. Superadmin/Member still cannot legally save (separate U21). No money, no PII. Every merchant who opens General Settings.

### Suggested fix
Drop `|| true`. Include `logoUrl` and `primaryColor` in the dirty comparison against the values loaded from `GET /one/workspaces/{id}`. Keep the slug-change confirm. Do not change `UpdateWorkspaceCommand` role rules here.

### Evaluation notes
Still P2. 008 already had this; 009 re-verified open. Not blocked. Do not treat Superadmin 500 as this ticket.
