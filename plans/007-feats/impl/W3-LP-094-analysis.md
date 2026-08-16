# W3-LP-094 — Disputes / chargebacks as first-class

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-094`. Tracker: *Disputes / chargebacks as first-class* — Lazuar **P**.  
**Not this ID:** Utility credit clawback (already in `ChargebackClawbackHandler`). Hub SaaS `MarkPastDue`. Settlement / payout reports (`LP-095` — refuse). Refunds (`LP-091`–`093`). Won/lost evidence packs.

**Invariant:** A Stripe `charge.dispute.created` on a **Commerce GMV** payment is a visible row, reverses the matching sales ledger (idempotent), and flags the subscription. We do **not** auto-cancel access on create (the tenant is the merchant; they decide). We do **not** invent a PayNet chargeback desk for FPX.

---

## 0. Scope lock

In scope:

- Persist a `Dispute` (or reuse a thin Payments table)  
- Commerce handler for `GatewayDisputeCreated` when metadata is a checkout/sub (not only `utility_credit_topup`)  
- Ledger contra of the original `GATEWAY_PAYMENT`  
- Ops list + transaction badge  
- Stripe `created` (already parsed)

Out of scope:

- `charge.dispute.closed` / won / lost (optional follow-up)  
- Billplz/CHIP dispute APIs (they barely have them)  
- Outbound `dispute.created` (frozen catalog)  
- Representment / evidence upload  
- Auto-`Cancel()` (too early; money is disputed, not lost)

---

## 1. Verdict

Payments already maps Stripe `charge.dispute.created` → `DISPUTE_CREATED` → `GatewayDisputeCreatedIntegrationEvent` (amount + PI metadata). Billing handles **platform** types only and **returns** on everything else. Commerce has **zero** consumer. Ops has no disputes page. That is why the cell is **P**, not **N**.

---

## 2. Current files

| Path | Role |
|------|------|
| `StripeGatewayAdapter` dispute branch | Fetches PI metadata |
| `ProcessGatewayWebhookCommandHandler` | Publishes dispute event |
| `ChargebackClawbackHandler` | Utility + Hub SaaS only; comment says GMV out of scope |
| Commerce event handlers | No dispute |
| `TransactionsPage.tsx` | Refunds, not disputes |
| CHIP / Billplz / Razorpay adapters | No `DISPUTE_CREATED` |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No durable dispute row |
| G2 | No GMV ledger reverse |
| G3 | Subscription stays `ACTIVE` with no ops signal |
| G4 | No UI |
| G5 | Closed/won not mapped (acceptable for v1) |

---

## 4. Recommended model

```
payments.Disputes or commerce.Disputes
  Id, OrganizationId, GatewayTransactionId, Amount, Currency, Status=OPEN
  SubscriptionId?, LedgerEntryId?
  unique (OrganizationId, GatewayTransactionId)   // replay

On GatewayDisputeCreated:
  upsert OPEN
  if metadata.type is platform → existing Billing handler only
  if subscription_id / checkout:
     reverse GATEWAY_PAYMENT lines (same shape as refund contra)
     stamp transaction log DISPUTED
     set sub.HasOpenDispute = true  // or just query by tx
     do not MarkAsPastDue / Cancel
```

Ops: table under Transactions or a slim Disputes page: date, amount, sub, “open in Stripe.”

v1 Stripe-only. Other adapters stay silent (honest).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| New entity + migration | Dispute |
| New Commerce (or Billing) handler | GMV reverse + upsert; skip platform types |
| `ChargebackClawbackHandler` | Unchanged for utility |
| TypeSpec + `GET /admin/commerce/disputes` | List |
| Ops page or transaction badge | OPEN |
| Tests | Idempotent reverse; no cancel event |

Must not: new outbound event; auto-suspend; pretend Billplz has chargebacks.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Stripe dispute + `subscription_id` | One dispute row; ledger contra once; sub not `CANCELED` |
| Replay same `GatewayTransactionId` | Still one row / one contra |
| `type=utility_credit_topup` | Commerce handler no-ops; Billing clawback still runs |
| No metadata | Persist dispute, no sub mutation |

---

## 7. Acceptance

1. Card chargeback appears in ops the same day as Stripe’s event.  
2. Sales ledger nets the disputed amount (not only credits wallet).  
3. Subscription is findable from the row; access still `ACTIVE` until the merchant cancels.  
4. Utility top-up path unchanged.

Tracker **P → Y** after 1–2. Won/lost can stay later.

---

## 8. Order

1. Table + upsert from existing event  
2. Ledger reverse  
3. Ops list  
4. Tests  

Do **not** implement from this file.
