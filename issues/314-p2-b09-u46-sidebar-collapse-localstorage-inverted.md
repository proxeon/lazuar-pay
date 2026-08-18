---
number: "314"
id: B09-U46
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 314 — B09-U46 — Sidebar collapse localStorage inverted

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U46 — Sidebar collapse localStorage inverted (P2)

`App.tsx` 104–108 (ops), 47–51 (admin).

## Evaluation (current tree, 2026-08-18)

### What the bug is
008 and 009 claimed sidebar collapse persistence is inverted because `localStorage.setItem("lazuar-*-sidebar-collapsed", String(prev))` writes the *pre-toggle* `isSidebarOpen`. The key is named `…-collapsed`, and the read path is `isSidebarOpen = localStorage.getItem(...) !== "true"`. Writing the old `isOpen` is therefore the *new* collapsed flag: collapse from open stores `"true"` and the next load starts closed; expand from closed stores `"false"` and the next load starts open. I could not find a code path where that polarity flips. The cited setter is still there (line numbers moved). There is no browser test, so a real invert cannot be confirmed or denied from the tree alone. Flipping the write to `String(!prev)` without a repro would make collapse *fail* to persist.

### Still present?
**SPECULATION / CANNOT CONFIRM**

The write 008/009 called inverted is still the write:

```60:60:apps/lazuar-ops/src/App.tsx
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => localStorage.getItem("lazuar-ops-sidebar-collapsed") !== "true");
```

```121:126:apps/lazuar-ops/src/App.tsx
  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-ops-sidebar-collapsed", String(prev));
      return !prev;
    });
  };
```

Admin is the same pair:

```17:17:apps/lazuar-admin/src/App.tsx
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => localStorage.getItem("lazuar-admin-sidebar-collapsed") !== "true");
```

```56:61:apps/lazuar-admin/src/App.tsx
  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-admin-sidebar-collapsed", String(prev));
      return !prev;
    });
  };
```

Polarity check: `isSidebarOpen === true` (expanded) → click Collapse (`Sidebar.tsx` 222) → store `String(true)` = collapsed → `return false`. Reload → `"true" !== "true"` → `isSidebarOpen === false`. That is the intended persist. 134 changed mobile force-close / hamburger; it did not change this setter.

### Related files
- `apps/lazuar-ops/src/App.tsx` — ops persist + initial state.
- `apps/lazuar-admin/src/App.tsx` — admin persist + initial state.
- `apps/lazuar-ops/src/components/Sidebar.tsx` / `apps/lazuar-admin/src/components/Sidebar.tsx` — collapse/expand buttons call `setIsOpen` (the toggle).
- Issue 134 (`fix/134-mobile-nav-hamburger`) — mobile rail; do not re-force-close on resize.

### Tests
- Existing tests that touch this path: none. Ops/admin have no unit or Playwright tests.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert (only after a manual repro): after `handleToggleSidebar` from open, `localStorage['lazuar-ops-sidebar-collapsed'] === 'true'` and a remount reads closed. If that already holds, close this ticket as a polarity misread rather than inverting the write.

### Reproduction today
Desktop width ≥ 768. Load ops. Rail expanded (no key or key `"false"`). Click the collapse control. Refresh. Assert whether the rail stays collapsed. Repeat expand → refresh. Repeat on admin. Record the `localStorage` values. Do **not** change the setter until that walk disagrees with the polarity above.

### Blast radius
If the audit were right: every ops/admin desktop user, preference only, no money/PII. If the polarity is correct: blast is an implementer “fix” that breaks persist. Frequency: every sidebar toggle.

### Suggested fix
Do not invert `String(prev)` without a failing walk. If a walk shows invert, store the *new* collapsed value explicitly (`String(prev)` is already that value; prefer `const next = !prev; localStorage.setItem(..., String(!next)); setIsSidebarOpen(next)` for readability). Keep the key name `…-collapsed`. No TypeSpec, no product behavior change.

### Evaluation notes
008 and 009 repeated the same diagnosis; neither shipped a repro. 134 is adjacent mobile UX, not this key. Severity as a P2 persist bug is unproven; treat as P3/docs until a walk fails. Not blocked, but easy to regress. Do not mark resolved in YAML from this evaluation.

