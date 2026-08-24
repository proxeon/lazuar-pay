# X12 — POST /v2/invoices

**Track:** Xendit · **Depends:** X11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** —  
**Goal:** Basic `{secret}:` JSON. Return `invoice_url` + `id`.

---

## X12.1

- [x] `POST https://api.xendit.co/v2/invoices`
- [x] `Authorization: Basic base64(secret + ":")`
- [x] Amount in **major units**: Hub `BuildInvoicePayload` does `ToMinorUnitsRounded(amount, qty) / 100m` then sends `"amount"` (not integer cents)
- [x] Currency required; throw if missing (do not default MYR)
- [x] `payer_email` required (X22)
- [x] `success_redirect_url` / `failure_redirect_url` like C16 (verifying, not paid)
- [x] metadata `checkout_id` + `org_id` (do not require Hub `external_id` `lazuar_` prefix; checkout id is enough)
- [x] Missing `invoice_url` → throw → 503
- [x] Do **not** copy Hub `payment_methods` channel list (X20 — wallets stay on Xendit’s page)

## X12.2 Amount honesty

- [x] Round AwayFromZero via cents then `/ 100m` like Hub — do not send raw float without that policy
- [x] Do not apply SST

## X12.3 Exit

- [x] Method + mock start test
- [x] Unblocked for X13
