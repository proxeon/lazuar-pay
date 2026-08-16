# W3-LP-076 — Hard vs soft decline handling

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-076`. Tracker: *Hard vs soft decline handling* — Lazuar **N**. Alias `LP-DUN-007` (sequencing put this in Wave 4; **tracker Wave 3 wins**).  
**Not this ID:** Smart Retries ML. Extra attempt budget. WhatsApp (`LP-074`). Entry to `PAST_DUE` (`LP-071` — done). AUTO_CHARGE existence (`LP-072` — done).

**Invariant:** Soft declines (NSF, generic `charge_declined`) may use remaining AUTO_CHARGE slots. Hard declines (lost/stolen, incorrect number, pickup, revocation, `transaction_not_allowed`, `authentication_required`) **must not** create another off-session PaymentIntent. Conversation (email + update-payment) still runs. Attempt rows can exist as `FAILED`/`SKIPPED` so the offset is consumed and the hourly job does not thrash.

---

## 0. Scope lock

In scope:

- Classify Stripe (and pass-through CHIP) decline codes  
- Persist class on `ChargeAttemptLog`  
- AUTO_CHARGE skip when the cycle already has a **hard** failure  
- Copy: “update your card” vs “try again shortly”

Out of scope:

- ML / payday windows  
- Issuer-specific retry tables  
- Billplz (no vault — no AUTO_CHARGE)  
- Changing max 4 attempts  
- New webhook type

---

## 1. Verdict

`ChargeAttemptLog` already has `GatewayResponseCode` and `FailureReason`. Off-session publish uses coarse reasons: `charge_declined`, `charge_exception`, `off_session_not_supported`. Stripe `MapPaymentIntentPaymentFailed` puts `LastPaymentError.Message` in `Error` and **does not** copy `DeclineCode` into metadata. Commerce never reads a class. Default campaign still AUTO_CHARGEs days +1 and +5 after a stolen-card decline.

That burns Stripe merchant health. This ticket is a **static table + one if**.

---

## 2. Current files

| Path | Role |
|------|------|
| `ChargeAttemptLog.MarkFailed` | Reason + optional code |
| `ExecuteOffSessionChargeIntegrationEventHandler` | `failure_reason` coarse |
| `StripeGatewayAdapter.MapPaymentIntentPaymentFailed` | Message only |
| `StripeGatewayAdapter.ChargeOffSessionAsync` | Catch `StripeException` → `false` (code dropped) |
| `PastDueDunningProcessor` AUTO_CHARGE | Caps + vault + reminder-only; no class |
| `GatewayPaymentFailedIntegrationEvent` | Metadata dict only — no first-class code |
| `ChargeAttemptLimits` | Max 4 |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Decline code not propagated from Stripe PI fail / off-session exception |
| G2 | No classifier |
| G3 | AUTO_CHARGE retries hard codes |
| G4 | Email copy does not distinguish “card will never work” |

---

## 4. Recommended model

Hard set (Stripe Billing docs, copy literally):

`incorrect_number`, `lost_card`, `pickup_card`, `stolen_card`, `revocation_of_authorization`, `revocation_of_all_authorizations`, `authentication_required`, `highest_risk_level`, `transaction_not_allowed`

Everything else, including missing code and `charge_declined` / NSF: **soft**.

```
metadata.decline_code + metadata.decline_class = hard|soft
ChargeAttemptLog.DeclineClass

AUTO_CHARGE:
  if cycleAttempts.Any(hard FAILED): skip charge, consume offset
     (log SKIPPED or MarkFailed("hard_decline_skip"))
  else: existing path
```

New card (update-payment success) starts a **new** billing date / clears the cycle — existing recover path. Do not special-case further.

CHIP: if no code, soft. Razorpay: soft until 044 maps codes.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `DeclineClassifier.cs` (Commerce.Domain) | Static hard set |
| Stripe map + off-session catch | Put `decline_code` on PI metadata / failed-event metadata |
| `ExecuteOffSessionCharge…` | Pass Stripe error code through, not only `charge_declined` |
| `ChargeAttemptLog` + migration | `DeclineClass` varchar nullable |
| Failed handler | Classify when marking failed |
| `PastDueDunningProcessor` | Skip AUTO_CHARGE on hard in-cycle |
| Default seed (new orgs) | Optional: day-0 body mentions new card if hard — skip if noisy |
| Ops subscriber | Show last decline class |

Must not: change attempt max; skip EMAIL; train a model.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Classifier table | Each hard code → hard; `insufficient_funds` → soft; null → soft |
| Stripe map includes `decline_code` | Unit on `MapPaymentIntentPaymentFailed` |
| AUTO_CHARGE after hard | Zero `ExecuteOffSessionCharge`; offset consumed |
| AUTO_CHARGE after soft | Charge as today (attempt 2–4) |
| Email day 0 still sends after hard | Dispatcher still EMAIL |

`PastDue` / `DunningEngineJobTests` + `DeclineClassifierTests`.

---

## 7. Acceptance

1. Stolen-card off-session: one failed attempt, later AUTO_CHARGE steps do **not** hit Stripe.  
2. NSF: retries still fire within the 4-cap.  
3. Buyer still gets the recovery email + update-payment.  
4. Ops can see hard vs soft on the last attempt.

Tracker **N → Y** after 1–2.

---

## 8. Order

1. Propagate code  
2. Classifier + column  
3. AUTO_CHARGE gate  
4. Tests  

Do **not** implement from this file.
