# P24 — Missing org credentials is 400

**Track:** Provider door · **Depends:** P21  
**Analysis:** [00](../00-what-must-be-done.md) §5  
**IDs:** —  
**Goal:** Do not verify with the wrong org’s secret. Do not 200.

---

## P24.1 Webhook

- [ ] No `gateway_credentials` row for `(orgId, provider)` → 400 `"rail not configured"`
- [ ] Stripe today already checks stripe row exists
- [ ] Check **that provider**, not “any stripe row”

## P24.2 Start

- [ ] Keep 503 `"rail not configured"` / `"Stripe rejected the org key"` for public start (buyer-facing; already)
- [ ] Webhook stays 400 (PSP; not a Pay outage)

## P24.3 Test

- [ ] POST `/v1/webhooks/stripe/t1` with no PUT keys → 400

## P24.4 Exit

- [ ] Consistent per provider
