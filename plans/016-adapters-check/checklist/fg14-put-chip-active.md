# fg14 — PUT chip GET active is chip

**Track:** Fill gateway · **Depends:** S18  
**Analysis:** 09 method 61; P14  
**Goal:** `GatewayTests.Put_chip_get_active_is_chip_not_stripe`

---

- [ ] PUT chip brand+PEM. GET no query
- [ ] `provider == chip`, `capability == hosted_link`, `public_merchant_id` present, no PEM in JSON
- [ ] Exit: green
