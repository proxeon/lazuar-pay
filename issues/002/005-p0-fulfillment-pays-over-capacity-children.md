---
number: "005"
id: PAY-OCC-005
severity: P0
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 005 — Fulfillment pays over-capacity children

- **Severity:** P0 (given 001)
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B7 (also `06-rails-webhooks-fulfillment.md` G13)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`FulfillPaidAsync` only refuses when the **checkout** is not `open` (or amount ≤ 0, or missing). It does not know about payment links. After 001 over-admits, every extra `open` child is a live payable object. Plane B amount/currency checks use the child’s snapshot (copied from the link), so they **match**. Each extra webhook mints `RCPT-` and a journal pair.

There is no refund path, no “ignore paid because the link is full”, no auto-expire of extras.

001 without 005 still creates extra PSP sessions. 005 is why those sessions become extra Official Receipts.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` **13–37** — `if (checkout.Status != "open") return;` then `checkout.Status = "paid"`.
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` **143–154** — TX around event insert + fulfill. No occupancy re-check.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` **15–34** — `CheckoutRow.PaymentLinkId` unused by fulfill.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` — not called from Money/.

## Reproduction

Depends on 001:

1. Force two `open` children on `max_payers=1` (concurrent start, or two slots before a lock exists).
2. Deliver both PSP paid webhooks.
3. Two `documents` titled Official Receipt, two `charges`, `taken_count = 2`.

On Test rail, start **is** fulfill — concurrent Test starts skip Plane B and still double-book via `IFulfillPaid` in `Start`.

## Blast radius

Same as 001, plus the books: Official Receipt numbers and cash/revenue lines for payments the merchant did not intend to accept. No refund door on this host to unwind them.

## Suggested fix

After 001’s parent lock, extras should not exist. Still belt fulfill:

- If `PaymentLinkId` is set, re-read occupancy (or a stored `taken`) **inside the same TX** as `open → paid`. If the link is already at cap **in paid seats**, do not fulfill; return ignored / 409 and do not consume a hostile retry forever without a named rule.
- Unique `charges (CheckoutId)` (see 010) so two grains on the **same** child do not double.

Do not put the occupancy algorithm inside every rail parser. Do not skip 001 and only filter in fulfill — extra PSP sessions would still charge the buyer.

## Tests

- Missing: after a fixture that inserts two `open` children on max=1, second fulfill does not mint a second `RCPT-` **if** the product rule is “cap is paid”. If the rule is “cap is starts” and 001 is closed, this test is “no second child exists.” Name the rule in the test.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B7
- `plans/019-evals/06-rails-webhooks-fulfillment.md` §G13
- `plans/019-evals/10-honesty-bugs-gaps.md` §P0-1 race step 5
