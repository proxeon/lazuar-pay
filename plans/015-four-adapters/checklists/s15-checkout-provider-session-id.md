# S15 — checkouts.provider_session_id

**Track:** Schema · **Depends:** S14  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Persist the processor session id next to the redirect URL.

---

## S15.1 Column

- [x] Add nullable `ProviderSessionId` on `CheckoutRow`
- [x] Set when `CreateHostedUrl` returns
- [x] Values: Stripe `cs_…`, CHIP purchase id, Billplz bill id, Xendit invoice id, Razorpay `plink_…`
- [x] Keep existing `PspRedirectUrl`

## S15.2 Why

- [x] Billplz merge-by-bill-id if `checkout_id` query is lost
- [x] Support / replay without scraping metadata

## S15.3 Exit

- [x] Column on the row type
- [x] Unblocked for P18
