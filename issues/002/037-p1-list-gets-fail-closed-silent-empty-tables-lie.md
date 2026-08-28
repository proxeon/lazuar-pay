---
number: "037"
id: PAY-MERCH-003
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 037 — List GETs fail closed-silent; empty tables lie

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B3
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Overview / Gateway / Pay links / Payments / Receipts treat non-OK list GETs as empty:

- Overview `GET /gateways` `if (!r.ok) return` → “On file none”
- Gateway `refresh` same → all rails Empty, Test still Ready
- `loadLinks` `if (!r.ok) return` → “No pay links yet”
- Payments / receipts `if (r.ok) set…` else ignore

Host 403/503 `detail` is discarded. A paused identity blip looks like a brand-new workspace. `void loadLinks()` without `.catch` is an unhandled rejection if `fetch` throws.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/OverviewPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **45–50**
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **117–121**
- `apps/lazuar-pay-merchant/src/pages/org/PaymentsPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/org/ReceiptsPage.tsx`
- `apps/lazuar-pay-merchant/src/lib/http.ts` — `problemDetail` used on **writes**, not these GETs.

## Reproduction

Stop 8081. Open Pay links. “No pay links yet”. Create looks available.

## Blast radius

Staff mint/paste into a host that is not up. Empty-state illustrations lie.

## Suggested fix

One shared `payJson` helper: non-OK → `problemDetail` into a page-level `role="alert"`; network throw → “Pay unreachable”. Empty-state copy only when HTTP 200 and `[]`.

## Tests

- Missing: locks that error paths are not the empty illustration (component). Today greps lock empty copy strings.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B3
