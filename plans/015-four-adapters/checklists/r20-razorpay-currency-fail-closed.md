# R20 — Razorpay missing currency: do not default MYR

**Track:** Razorpay · **Depends:** R17  
**Analysis:** Hub `TryReadCurrency`  
**IDs:** —  
**Goal:** Razorpay accounts may be INR. Do not invent MYR.

---

## R20.1

- [x] Missing currency on entity → do not fulfill
- [x] Must match checkout.Currency
- [x] Creating a MYR checkout against an INR Razorpay account will fail at **start** (API error → 503) — do not lie in the webhook

## R20.2 Exit

- [x] Fixture without currency does not pay
