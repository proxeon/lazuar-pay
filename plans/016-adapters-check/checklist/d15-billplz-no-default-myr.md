# D15 — Billplz must not hardcode Currency MYR

**Track:** Units · **Depends:** A00  
**Analysis:** [`../06-billplz-crosscheck.md`](../06-billplz-crosscheck.md); 015 freeze “do not default MYR”; live `Currency = "MYR"`  
**IDs:** P1-2  
**Goal:** Missing currency fail-closed like CHIP/Xendit/Razorpay.

---

## D15.1 Live today

- [ ] `BillplzWebhook` sets `Currency = "MYR"` always
- [ ] A USD checkout would 400 currency mismatch (accidental). A MYR checkout passes even if Billplz omitted currency

## D15.2 Change

- [ ] Read currency from form if Billplz sends one (`currency` field) and `TryNormalizeCurrency`
- [ ] If omitted: **throw `PspVerifyException("missing currency")`** — do **not** invent MYR
- [ ] Billplz bills are MYR in practice; fail-closed still applies. Checkout currency remains whatever was minted

## D15.3 Must not

- [ ] Do not default MYR “because Billplz is Malaysian”
- [ ] Do not skip the handler currency compare (that is D16’s Stripe bug)

## D15.4 Exit

- [ ] Paid fixture includes `currency=MYR` (or whatever field you confirm) **or** a documented form key
- [ ] Missing currency 400, zero documents (`RailTests.Billplz_missing_currency_does_not_pay` if the field exists; if Billplz never sends currency, A00 note: still refuse rather than hardcode — then tests must send it on happy path only)
