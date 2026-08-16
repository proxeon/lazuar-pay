# W4-LP-035 — GrabPay (wrap only)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-035`. Tracker: *GrabPay* — Lazuar **N**.  
**Not this ID:** Grab PayLater / BNPL (`LP-039` refuse). Apple Pay (`LP-037` — Stripe `card`). Stripe `grabpay` as a **separate** PaymentMethod type.

**Invariant:** GrabPay is either (a) already on Billplz/CHIP hosted collections, or (b) Stripe Checkout `payment_method_types` including `grabpay` **in addition to** `card` only if we accept the **manual** PM list (LP-037 locked `card` for wallets). Do not mix “add GrabPay” with dynamic Stripe PMs — Stripe forbids mixing.

---

## 0. Scope lock

In scope:

- CHIP/Billplz/Xendit method flag + hop-1 copy (same as 033)  
- **Optional** Stripe: document that adding `grabpay` **replaces** dynamic methods and must include `card` to keep Apple/Google Pay

Out of scope:

- Grab merchant app  
- PayLater instalment ledger  
- Hop-1 Grab button / Elements

---

## 1. Verdict

LP-037 set Stripe sessions to **`card` only** so Apple/Google Pay can show. Adding `grabpay` is a **conscious** manual list: `["card","grabpay"]`. That may hide Dashboard-only methods (FPX on Stripe). Prefer CHIP/Xendit/Billplz for MY GrabPay; use Stripe only if the tenant’s gateway **is** Stripe and they opt in.

---

## 2. Current files

`StripeGatewayAdapter.ApplyCardWalletPaymentMethodTypes` → `card`. Billplz/CHIP: no filter.

---

## 3. Exact gaps

No `SupportsGrabPay`. Stripe list does not include `grabpay`. Hop 1 silent.

---

## 4. Recommended model

1. Default: wrap hosted processors (flag + copy).  
2. Stripe opt-in later: `PaymentMethodTypes = card + grabpay` behind a **product or gateway setting**, not global.  
3. Recurring: GrabPay wallet mandate is processor-specific — default reminder-only unless Stripe documents reusable GrabPay + off-session (usually **no**).

---

## 5. Minimal code changes

Shared capability + hop-1. Stripe change **only** with an explicit setting and a test that `card` remains present (LP-037 regression).

---

## 6. Tests

LP-037 still `card` when GrabPay opt-in is off. Opt-in includes both `card` and `grabpay`. CHIP/Billplz unit whitelist.

---

## 7. Acceptance

GrabPay is visible on hop 2 via a wrapped host. We never claim “Lazuar GrabPay.” Apple Pay still works on Stripe tenants who did not opt into a wider manual list.

Tracker **N → W**.

---

## 8. Order

Prefer after LP-045. Do not break LP-037.

Do **not** implement from this file.
