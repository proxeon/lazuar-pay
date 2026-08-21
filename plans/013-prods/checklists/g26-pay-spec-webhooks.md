# G26 — TypeSpec webhook op (`packages/pay-spec` only)

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §5.1, [012/04](../../012-one-to-pay/04-pay-spec-contract.md)  
**IDs:** NP-API-002  
**Goal:** Spec matches the host. Not Hub `api-spec`.

---

## G26.1 Add

- [x] `POST /v1/webhooks/{provider}/{orgId}` on namespace `LazuarPay`
- [x] Anonymous POST — document: **no** Bearer; PSP signature is auth
- [x] `{provider}` allow-list is the G10 rail (do not publish five Hub names)
- [x] Server remains `http://localhost:8081`

## G26.2 Must not add

- [x] No Hub `/api/v1/webhooks/payments/…`
- [x] No `/one/*`, no Plane A One HMAC, no Plane C merchant outbound
- [x] No `packages/api-spec` import. No `task gen` / NSwag / honesty-allowlist

## G26.3 Compile

- [x] `task pay:spec` succeeds
- [x] OpenAPI shows the webhook op and still `/v1/health`
- [x] Dist stays gitignored

## G26.4 Exit

- [x] Spec path matches G18
- [x] `NP-API-002` may move if the host + spec agree
- [x] Unblocked for F10 / B99 (rails door exists)
