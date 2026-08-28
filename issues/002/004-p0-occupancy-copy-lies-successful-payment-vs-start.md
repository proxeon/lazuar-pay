---
number: "004"
id: PAY-OCC-004
severity: P0
status: resolved
source: plans/019-evals/05-payment-links-occupancy.md
head: "9f04ad58"
---

# 004 — Occupancy copy lies: “successful payment” vs start

- **Severity:** P0 (product-false with cash effect)
- **Status:** open
- **Source:** `plans/019-evals/05-payment-links-occupancy.md` B5 (also `10-honesty-bugs-gaps.md` P1-1)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Three surfaces disagree on what a “payer” is.

| Surface | Claim | Live |
|---------|-------|------|
| Merchant dialog, capacity `one` | “The link closes after one **successful payment**.” | Closes after one `open` child (a start). |
| Merchant limited column | `taken / max_payers` | Counts starts + paid. |
| Merchant unlimited column | `paid paid · unlimited` | Uses `paid_count` only. Mixed definition. |
| API `PaymentLinkView.Status` | `"full"` or `"open"`, never `"paid"` | Merchant remaps max=1 full+paid → `paid`. Public GET for that case returns checkout `"paid"`. |
| Buyer full card | “no remaining payments” | May mean no remaining **starts**. |

Staff who mint “1 person only” believe the cousin can still pay until Ada completes CHIP. Live: Ada’s click Pay fills the link.

Fixing 001–003 without rewriting this copy leaves the product lying.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **89–101** — `payersLabel` / `statusLabel`.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **397–400** — “The link closes after one successful payment.”
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs` **5–6** — `open` or `paid`.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` **156–176** — `Status = full ? "full" : "open"`.
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` **66–75** — max=1 paid leak to GET without slot.
- `apps/lazuar-pay-checkout/src/App.tsx` **226–239** — “Link is full” / “no remaining payments”.

## Reproduction

1. Open `:5178` → Pay links → Create → “1 person only”.
2. Read the helper: “closes after one successful payment.”
3. Buyer starts CHIP, does not pay.
4. Merchant table: `1 / 1`, status `full` (not `paid`, because `paid_count` is 0 — `statusLabel` only remaps when paid).
5. Second buyer: SPA “Link is full.”

## Blast radius

Every merchant who uses the default capacity. Support tickets: “the link is full but nobody paid.” WhatsApp SMEs will not distinguish start vs paid.

## Suggested fix

Write the product rule in **one** place and make three surfaces quote it:

- If 003 picks TTL reservation: copy becomes “closes after someone starts Pay; unpaid starts free after N minutes” **or** keep “successful payment” and implement paid-only occupancy + expire `open`.
- Align unlimited vs limited columns on the same grain.
- Host `status` for max=1 paid should be one spelling (`paid` **or** `full`), documented in pay-spec.

Do not leave host counting `open` and the dialog saying payment.

## Tests

- Existing merchant `locks.test.ts` greps `'1 person only'` and `unlimited` — it does **not** lock the helper sentence against host behaviour.
- Missing: a lock that the helper matches `CountsTowardCapacity`, **or** a host test named after the written rule (see 003). Prefer one product test, not a grep of a lie.

## Source reports

- `plans/019-evals/05-payment-links-occupancy.md` §B5
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-1
- `plans/019-evals/02-merchant-frontend.md` capacity mismatches
