---
number: "325"
id: B09-U57
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 325 — B09-U57 — Zero tests in ops and admin

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U57 — Zero tests in ops and admin (P2)

The only frontend test in this slice is `i18n.test.mjs`, and it cements U22.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The 17 Aug frontend audit of ops / portal / admin found **no** test files in `lazuar-ops` or `lazuar-admin`. The only frontend test in that slice was portal `i18n.test.mjs`, and at audit time it asserted the U22 lie (missing Resend → `error.gatewayDown`). There is no Playwright, Vitest, or component test that clicks Viewer-illegal buttons, checks admin `returnUrl`, or asserts Xendit fields. CI can stay green while those UIs regress. This ticket is the coverage gap, not a single runtime failure.

### Still present?
**STILL BROKEN**

Ops and admin still have **zero** `*.test.*` / `*.spec.*` files and **no** `test` script.

`apps/lazuar-ops/package.json` scripts are only `dev`, `build`, `preview`, `clean`, `lint` (`tsc --noEmit`) — lines 6–11. Same for `apps/lazuar-admin/package.json` 6–11. A search for `.test.` / `.spec.` / `vitest` / `playwright` / `cypress` under both app trees is empty.

Portal is no longer “one file that cements U22”:

- `apps/lazuar-portal/package.json:10` runs `i18n.test.mjs` **and** `src/modules/checkout/lib/grossBreakdown.test.mjs`.
- U22 was fixed in **151** (`fix/151-email-missing-not-gateway`). `classifyCheckoutError` maps missing email provider to `error.emailMissing` (`errors.ts:26–28`); `i18n.test.mjs:134–137` now asserts that, not `error.gatewayDown`.

That does not give ops or admin a test runner. The audit table still holds for those two apps: no test that Viewer buttons are hidden, that admin `returnUrl` is relative-only, that Xendit fields render, or that `/ops/chat` stays unmounted.

### Related files
- `apps/lazuar-ops/package.json` — no `test` script.
- `apps/lazuar-admin/package.json` — no `test` script.
- `apps/lazuar-portal/package.json` — `node --test` for two modules only.
- `apps/lazuar-portal/src/modules/checkout/i18n/i18n.test.mjs` — locale + error classification (U22 no longer cemented).
- `apps/lazuar-portal/src/modules/checkout/lib/grossBreakdown.test.mjs` — added later; SST math, not ops/admin.
- `apps/lazuar-admin/src/components/LoginPage.tsx` — `isSafeReturnUrl` (136) untested in-app.
- `apps/lazuar-ops/src/modules/core/components/PageLayout.tsx` — untested Viewer create (317).
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — untested missing invites list (318).
- `apps/lazuar-ops/src/App.tsx` — untested `[MVP-HIDE]` chat (321).

### Tests
- Existing in this slice: portal `parseLocale` / `resolveCheckoutLocale` / `messages` / `classifyCheckoutError` / `interpolate and money` (`i18n.test.mjs`); `computeSstTax` / quote breakdown (`grossBreakdown.test.mjs`).
- Existing API tests cover some contracts (create workspace, invite pending-exists, platform `/me` null for non-admin, QR `/qr` policy) but **do not** mount React.
- No test would fail because ops/admin still have zero tests — that *is* the bug.
- First regression tests (pick the painted P1/P2s, smallest):
  1. Admin: `isSafeReturnUrl('https://evil.example') === false`, `'//evil' === false`, `'/platform/gateways' === true`.
  2. Ops: `PageLayout` + `role: 'VIEWER'` does not offer Create New Workspace (317).
  3. Ops: Team page after invite fetches `/one/workspaces/{id}/invites` (318).
  4. Ops: `App` route table has no live `/ops/chat` (321).
  Use `node:test` like portal, or Vitest; do not stand up Playwright for the first lock unless you already have it. Do not regenerate TypeSpec to “get tests.”

### Reproduction today
Arrange: repo root. Act: `ls apps/lazuar-ops/**/*.{test,spec}.*` and same for admin — no files. Act: `jq .scripts apps/lazuar-ops/package.json` — no `test`. Act: `pnpm --filter lazuar-ops test` / `lazuar-admin test` — script missing. Act: `pnpm --filter lazuar-portal test` — runs and passes (i18n + gross breakdown). That green run does not exercise ops or admin.

### Blast radius
Every ops/admin UI bug in this audit (Viewer writes, silent admin bounce, missing invites, leftover WhatsApp labels, remounted chat) can return without CI. Not directly money; it is why P0/P1 frontend bugs had no safety net. Frequency: every PR that touches those apps. Still P2 as a meta-issue; the individual painted bugs keep their own severity.

### Suggested fix
Add the smallest runner that matches portal (`node --test` + strip-types) under `apps/lazuar-ops` and `apps/lazuar-admin`, plus 1–2 tests on the highest-churn guards (`isSafeReturnUrl`, switcher role, chat route absent). Wire `package.json` `"test"` and the repo Taskfile/turbo if those already call `pnpm test`. Do not try to cover the whole SPA. Do not remount chat to make it “testable.” No Wave 5.

### Evaluation notes
Still P2 coverage gap. Portal gained `grossBreakdown.test.mjs`; U22 is **not** cemented anymore (151). Ops/admin were not given tests in 001–200. Sister tickets that would actually turn red if those tests existed: 136 (already fixed, untested), 317, 318, 321, 322. Do not mark 325 resolved until ops and admin have a runner and at least one assertion each.

