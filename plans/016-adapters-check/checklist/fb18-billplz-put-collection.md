# fb18 — Billplz PUT requires collection id

**Track:** Fill Billplz · **Depends:** A00  
**Analysis:** 09 method 27; B27  
**Goal:** `GatewayTests.Billplz_put_requires_collection_id`

---

- [ ] PUT `{provider:billplz, secret, webhook_secret, environment:test}` without `public_merchant_id`
- [ ] 400
- [ ] Exit: green
