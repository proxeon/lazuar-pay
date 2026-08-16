# W4-LP-032 — FPX e-mandate (true auto-debit)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-032`. Tracker: *FPX e-mandate* — Lazuar **N**.  
**Not this ID:** FPX **one-time** retail (already hosted on Billplz/CHIP). Reminder-only Billplz (`LP-053`). Homemade PayNet integration. Stripe Billing + FPX (Stripe FPX is **not** recurring).

**Invariant:** True FPX auto-debit is a **mandate on Curlec (Razorpay MY) or Xendit**, not a Lazuar rail. We store their mandate/token like a card vault and run `ChargeOffSessionAsync`. If neither adapter exposes e-mandate, **do not ship this ID**.

---

## 0. Scope lock

In scope:

- Collection mode `emandate` on a recurring product (gateway = RAZORPAY or XENDIT)  
- Hop 1 copy: bank authorization, not “card on file”  
- Capability `SupportsEmandate`  
- Billing uses the mandate token (existing off-session event)

Out of scope:

- Talking to PayNet  
- Billplz e-mandate (they do not vault)  
- Corporate FPX B2B1 as a separate mode  
- Building a bank-picker UI (hosted page does that)

**Depends on:** [W4-LP-044](./W4-LP-044-analysis.md) and/or [W4-LP-045](./W4-LP-045-analysis.md).

---

## 1. Verdict

Billplz/CHIP hosted FPX is **one-shot**. Recurring Billplz is pay-link. Stripe FPX is not Billing. The adult MY answer is Curlec or Xendit. We have neither e-mandate path today (Razorpay registration is `method=card`).

---

## 2. Current files

| Path | Role |
|------|------|
| `PaymentGatewayCapabilities` | Off-session Stripe/CHIP only |
| `RazorpayGatewayAdapter` | Card registration link |
| No Xendit | |
| Product form | Reminder-only vs auto-debit (card) |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No emandate checkout option |
| G2 | No capability bit |
| G3 | Hop 1 would say “card saved” if we reuse vault copy |

---

## 4. Recommended model

```
Product.GatewayName in (RAZORPAY, XENDIT)
Product.CollectionMode? = auto | reminder | emandate
  or infer: setupFutureUsage + SupportsEmandate(gateway)

Hop 1: "Authorize FPX debit. Your bank will show a mandate."
Hop 2: adapter creates mandate/registration (not a one-time bill)
Webhook: token ids → StoreVaultedToken
Billing: existing ExecuteOffSessionCharge
```

If the chosen processor cannot create a mandate in sandbox, **delete the product toggle** and keep reminder-only. Do not fake auto-debit.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Capabilities | `SupportsEmandate` |
| Razorpay and/or Xendit adapter | Mandate create + parse token |
| Product form | E-mandate mode when gateway supports it |
| Hop 1 | Bank-mandate copy |
| Tests | Reminder-only still default for Billplz |

Must not: PayNet certificates; new worker.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Billplz product | Cannot select emandate |
| Razorpay/Xendit + emandate checkout | `setupFutureUsage` path hits mandate API (unit with HTTP fake) |
| Token stored | `IsReminderOnly=false` |
| Billing due | Off-session, not mint |

---

## 7. Acceptance

1. A sandbox Curlec **or** Xendit mandate renews once without the buyer opening a new bill.  
2. Billplz products cannot claim e-mandate.  
3. Docs: “FPX auto-debit via {processor}, not Lazuar.”

Tracker **N → W**. If no processor API works, leave **N** and do not add UI.

---

## 8. Order

Pick **one** processor (prefer the one a paying tenant already has). Finish 044 or 045 first. Then this toggle.

Do **not** implement from this file.
