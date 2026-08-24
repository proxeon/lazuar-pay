# S15 — checkouts.provider_session_id

**Track:** Schema · **Depends:** S14  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Persist the processor session id next to the redirect URL.

---

## S15.1 Column

- [ ] Add nullable `ProviderSessionId` on `CheckoutRow`
- [ ] Set when `CreateHostedUrl` returns
- [ ] Values: Stripe `cs_…`, CHIP purchase id, Billplz bill id, Xendit invoice id, Razorpay `plink_…`
- [ ] Keep existing `PspRedirectUrl`

## S15.2 Why

- [ ] Billplz merge-by-bill-id if `checkout_id` query is lost
- [ ] Support / replay without scraping metadata

## S15.3 Exit

- [ ] Column on the row type
- [ ] Unblocked for P18
