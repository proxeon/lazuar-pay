# W3-LP-058 — Plan change (swap product on a live sub)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-058`. Tracker: *Plan change* — Lazuar **N**. Aliases `SL-042` / `LP-COM-012`.  
**Not this ID:** Money math (`LP-059` — **next-renewal-only**, no invoice-now). Seats (`LP-060`). Multi-price SKU (`LP-063`). Portal picker (`LP-174`). Cancel+re-checkout (today’s workaround).

**Invariant:** An `ACTIVE` (or `TRIALING`) subscription can point at a **different recurring product in the same org** without a second checkout. The swap is **scheduled**. The current product and price stay in force until `NextBillingDate`. No mid-cycle charge.

---

## 0. Scope lock

In scope:

- `PendingProductId` on `Subscription`
- Admin `POST /subscribers/{id}/change-plan`
- Billing due: apply pending product **then** charge/mint that product
- Ops picker + “changes on {paid-through}”
- Undo (clear pending)

Out of scope:

- Proration / credit / `invoice_now` (LP-059 explicitly refuses these)  
- Cross-gateway swap (vault is per gateway)  
- `one_time` target  
- `subscription.updated`  
- Immediate product swap that mutates `ProductId` before the clock

---

## 1. Verdict

There is no `ChangePlan` command, no setter on `ProductId` after construct, no pending column. “Upgrade” today is cancel (immediate or period-end) + new link. That drops the vault token story and the integrator id.

Price edits on the **old** product already mutate everyone’s next charge — that is not plan change.

---

## 2. Current files

| Path | Role |
|------|------|
| `Subscription` | `ProductId` ctor-only |
| `SubscriberEndpoints` | cancel / keep / record-payment / dunning — no change-plan |
| `BillingEngineJob` | Loads `product` from **current** `ProductId` |
| `CommerceWebhookPayload` | `product_id` from current row |
| `SubscribersPage.tsx` | No plan control |
| Portal DTO | `product_id` / `product_name` only |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Cannot schedule or apply a product swap |
| G2 | Billing cannot charge the new catalog row |
| G3 | No preview (“next bill RM X on date”) |
| G4 | No same-gateway / recurring guard |

---

## 4. Recommended model

```
POST /admin/commerce/subscribers/{id}/change-plan
  { product_id }

Guards:
  status ACTIVE | TRIALING
  target.OrganizationId match
  target.IsActive
  target.Interval in (mo, yr)
  target.GatewayName == current product.GatewayName
  target.Currency == current.Currency
  target.Id != current.Id (or != pending)

Effect:
  PendingProductId = target
  ProductId unchanged
  no event

Billing due (before vault/mint):
  if PendingProductId:
    ProductId = PendingProductId
    PendingProductId = null
    reload product
  then existing charge/mint

Undo: POST .../change-plan { product_id: null } or POST .../keep-plan
```

Same-interval only in v1 (mo→mo, yr→yr). Interval change is a calendar rewrite; refuse with 400 “create a new checkout.”

Webhook: next `subscription.activated` (renewal success) or `subscription.past_due` carries the **new** `product_id`. No extra type.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `Subscription` + migration | `Guid? PendingProductId`; `SchedulePlanChange` / `ClearPendingPlanChange` / `ApplyPendingPlanChange` |
| `BillingEngineJob` | Apply pending before price/gateway read |
| New command + `SubscriberEndpoints` | change-plan + clear |
| TypeSpec subscriber | `pending_product_id?`, `pending_product_name?` |
| `SubscribersPage` | Select + effective date = `next_billing_date` |
| Query map | Join pending product name |

Must not: write `ProductId` in the HTTP handler; charge a delta; allow Stripe↔Billplz.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Schedule A→B | `ProductId=A`, pending=B, status unchanged, 0 events |
| Billing due | `ProductId=B`, pending null, charge `B.Price` |
| Foreign org / one_time / other gateway | 400 |
| Clear pending | next bill still `A.Price` |
| IDOR other tenant product | 400 not found |

`BillingEngineJobTests` + `ChangePlanCommandHandlerTests`.

---

## 7. Acceptance

1. Ops schedules Basic→Pro; subscriber still Basic until paid-through.  
2. Due tick charges Pro (or mints a Pro-priced Billplz link).  
3. Undo before due keeps Basic.  
4. No proration line, no same-day second charge.  
5. Portal change is **not** required (LP-174).

Tracker **N → Y** after 1–3. Pair with LP-059 copy so merchants are not promised credit.

---

## 8. Order

Implement **with** [W3-LP-059](./W3-LP-059-analysis.md) (policy) then [W3-LP-174](./W3-LP-174-analysis.md) (portal).

1. Column + apply-on-due  
2. Admin API  
3. Ops UI  
4. Tests  

Do **not** implement from this file.
