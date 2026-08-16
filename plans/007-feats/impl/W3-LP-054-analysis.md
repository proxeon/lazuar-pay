# W3-LP-054 — Free trial (`TRIALING`)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-054`. Tracker: *Free trial (`TRIALING`)* — Lazuar **N**. Aliases `SL-012` / `BE-008` / `LP-COM-011`.  
**Not this ID:** Zero-amount / 100% coupon first period (`SL-017` — that path is `ACTIVE` then surprise `PAST_DUE`). Complimentary enroll `COMPED` (`SL-018`). Setup fees (`LP-062` — skip). Paid intro / Trial Offer (`SL-014` — refuse).

**Invariant:** A recurring product can grant timed access with **no first charge**. Status is `TRIALING` until `TrialEndsAt`. The billing job **must not** charge or mint during the trial. On trial end it either auto-debits (vaulted), mints a pay link (reminder-only), or cancels if the product says so. Integrators learn about access on `subscription.activated` at trial **start**, and about money only when a real charge succeeds or fails.

---

## 0. Scope lock

In scope:

- `Product.TrialDays` (0 = off)
- `Subscription` status `TRIALING` + `TrialEndsAt`
- Checkout hop 1 copy + $0 / setup-future first hop
- Billing job skip-until-trial-end, then existing vault / reminder branches
- Ops product field + subscriber badge
- Outbound webhook **status union** grows `TRIALING` (frozen catalog otherwise untouched)

Out of scope:

- Trial without any identity (must still collect email)
- Card-required vs card-optional as two products — one rule: **vault if the gateway can; otherwise reminder-only trial**
- Trial-end email CMS (existing pre-dunning / day-0 is enough)
- `subscription.updated` (still forbidden)
- Changing `PENDING` semantics

---

## 1. Verdict

Tracker **N** is correct. `TRIALING` is a ghost string in GDPR cancel. No `TrialDays`, no write path, no clock. A $0 first invoice is **not** a trial: `ProcessZeroAmountCheckout` activates `ACTIVE` with `NextBillingDate = now+interval`, then `BillingEngineJob` past-dues a Billplz member.

Adding `TRIALING` without teaching the billing claim to skip it would **bill the trial** (`NOT IN ('PENDING','PAST_DUE','SUSPENDED','CANCELED')` plus cancel-at-period-end). That is the P0.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Commerce/Domain/Aggregates/Product.cs` | No trial field |
| `Modules/Commerce/Domain/Aggregates/Subscription.cs` | Constructor `PENDING`; `Activate` → `ACTIVE` |
| `Modules/Commerce/Infrastructure/Workers/BillingEngineJob.cs` | Would claim a due `TRIALING` row |
| `Modules/Commerce/Infrastructure/EventHandlers/ClientProfileAnonymizedIntegrationEventHandler.cs` | Mentions `TRIALING` only |
| `packages/api-spec/modules/commerce/models/webhooks.tsp` | Status union: `ACTIVE \| PAST_DUE \| CANCELED \| SUSPENDED` |
| `ProductForm.tsx` | Interval + price; no trial days |
| `CheckoutView.tsx` / `OrderSummaryCard.tsx` | No trial copy |
| `ProcessZeroAmountCheckout` (Commerce) | Free first period ≠ trial |

---

## 3. Exact gaps

| # | Sev | Gap |
|---|-----|-----|
| G1 | P0 | No `TrialDays` / `TrialEndsAt` / `ActivateTrial` |
| G2 | P0 | Billing would charge a hypothetical `TRIALING` due row |
| G3 | P0 | First checkout always charges `product.Price` (or zero-amount → wrong clock) |
| G4 | P1 | Webhook status union cannot legally say `TRIALING` |
| G5 | P1 | Hop 1 and ops hide the offer |
| G6 | P2 | Pre-dunning would email “renews” during a trial if `NextBillingDate` is trial end |

**Not gaps:** COMPED; 100% coupon; Stripe Billing `trial_end` on their object (we are not Stripe Billing).

---

## 4. Recommended model

```
Product.TrialDays > 0 and interval in (mo, yr)
  hop 1: "N-day trial, then RM X / month. Cancel anytime during trial."
  hop 2: amount 0 + setup_future_usage if SupportsOffSession
         else reminder-only trial (no vault)
  → Subscription.ActivateTrial(trialEndsAt)
       Status = TRIALING
       NextBillingDate = TrialEndsAt
       IsReminderOnly = !vaulted
  → outbound subscription.activated (status TRIALING, is_first_payment true, amount 0)

BillingEngineJob when Status == TRIALING && NextBillingDate <= now:
  same as ACTIVE due:
    vaulted → attempt 1
    reminder-only → mint + PAST_DUE
  do not leave TRIALING past the clock

Trial + CancelAtPeriodEnd before TrialEndsAt:
  billing finalize Cancel() — same as LP-056 (no charge)
```

Rules:

1. `TrialDays` integer 1–90. `0` / null = off. One-time products reject non-zero.
2. Do **not** reuse zero-amount coupon path. Coupon + trial is “discount after convert,” not this ticket.
3. No-card Billplz trial is legal: it is reminder-only with a later first bill.
4. Access: integrators treat `subscription.activated` as grant. Trial cancel still `subscription.canceled`.
5. Do not add `TRIALING` to the billing **exclusion** list — the due tick **is** convert. Skip only while `NextBillingDate > now` (already true for future dates).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `Product` + TypeSpec + migration | `TrialDays int not null default 0` |
| `Subscription` + migration | `TrialEndsAt?`; `ActivateTrial(DateTime endsAt, bool reminderOnly)` sets `TRIALING`, both clocks = `endsAt` |
| `InitiateCheckout` / zero-amount / offline | If `TrialDays > 0` and recurring: first charge **0**, `setupFutureUsage` if vaulting gateway; create sub via `ActivateTrial` not `Activate` |
| `BillingEngineJob` | After product load: if `TRIALING` and not yet due, return (should not be claimed). If due, fall through to existing vault/mint. On successful convert, `Activate(...)` as today |
| `GatewayPaymentCompleted` subscription path | `TRIALING` + first real money → `Activate` / `RecoverFromPayment` |
| `webhooks.tsp` + `CommerceWebhookPayload` | Add `TRIALING` to status union; amount 0 on trial activate |
| `ProductForm` + hop 1 | Trial days; “then RM X / interval” |
| `CommerceSubscriptionDto` / portal DTO | `trial_ends_at?` |
| Pre-dunning claim | Optional: exclude `TRIALING` so we do not say “renewal” |

Must not: Stripe Billing subscriptions; a second worker; `subscription.trial_will_end` event.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Product `TrialDays=14`, Stripe checkout | Session amount 0 + setup future; sub `TRIALING`, `TrialEndsAt ≈ now+14d`, `NextBillingDate` same |
| Billing before trial end | Untouched |
| Billing at/after trial end, vaulted | Attempt 1 at **catalog** price (or `Price×Quantity` after LP-060); status leaves `TRIALING` only after paid handler |
| Billing at trial end, Billplz | Mint + `PAST_DUE`; no silent charge |
| Cancel during trial | `CANCELED` + canceled event; no charge |
| `TrialDays` on `one_time` | 400 |
| Anonymize | Still cancels `TRIALING` |
| Webhook activate | `status=TRIALING`, `amount=0` |

Extend `BillingEngineJobTests`, `CommerceProductCompletenessTests`, new `SubscriptionTrialTests`.

---

## 7. Acceptance

1. Merchant sets 7/14/30 trial on a monthly product and hop 1 states it.  
2. Buyer finishes hop 2 with **no** first-period charge; row is `TRIALING` with a visible end date.  
3. Before that date, hourly billing does nothing.  
4. On that date, vaulted → off-session at list price; reminder-only → pay-link + `PAST_DUE`.  
5. Portal/ops show trial end; cancel works (LP-056 period-end is fine if paid-through is trial end).  
6. Docs do not call a 100% coupon a trial.

Tracker **N → Y** after 1–4. **P** if engine works but hop 1 / ops field missing.

---

## 8. Order

1. Domain + migration + billing due behavior  
2. Checkout $0 + vault flag  
3. Webhook union  
4. Ops + hop 1  
5. Tests §6  

Do **not** implement from this file.
