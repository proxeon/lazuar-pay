# W14 — Register Plane C endpoint (writer)

**Track:** W · **Depends:** W10, W13  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §6 / §9.2.4  
**Goal:** Owner/admin (JWT) or bound key (after M14) can PUT a URL. Secret shown **once**.

**Why:** Mirror One and Pay vault: writer PUT, secret once, GET metadata. Third `whsec_` family — README must say **Pay signs; you verify**, not Stripe, not One inbound.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` | PUT vault pattern: writer, SecretBox, audit, no echo |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | PUT `/v1/orgs/{orgId}/one-webhook` — **different** door |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | `Map*` — add Map here or a new static class |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs` | Writer vs member, no echo |
| `packages/pay-spec/main.tsp` | Grow in W29 |

**Current (`6d730d15`):** No `/v1/orgs/{orgId}/webhooks` Map*.

---

## W14.1 Door

- [ ] `PUT /v1/orgs/{orgId}/webhooks` (singular; replace active) **or** `POST` create + 409 if exists — **pick PUT replace**
- [ ] Body: `{ "url": "https://app.example/pay-hook" }`
- [ ] Writer gate (`RequireWriterAsync`)
- [ ] SSRF validate URL
- [ ] Mint `whsec_` + random; wrap with SecretBox
- [ ] 200/201 JSON: `org_id`, `url`, `webhook_secret` (once), `webhook_configured: true`, `secret_prefix`
- [ ] Audit `webhook_endpoint.upsert`
- [ ] Empty URL → 400

## W14.2 Labels (honesty)

- [ ] README: this `whsec_` is **Pay signing for your app**, not Stripe, not One inbound

## W14.3 Must not

- [ ] Do not POST One `/tenants/{id}/webhooks`
- [ ] Do not share route with `/v1/one/webhooks`

## W14.4 Tests

- [ ] Writer PUT 200 and body contains `whsec_`
- [ ] GET later (W15) does not contain that secret

## W14.5 Exit

- [ ] Unblocked for W15
