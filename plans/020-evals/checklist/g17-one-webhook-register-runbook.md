# G17 — Ops: register Pay URL on One

**Track:** G · **Depends:** K00  
**Analysis:** [`../04-inbound-webhooks.md`](../04-inbound-webhooks.md) — Pay does not POST One `/tenants/{id}/webhooks`  
**Goal:** Humans know how pause gets to 8081.

**Why:** Pay never POSTs One `/tenants/{id}/webhooks`. One SSRF blocks loopback. 029 stored per-org `whsec_` but ops still pastes both sides.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | PUT/GET `/v1/orgs/{orgId}/one-webhook`; POST `/v1/one/webhooks` |
| `apps/lazuar-pay/README.md` | “Pay does not POST One `/tenants/{id}/webhooks`” |
| `apps/lazuar-pay/.env.example` | Process fallback comment |
| W14 | **Different** `whsec_` (Pay→app) — runbook must not mix |

**Current (`6d730d15`):** Code ready; runbook is one README sentence.

---

## G17.1

- [ ] Pay README: public **https** URL `POST {origin}/v1/one/webhooks`
- [ ] One UI or curl: create endpoint, copy `whsec_` once
- [ ] Pay writer `PUT /v1/orgs/{orgId}/one-webhook`
- [ ] One SSRF blocks loopback — laptop needs a tunnel
- [ ] Process `Pay__OneWebhookSecret` is one-shop fallback, not multi-shop design

## G17.2 Must not

- [ ] Do not add a Pay Zitadel PAT to register automatically
- [ ] Do not mix this `whsec_` with Plane C (W14) or Stripe vault

## G17.3 Exit

- [ ] Unblocked for G18
