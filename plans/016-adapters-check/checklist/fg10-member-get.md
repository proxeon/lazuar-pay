# fg10 — Member GET gateway metadata 200

**Track:** Fill gateway · **Depends:** S18  
**Analysis:** 09 method 57; H18  
**Goal:** `GatewayTests.Member_can_get_gateway_metadata`

---

- [ ] Owner PUT stripe. Switch One responder to `Role("member")`
- [ ] GET `/v1/orgs/t1/gateway` with Bearer
- [ ] 200, `configured true`, `provider stripe`, `capability hosted_link`
- [ ] JSON does not contain `sk_test` or `whsec`
- [ ] Exit: green
