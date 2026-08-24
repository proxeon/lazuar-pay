# W23 — Hermetic Standard Webhooks vector

**Track:** One HMAC · **Depends:** W11–W20  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) T2; 014 zero One HMAC tests  
**IDs:** —  
**Goal:** Suspend works in CI without One.

---

## W23.1 New class `OneWebhookTests`

- [ ] Factory sets `Pay:OneWebhookSecret` to a known test string
- [ ] Helper: `t={unix},v1={lowercase HMAC hex of $"{unix}.{body}"}`

## W23.2 Methods (split if the file grows; names locked)

- [ ] `Valid_tenant_suspended_sets_charges_paused` — 200, `ChargesPaused == true`
- [ ] `Valid_tenant_id_field_sets_charges_paused` — JSON `tenant_id` not `org_id`
- [ ] `Body_only_uppercase_hex_is_401` — W16
- [ ] `Missing_signature_is_401` — W15
- [ ] `Stale_timestamp_is_401` — W14 (`t` = now-1000)
- [ ] `Missing_secret_is_503` — W20 (secret `""`)
- [ ] `Replay_delivery_is_duplicate` — same JSON `id`

## W23.3 Must not

- [ ] Do not call live One
- [ ] Do not use Stripe `Sign()` helper for this header

## W23.4 Exit

- [ ] `task pay:test` includes these methods green
- [ ] Unblocked for W24
