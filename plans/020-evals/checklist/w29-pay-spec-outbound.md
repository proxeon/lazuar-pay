# W29 — TypeSpec Plane C doors

**Track:** W · **Depends:** W14, W15, W16 (host first)  
**Analysis:** honesty script  
**Goal:** Map* and OpenAPI stay aligned.

**Why:** Honesty script fails if Map* exists without tsp. Spec-first without Map* also fails. Host W14–W16 first.

**Related files**

| Path | Role today |
|------|------------|
| `packages/pay-spec/main.tsp` | Webhooks tag: Plane A + B only |
| `scripts/check-pay-openapi-honesty.mjs` | IMPL_ONLY `/health` `/ready` |
| `Taskfile.yml` | `pay:spec` |
| W14–W16 | Live doors |

**Current (`6d730d15`):** 22 spec ops. No org webhooks Map*.

---

## W29.1

- [x] `PUT /v1/orgs/{orgId}/webhooks` + GET + rotate POST
- [x] Models: `PutOrgWebhook` `{ url }`, `OrgWebhookView` `{ org_id, url?, webhook_configured, secret_prefix? }`, `OrgWebhookCreated` includes `webhook_secret`
- [x] Doc: Lazuar HMAC (One dialect). Not Stripe. Not Standard Webhooks
- [x] Do **not** spec `payment.failed`
- [x] `task pay:spec` + honesty exit 0
- [x] Unversioned `/ready` stays out of tsp

## W29.2 Exit

- [x] Unblocked for W30, E14
