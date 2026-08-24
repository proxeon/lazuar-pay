# S12 — gateway_credentials.environment

**Track:** Schema · **Depends:** S10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2 / §5.2  
**IDs:** —  
**Goal:** Billplz sandbox vs www host. Do not infer live from hostname.

---

## S12.1 Column

- [x] Add `Environment` string on `GatewayCredentialRow`, default `test`
- [x] Allowed values: `test` | `live` (normalize lowercase)
- [x] Billplz: `test` → `https://www.billplz-sandbox.com/api/v3/`
- [x] Billplz: `live` → `https://www.billplz.com/api/v3/`
- [x] Other rails may ignore (CHIP test is a dashboard toggle; Stripe key prefix is enough)

## S12.2 Must not

- [x] Do not infer live from `Pay:PublicBaseUrl` containing `lazuar.com` (Hub `BillplzPublicBase` warning: `pay-local.lazuar.com` must never go live)
- [x] Do not send sandbox keys to www or live keys to sandbox

## S12.3 Exit

- [x] Column on the row type
- [x] Unblocked for B12
