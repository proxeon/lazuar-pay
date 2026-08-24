# S14 — checkouts.provider

**Track:** Schema · **Depends:** S13  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** NP-CHK-005  
**Goal:** Remember which rail started the session. Billplz strips body metadata.

---

## S14.1 Column

- [x] Add nullable `Provider` on `CheckoutRow`
- [x] Set on **start** (`POST /v1/pay/{token}/start`), not on `POST /v1/checkouts`
- [x] Merchant may switch `active_provider` before the buyer pays; create stays rail-agnostic
- [x] Webhook may check `checkout.Provider` against path `{provider}` (H13 / P21)

## S14.2 Must not

- [x] Do not default `Provider` to `stripe` on create
- [x] Do not use Hub `tenant_id` metadata as the only join for Billplz

## S14.3 Exit

- [x] Column on the row type
- [x] Unblocked for S15, P18
