# fg11 — PUT unknown provider is 400

**Track:** Fill gateway · **Depends:** A00  
**Analysis:** 09 method 58; P22 (webhook paypal exists; PUT missing)  
**Goal:** `GatewayTests.Put_unknown_provider_is_400`

---

- [ ] Owner PUT `{provider:"paypal", secret:"x", webhook_secret:"y"}`
- [ ] 400 unknown provider
- [ ] Exit: green
