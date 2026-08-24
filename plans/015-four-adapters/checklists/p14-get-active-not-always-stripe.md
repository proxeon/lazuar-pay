# P14 — GET describes the active rail, not always stripe

**Track:** Provider door · **Depends:** P13, S18  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-GW-009  
**Goal:** Live GET `FindAsync([orgId, StripeHosted.Provider])` is wrong once CHIP exists.

---

## P14.1 Live today

- [ ] `GatewayEndpoints.Get` always loads stripe

## P14.2 Change

- [ ] Load `org_settings.active_provider`
- [ ] If null → `{ configured: false }` (no fake stripe row)
- [ ] Else load `(orgId, active_provider)` credentials
- [ ] Return `provider`, `last4`, `configured: true`, `capability: "hosted_link"`, `public_merchant_id` if any, `environment` if billplz, `webhook_configured`

## P14.3 Test

- [ ] No credentials → configured false, no last4
- [ ] After stripe PUT → provider stripe
- [ ] Member can GET; secret not in body (S18)

## P14.4 Exit

- [ ] GET is not hard-coded to stripe
- [ ] Unblocked for P15, U21
