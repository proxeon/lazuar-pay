# fr19 — Razorpay PUT requires key_id:key_secret

**Track:** Fill Razorpay · **Depends:** A00  
**Analysis:** 09 method 53; R12  
**Goal:** `GatewayTests.Razorpay_put_requires_key_id_colon_secret`

---

- [ ] PUT `{provider:razorpay, secret:"nocolon", webhook_secret:"wh"}`
- [ ] 400 `"secret must be key_id:key_secret"`
- [ ] Exit: green
