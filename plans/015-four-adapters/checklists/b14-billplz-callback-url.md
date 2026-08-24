# B14 — callback_url is Pay Plane B + checkout_id query

**Track:** Billplz · **Depends:** B13  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-API-002  
**Goal:** Billplz **strips body metadata**. Query is the join.

---

## B14.1

- [ ] `callback_url` = `{Pay:PublicBaseUrl}/v1/webhooks/billplz/{orgId}?checkout_id={checkout.Id}`
- [ ] `Pay:PublicBaseUrl` absolute, no trailing slash issues
- [ ] Not Hub `/api/v1/webhooks/payments/billplz/{tenantId}`
- [ ] `redirect_url` is buyer success/cancel on `:5179`, not callback

## B14.2 Must not

- [ ] Do not put secrets in the query string
- [ ] Do not omit `checkout_id` (B16)

## B14.3 Exit

- [ ] Mocked create body contains the query
- [ ] Unblocked for B15
