# R13 — POST /v1/payment_links

**Track:** Razorpay · **Depends:** R12  
**Analysis:** [00](../00-what-must-be-done.md) §5.4; Hub `PaymentLink.Create`  
**IDs:** —  
**Goal:** Raw HTTP, not official SDK.

---

## R13.1

- [x] `POST https://api.razorpay.com/v1/payment_links`
- [x] Amount **minor units** AwayFromZero (Hub `ToMinorUnits`)
- [x] `notes.checkout_id`, `notes.org_id`
- [x] `callback_url` / success like C16 (GET callback_method if Hub sent it — read `BuildPaymentLinkRequest`)
- [x] Read `short_url` and `id` (`plink_`)
- [x] Missing short_url → throw → 503

## R13.2 Open Hub

- [x] Copy customer object shape from `BuildPaymentLinkRequest` (R24)
- [x] Discard `setupFutureUsage` (R15)

## R13.3 Exit

- [x] Mocked start test
