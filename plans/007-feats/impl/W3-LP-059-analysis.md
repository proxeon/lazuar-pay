# W3-LP-059 — Next-renewal-only (not full proration)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-059`. Tracker: *Proration* — Lazuar **N**. Aliases `SL-043`–`SL-045` / `LP-COM-008`. **Program choice:** next-renewal-only, **not** unused-time credit.  
**Not this ID:** The swap itself (`LP-058`). Quantity (`LP-060`). Chargebee `invoice_now` / ramps (`SL-046` — refuse). Preview as a second money object.

**Invariant:** Any catalog change on a live sub (plan, seats after LP-060, price point after LP-063) takes effect at `NextBillingDate`. **Amount due today is always 0.** Copy and APIs say that out loud so we never sell “proration.”

---

## 0. Scope lock

In scope:

- Policy + preview DTO used by LP-058 / LP-060 / LP-174
- Ops + portal one-liner: “No charge today. Next bill {date}: {new amount}.”
- Tests that mid-cycle change does **not** call Payments

Out of scope:

- Unused-days credit  
- Immediate upgrade charge  
- Next-invoice credit balance  
- Invoice line items  
- Time-weighted MRR movements (LP-161 can ignore mid-cycle)

---

## 1. Verdict

Tracker row is named “Proration.” Implementing Chargebee-style credit is a new money object we do not have (`Invoice` aggregate is absent). Wave 3 asked for 10% of Chargebee. **Do the 10%:** scheduled change, zero today.

After ship, flip the tracker cell to **Y** only if the **feature name in the matrix stays “Proration (or next-renewal-only)”**. If someone relabels the row to pure proration, keep **N** and add a note. Do not mark **Y** on unused-time math that does not exist.

---

## 2. Current files

Nothing to prorate. `BillingEngineJob` sends `product.Price`. `RecordSubscriberPayment` is cash, not a change. No pending columns until LP-058/060.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No policy object / preview |
| G2 | Risk that LP-058 implementers charge a delta “to feel professional” |
| G3 | Ops/portal will otherwise imply immediate upgrade |

---

## 4. Recommended model

```
PlanChangePreview {
  current_product_id, current_amount, currency, interval
  next_product_id, next_amount
  effective_at          // NextBillingDate
  amount_due_now        // always 0
  policy                // "next_renewal"
}
```

One helper `PlanChangePolicy.Preview(sub, targetProduct, qty)` used by admin and portal. Hard-code `amount_due_now = 0`. Reject `apply: "immediate"` / `prorate: true` with 400.

If a later tenant demands true proration, that is a **new ID**, not a tweak to this ticket.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New `PlanChangePolicy.cs` | Preview + guards; no I/O |
| TypeSpec | `PlanChangePreviewDto` + field on change-plan response |
| Ops / portal copy | Effective date + “no charge today” |
| `BillingEngineJob` | Already delayed if LP-058 applies pending **on due** — do not add a charge here |

Must not: ledger credit lines; `ExecuteOffSessionCharge` from the change-plan handler.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Preview mid-cycle | `amount_due_now=0`, `effective_at=NextBillingDate` |
| Handler `prorate=true` | 400 |
| Change-plan handler | Zero `ExecuteOffSessionCharge` / `GenerateCheckoutSession` |
| Billing still charges **once** on due at the **new** price | Existing job tests + LP-058 |

---

## 7. Acceptance

1. Every change-plan / set-quantity response includes `policy=next_renewal` and `amount_due_now=0`.  
2. No gateway call at schedule time.  
3. UI never says “you’ll be credited” or “pay the difference now.”  
4. Full unused-time proration is **not** shipped.

Tracker: document the policy on the row. **Y** for next-renewal-only; do not claim Stripe-style proration.

---

## 8. Order

Land the helper in the same PR as LP-058. Do not open a proration epic.

Do **not** implement from this file.
