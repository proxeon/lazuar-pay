---
number: "034"
id: PAY-TEST-004
severity: P1
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 034 — Test occupancy tests hide the reservation model

- **Severity:** P1 (test lie)
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B8 (also `09-tests-inventory.md`)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`Two_people_can_pay_a_link_of_two` and `One_person_link_shows_paid_without_slot_after_pay` seed `provider: test` (PayTest default). Start pays instantly. The third Start 409 is “two **paid** seats”, which coincides with “two started seats”. Replace Test with CHIP and the same test would 409 the third Start after two **unpaid** hosted sessions — which is the real product, and is **not** what the merchant dialog says (004).

`Same_slot_start_twice` is the only payment-link test that uses CHIP and therefore the only one that observes `open` occupancy.

CI cannot fail red on 003/004 while the suite uses Test.

## Related files

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` **121–202**.
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` — default provider test.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **176–186** — Test auto-fulfill.

## Reproduction

Change those two tests to `chip` + FakePsp. Third start 409 with `paid_count = 0`. That is the live cashier. Tests today would not show it.

## Blast radius

False confidence on occupancy. 001 still untested concurrently. 003/004 stay green.

## Suggested fix

Keep Test for “start is paid” dogfood. Add CHIP (or Stripe FakePsp) occupancy tests that assert `open` holds the seat **or** document the TTL rule from 003. Do not claim “two people can pay” only on Test.

## Tests

This issue **is** the test work. See 001 T0, 003 CHIP walk-away.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B8
- `plans/019-evals/09-tests-inventory.md` occupancy sequential / Test rail
