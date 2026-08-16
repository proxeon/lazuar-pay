# W3-LP-060 — Quantity / seats on the subscription

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-060`. Tracker: *Quantity / seats* — Lazuar **N**. Aliases `SL-049`–`SL-051` / `LP-COM-007`.  
**Not this ID:** One-time checkout quantity (`LP-014` — Wave 1; persist on session/Order only). Quote line qty (already first-class). Usage meters (`LP-061` — skip). Graduated tiers (`SL-004`).

**Invariant:** Recurring seats are an integer `N` on the **Subscription**. First charge, renewal, arrears, AUTO_CHARGE, and webhook `amount` are all `unit × N`. Changing `N` follows LP-059 (next renewal). A stepper on monthly checkout without this column is a lie (LP-014 correctly hid it).

---

## 0. Scope lock

In scope:

- `Subscription.Quantity` (default 1) + optional `PendingQuantity`
- Recurring checkout stepper (FIXED `mo`/`yr` only)
- Billing / dunning / arrears / webhook use `Price * Quantity`
- Admin set-quantity (schedule)
- Portal display of seats

Out of scope:

- Entitlements service  
- Per-seat feature flags  
- Mid-cycle seat charge  
- PWYW × seats  
- Max seats on Product (optional later)

---

## 1. Verdict

`CheckoutSession.Quantity` and `Order.Quantity` exist. `Subscription` has no column. `BillingEngineJob` and `PastDueDunningProcessor` send `product.Price` only. Arrears GET is list price × 1.

LP-014’s contract still holds: Payments `Amount` is **unit**, `Quantity` is the multiplier **inside** the adapter. Renewals today pass implicit qty 1 via `ExecuteOffSessionCharge(product.Price)` — that event has **no quantity field**. Minimal fix: pass `product.Price * N` as the amount **or** add `Quantity` to the off-session event. Prefer **multiply in Commerce** and keep Payments qty=1 on renewals (same as custom/M2M). Checkout first hop should send unit + N (LP-014 rules).

---

## 2. Current files

| Path | Role |
|------|------|
| `CheckoutSession.Quantity` / `Order.Quantity` | First purchase / one-time only |
| `InitiateCheckoutCommand` | Accepts qty; recurring forced 1 (LP-014) |
| `BillingEngineJob` | `ExecuteOffSessionCharge(..., product.Price, ...)` |
| `PastDueDunningProcessor` | Same `product.Price` |
| `PublicArrearsEndpoints` | `p."Price"` |
| `CommerceWebhookPayload` | `product?.Price` |
| `CheckoutView.tsx` | Stepper only when `one_time` + FIXED |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No `Subscriptions.Quantity` |
| G2 | Renewal / dunning / arrears ignore seats |
| G3 | Recurring hop 1 forbids N>1 |
| G4 | Webhook `amount` is unit, not line |
| G5 | Manual enroll / record-payment have no seats |

---

## 4. Recommended model

```
Subscription.Quantity >= 1 (default 1)
Subscription.PendingQuantity?   // LP-059

Open checkout (recurring FIXED):
  persist session.Quantity
  first payment: Payments Amount=unit, Quantity=N  (LP-014)
  Activate(..., quantity: N)

Billing / AUTO_CHARGE / arrears:
  amount = product.Price * sub.Quantity   // after applying pending qty on due

POST /subscribers/{id}/quantity { quantity }
  schedule PendingQuantity; preview amount_due_now=0
```

Rules:

1. Cap 1–99 (same as checkout).  
2. Reminder-only: mint uses `Price * N`.  
3. Snapshot: if LP-161 adds `RecurringAmount`, set it to `Price * N` on apply.  
4. Do not store seats only on the session.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `Subscription` + migration | `Quantity` default 1; `PendingQuantity`; apply on due with plan change |
| Open-checkout + zero-amount + offline + manual | Write `Quantity` |
| `BillingEngineJob` / `PastDueDunningProcessor` | `product.Price * sub.Quantity` |
| Arrears query | Same |
| `CommerceWebhookPayload` | `amount = Price * Quantity` |
| TypeSpec | `quantity`, `pending_quantity?` on sub + portal |
| `CheckoutView` | Allow stepper for FIXED recurring |
| Admin + ops | Set seats |

Must not: change adapter multiply contract; entitlements.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Checkout N=3 monthly | Session + sub qty 3; hop 2 total 3×price |
| Billing due | Off-session amount 3×price |
| AUTO_CHARGE | Same |
| Arrears GET | 3×price |
| Schedule N=5 | qty stays 3 until due; then 5 |
| N=0 / 100 | 400 |
| Existing rows | qty 1, no double charge |

Extend `CommerceCheckoutQuantityTests`, `BillingEngineJobTests`.

---

## 7. Acceptance

1. Buyer can buy 3 seats; first charge is 3×.  
2. Next auto-debit / pay-link is 3×.  
3. Ops can schedule 5 seats; no money until renewal.  
4. Webhook `amount` matches what the gateway charged.  
5. One-time LP-014 path unchanged.

Tracker **N → Y** after 1–2. **P** if checkout works but renewals still ×1.

---

## 8. Order

1. Column + write on first activate  
2. Engine / dunning / arrears / webhook  
3. Recurring stepper  
4. Admin schedule + LP-059 preview  
5. Tests  

Do **not** implement from this file.
