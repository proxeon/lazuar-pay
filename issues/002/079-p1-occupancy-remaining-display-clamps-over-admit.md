---
number: "079"
id: PAY-OCC-010
severity: P1
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 079 — Occupancy remaining display clamps over-admit (honesty leftover)

- **Severity:** P1 (honesty; money is 001)
- **Status:** resolved
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B11
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`Remaining` is `Math.Max(0, max - taken)`. After 001, `taken = 2` on `max = 1` shows remaining 0, status `full`, merchant `2 / 1`. That is accidentally honest on the fraction and **silent** on “over capacity.” Money already moved (005). UI does not warn.

If 001 is closed, this is a leftover clamp that hides bugs. If 001 is open, staff see `2 / 1` without a red flag.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` **11–12**.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` **156–174**.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **89–96**.

## Reproduction

After a 001 race, list the link. `taken_count: 2`, `max_payers: 1`, `remaining: 0`, `status: full`. No over-capacity error.

## Blast radius

Ops cannot distinguish “full as designed” from “we over-charged.”

## Suggested fix

If `taken > max`, surface `status: "over_capacity"` (or a boolean) and a merchant banner. Do not clamp without a log. Closing 001 first makes this a belt.

## Tests

- Missing: fixture two children on max=1 → list status not silently `full` with remaining 0 only — or after 001, this test cannot happen.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B11
