# W3-LP-057 — Pause / resume as a product action

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-057`. Tracker: *Pause / resume* — Lazuar **P** (dunning pause only). Aliases `SL-036`–`SL-038`.  
**Not this ID:** Pause **dunning** (`LP-080` / `DunningPausedUntil` — already **Y**). Dunning terminal `SUSPEND` (`LP-078`). Cancel at period end (`LP-056`). Plan change (`LP-058`).

**Invariant:** A merchant (then optionally a buyer) can stop **collection** on an `ACTIVE` membership without revoking access and without starting dunning. Billing skips charge and skip-mints until resume. Resume is a date, not a payment.

---

## 0. Scope lock

In scope:

- Collection pause / resume on `ACTIVE` (admin first)
- Billing + pre-dunning skip while paused
- Ops subscriber actions distinct from “Pause dunning”
- Status stays `ACTIVE` (frozen webhook union has no `PAUSED`)

Out of scope:

- New status `PAUSED` / new outbound type
- Pause-at-period-end schedule (nice; not required)
- Portal self-serve pause (Chargebee-shaped; add only if admin path is one extra button)
- Using `Resume()` (that method is **arrears recovery** from `SUSPENDED`)

---

## 1. Verdict

Tracker **P** is honest: ops can pause **emails/AUTO_CHARGE**, but `BillingEngineJob` **ignores** `DunningPausedUntil`. An ACTIVE Stripe sub still auto-debits on the due tick. There is no “holiday, keep the gym pass, don’t bill.”

Do **not** reuse `SUSPENDED`. That fires `subscription.suspended` and integrators revoke access.

Do **not** add `PAUSED` to the frozen status union in this wave. Mirror LP-056: a **flag** on an otherwise `ACTIVE` row.

---

## 2. Current files

| Path | Role |
|------|------|
| `Subscription.PauseDunning` / `ResumeDunning` | Recovery mute only |
| `SubscriberEndpoints` `POST .../dunning/pause` | Ops modal |
| `Subscription.Resume(newNextBilling)` | Clears dunning; for `SUSPENDED` after pay |
| `BillingEngineJob` | No collection-pause branch |
| `DunningEngineJob.Claim` | Pre-dunning does not check dunning pause; past-due does |
| Frozen `webhooks.tsp` | No `PAUSED` |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No `CollectionPausedUntil` (or equivalent) |
| G2 | Billing still charges / mints a “paused” customer |
| G3 | Ops copy conflates dunning pause with billing pause |
| G4 | `Resume()` name collision — do not overload |

---

## 4. Recommended model

```
ACTIVE + CollectionPausedUntil > now
  BillingEngineJob: skip (do not Cancel, do not PAST_DUE)
  Pre-dunning: skip
  Access webhooks: none
  MRR (LP-161): exclude or keep — pick exclude (holiday ≠ committed)

POST /subscribers/{id}/collection/pause { resume_on }
  ACTIVE only, resume_on > now
  sets CollectionPausedUntil

POST /subscribers/{id}/collection/resume
  clears flag; if NextBillingDate < now, set NextBillingDate = now + 1 interval
  (do not back-charge the holiday)
```

Rules:

1. Pause dunning buttons stay. Label them **Pause recovery**.  
2. Collection pause **implies** no AUTO_CHARGE (nothing is due).  
3. If due tick fires while paused, **do not** roll `NextBillingDate`. The holiday holds the clock; resume pushes the next bill forward from resume instant (simple, no proration).  
4. `CancelAtPeriodEnd` + collection pause: cancel still wins on the due tick (LP-056 finalize).  
5. `PAST_DUE` / `SUSPENDED`: reject collection pause — they are already not billing.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `Subscription` + migration | `DateTime? CollectionPausedUntil`; `PauseCollection(until)`; `ResumeCollection(nextBill)` |
| `BillingEngineJob` | After cancel-at-period-end branch: if `CollectionPausedUntil > now` return |
| `DunningEngineJob.Claim` | Pre-dunning exclude collection pause |
| `SubscriberEndpoints` + TypeSpec | `POST .../collection/pause` + `.../resume` |
| `CommerceSubscriptionDto` | `collection_paused_until?` |
| `SubscribersPage.tsx` | Pause collection vs Pause recovery — two verbs |

Must not: new webhook; mutate `Resume()`; pause from portal in v1.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Pause ACTIVE + future resume | Flag set, status `ACTIVE`, zero events |
| Billing due while paused | No off-session, no mint, still `ACTIVE` |
| Resume after due date passed | `NextBillingDate` in the future; next job **does** charge |
| Pause `PAST_DUE` | 400 |
| Dunning pause still independent | Collection running + dunning paused = charge yes, email no |

`BillingEngineJobTests` + new `SubscriptionCollectionPauseTests`.

---

## 7. Acceptance

1. Ops can pause collection on a healthy sub and the next due tick does not take money.  
2. Access stays granted (`ACTIVE`, no `subscription.suspended`).  
3. Resume without a payment; the following cycle bills once.  
4. UI does not call dunning pause “pause subscription.”

Tracker **P → Y** after 1–3. Leave **P** if only copy changes.

---

## 8. Order

1. Flag + billing skip  
2. Admin routes  
3. Ops verbs  
4. Tests  

Do **not** implement from this file.
