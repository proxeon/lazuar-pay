# Q10 — pay-spec PUT/GET gateway

**Track:** Q · **Depends:** P11, P14  
**Analysis:** [00](../00-what-must-be-done.md) §7  
**IDs:** —  
**Goal:** Spec catches up to doors that exist. Not Hub `api-spec`.

---

## Q10.1

- [x] `packages/pay-spec/main.tsp` models for gateway PUT/GET (provider, last4, capability, configured, public_merchant_id, environment, webhook_configured)
- [x] PUT body: secret, webhook_secret, optional public_merchant_id, environment
- [x] Do not publish ciphertext in GET model
- [x] `task pay:spec` regenerates OpenAPI

## Q10.2 Must not

- [x] Do not add pay-spec to Hub `task gen` (Q16)

## Q10.3 Exit

- [x] Spec lists the gateway ops
