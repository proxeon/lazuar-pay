# W3-LP-174 — Change plan from the buyer portal

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-174`. Tracker: *Change plan from portal* — Lazuar **N**. Alias `SL` portal.  
**Not this ID:** Admin change-plan (`LP-058`). Next-renewal policy (`LP-059`). Update payment method (`LP-173`). Cancel (`LP-056`). Stripe Billing Portal `portal-link`.

**Invariant:** A magic-link buyer can schedule a move to another **active recurring product** (same gateway + currency) on this tenant. Effect is LP-059: **no charge today**. Same command core as admin.

---

## 0. Scope lock

In scope:

- `GET .../portal/plans` (eligible products)  
- `POST .../portal/change-plan` + preview  
- `portal/page.tsx` picker + Keep current  
- Token ownership = same as cancel

Out of scope:

- Product families / groups (filter: `mo|yr`, same `gateway_name`, `is_active`, not current)  
- Immediate paid upgrade  
- Quantity stepper (show seats, don’t edit unless LP-060 portal is added — skip)  
- CommunityPortalView (still unwired)

**Blocked on LP-058.** Do not invent a second apply path.

---

## 1. Verdict

Portal is subscriptions + cancel/keep + (Wave 1) update-PM elsewhere. `PortalSubscriptionDto` has no pending plan. Chargebee/Paddle portals are why SaaS buyers expect this. HitPay/Xendit score **N** — we only need a thin picker.

---

## 2. Current files

| Path | Role |
|------|------|
| `portal/page.tsx` | Cancel / keep / paid-through |
| `PublicPortalEndpoints` | No change-plan |
| `portal.tsp` | No plans list |
| `CommunityPortalView.tsx` | Dead |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No public command |
| G2 | No eligible-product query (token-scoped) |
| G3 | No UI |

---

## 4. Recommended model

```
GET  /public/commerce/{slug}/portal/plans?token=
  → [{ id, name, interval, amount, currency }]
  exclude current ProductId, one_time, other gateway, inactive

POST /public/commerce/{slug}/portal/change-plan?token=
  { subscription_id, product_id }
  same guards as admin + client_profile ownership
  returns PlanChangePreview (amount_due_now=0)

POST keep-plan or change-plan to current id → clear pending
```

UI: only `ACTIVE` + not flagged cancel (or allow — pending + cancel-at-end is confusing; **reject** if `CancelAtPeriodEnd`). `PAST_DUE`: 400 “update payment first.”

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| TypeSpec public + portal DTO | `pending_product_name?` |
| Public endpoints | GET plans + POST change-plan |
| Shared handler with admin | One decision table |
| `portal/page.tsx` | Select + effective date + confirm |
| Tests | IDOR other sub / other tenant product |

Must not: Stripe Customer Portal; `subscription.updated`.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Token A changes A’s sub | Pending set, 0 charge |
| Token A + B’s subscription_id | 404 |
| Target other org product | 400 |
| PAST_DUE | 400 |
| Preview `amount_due_now` | 0 |

---

## 7. Acceptance

1. Buyer picks Pro; portal shows “starts {paid-through}”; ops sees pending.  
2. Due tick bills Pro (LP-058).  
3. Buyer can revert before due.  
4. No mid-cycle charge.

Tracker **N → Y** after 1–3. Without LP-058 this stays **N**.

---

## 8. Order

After LP-058 + LP-059 helper. Then public routes → portal UI → IDOR tests.

Do **not** implement from this file.
