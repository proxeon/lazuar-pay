# M26 — Merchant runbook

**Track:** Merchant · **Depends:** M16, M25  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Ada can sign in on `:5178` with Hub off. Not a CI gate.

---

## M26.1 README topology

- [x] Hub **off** (`task dev` / Hub compose not owning 8080)
- [x] One **8080** + login **5175** + Zitadel **8085**
- [x] Pay **8081**; merchant **5178**
- [x] Ada `ada@acme.test` (password on `:5175`)
- [x] Fingerprint One (`GET /api/v1/` `name=lazuar-one-api`)
- [x] Expected whoami `tenants[]` (empty → create; else pick)

## M26.2 Not CI

- [x] Live Ada OIDC is a **runbook**, not a `task pay:test` / GitHub gate
- [x] Hermetic tests stay fake-One (no Zitadel required in CI)

## M26.3 Exit

- [x] README lists the ports, Ada, fingerprint, and whoami expectation
- [x] Unblocked for M27
