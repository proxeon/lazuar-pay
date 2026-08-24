# S14 — checkouts.provider

**Track:** Schema · **Depends:** S13  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-CHK-005  
**Goal:** Remember which rail started the session. Billplz strips body metadata.

---

## S14.1 Column

- [ ] Add nullable `Provider` on `CheckoutRow`
- [ ] Set on **start** (`POST /v1/pay/{token}/start`), not on `POST /v1/checkouts`
- [ ] Merchant may switch `active_provider` before the buyer pays; create stays rail-agnostic
- [ ] Webhook may check `checkout.Provider` against path `{provider}` (H13 / P21)

## S14.2 Must not

- [ ] Do not default `Provider` to `stripe` on create
- [ ] Do not use Hub `tenant_id` metadata as the only join for Billplz

## S14.3 Exit

- [ ] Column on the row type
- [ ] Unblocked for S15, P18
