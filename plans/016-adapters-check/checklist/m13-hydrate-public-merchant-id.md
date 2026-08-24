# M13 — Hydrate Brand ID / Collection ID from GET

**Track:** Merchant · **Depends:** M11  
**Analysis:** GET returns `public_merchant_id`; SPA does not set the field  
**IDs:** —  
**Goal:** Writer can rotate secret without blanking collection/brand.

---

## M13.1

- [ ] On GET, `setPublicMerchantId(body.public_merchant_id ?? '')`
- [ ] Public ids are not secrets; members already see last4 — showing collection/brand to writers is OK
- [ ] Members: do not expose a paste box (writer gate stays)

## M13.2 Must not

- [ ] Do not PUT public_merchant_id for stripe/xendit/razorpay

## M13.3 Exit

- [ ] Field hydrates
