# W4-LP-044 — Finish Razorpay / Curlec adapter (e-mandate rail)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 4 `LP-044`. Tracker: *Razorpay / Curlec adapter* — Lazuar **P**.  
**Not this ID:** Homemade FPX e-mandate (`LP-032` consumes this or Xendit). India UPI product. Razorpay Route / marketplace. New fifth processor besides finishing this one.

**Invariant:** Curlec **is** Razorpay Malaysia. One adapter (`RAZORPAY`). Finish the broken bits so a MY e-mandate / card token can off-session without dummy `billing@lazuar.com`. Do not market “Curlec” as a second gateway type unless their API host/keys truly differ — then an alias `CURLEC` → same class.

---

## 0. Scope lock

In scope:

- Real customer email/phone on recurring create  
- Map `payment.failed` (and mandate-cancelled if documented)  
- Capability honesty: off-session only after tokens actually charge  
- Optional: Curlec e-mandate registration (not `method=card` only)  
- Currency: do not default missing webhook currency to `MYR` for INR accounts

Out of scope:

- Rebuilding PayNet  
- UPI checkout as a Hub product  
- Customer portal (Razorpay has none — keep throw)

---

## 1. Verdict

Adapter exists: payment links + registration links (`subscription_registration.method = card`). Off-session uses **hardcoded** email/contact. Webhook **only** `payment.captured`; other events verify-and-drop. `PaymentGatewayCapabilities.SupportsOffSession` is **false** for Razorpay (reminder-only). Refunds API yes.

**P** is correct: compiled, not demoable as MY auto-debit.

---

## 2. Current files

| Path | Role |
|------|------|
| `RazorpayGatewayAdapter.cs` | Checkout + captured-only webhook + dummy recurring |
| `PaymentGatewayCapabilities` | Not off-session |
| Ops label | “Razorpay (Global)” |
| `RazorpayGatewayAdapterTests` | Signature / event id |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Dummy contact on `CreateRecurringPayment` |
| G2 | Failures not `PAYMENT_FAILED` |
| G3 | Registration link is **card**, not FPX e-mandate |
| G4 | Off-session capability off — engine will mint pay-links even if tokens exist |
| G5 | Currency fallback `MYR` |

---

## 4. Recommended model

1. Pass CRM email/phone through metadata (already stamped on checkout) into recurring create.  
2. Map `payment.failed` / `invoice.expired` (confirm names) → `PAYMENT_FAILED`.  
3. E-mandate: Curlec docs at impl time — typically a mandate / token with `method=emandate` or FPX recurring. Add a **checkout path** when `setupFutureUsage` and product metadata `collection=emandate`, else keep card registration.  
4. Flip `SupportsOffSession("RAZORPAY")` **only** after a sandbox recurring payment succeeds with a real token. Until then stay reminder-only.  
5. Label ops: “Razorpay / Curlec (MY e-mandate + cards).”

LP-032 is the **product** flag on the subscription; this ticket is the **pipe**.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `RazorpayGatewayAdapter` | Contacts; failed events; optional emandate registration; no MYR default if currency present |
| Capabilities | Conditional off-session after soak — or keep false and document |
| Ops copy | Curlec alias |
| Tests | Failed event; missing currency ≠ forced MYR when payload has INR |

Must not: second adapter class without an API fork; UPI marketing.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Recurring notes include customer email from metadata | Not `billing@lazuar.com` |
| `payment.failed` | `PAYMENT_FAILED` |
| `payment.captured` | Unchanged |
| Event without currency | `Verified` fail or explicit currency required — do not invent MYR |

---

## 7. Acceptance

1. Off-session uses the buyer’s email/phone.  
2. Failed recurring is visible to Commerce (`PAST_DUE`).  
3. Ops does not promise auto-debit until capability is true.  
4. E-mandate (if shipped) is Curlec’s mandate, not a Hub ACH clone.

Tracker **P → W** when 1–3 land; **Y** never (we wrap).

---

## 8. Order

1. Dummy contact + failed webhook (unblocks honesty)  
2. Currency  
3. E-mandate registration if LP-032 chooses Curlec over Xendit  
4. Capability flip after soak  

Do **not** implement from this file.
