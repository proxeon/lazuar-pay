# G26 — TypeSpec webhook op (`packages/pay-spec` only)

**Track:** Rails · **Depends:** G18  
**Analysis:** [06](../06-money-rails.md) §5.1, [012/04](../../012-one-to-pay/04-pay-spec-contract.md)  
**IDs:** NP-API-002  
**Goal:** Spec matches the host. Not Hub `api-spec`.

---

## G26.1 Add

- [ ] `POST /v1/webhooks/{provider}/{orgId}` on namespace `LazuarPay`
- [ ] Anonymous POST — document: **no** Bearer; PSP signature is auth
- [ ] `{provider}` allow-list is the G10 rail (do not publish five Hub names)
- [ ] Server remains `http://localhost:8081`

## G26.2 Must not add

- [ ] No Hub `/api/v1/webhooks/payments/…`
- [ ] No `/one/*`, no Plane A One HMAC, no Plane C merchant outbound
- [ ] No `packages/api-spec` import. No `task gen` / NSwag / honesty-allowlist

## G26.3 Compile

- [ ] `task pay:spec` succeeds
- [ ] OpenAPI shows the webhook op and still `/v1/health`
- [ ] Dist stays gitignored

## G26.4 Exit

- [ ] Spec path matches G18
- [ ] `NP-API-002` may move if the host + spec agree
- [ ] Unblocked for F10 / B99 (rails door exists)
