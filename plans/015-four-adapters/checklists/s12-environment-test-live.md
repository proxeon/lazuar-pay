# S12 — gateway_credentials.environment

**Track:** Schema · **Depends:** S10  
**Analysis:** [00](../00-what-must-be-done.md) §3.2 / §5.2  
**IDs:** —  
**Goal:** Billplz sandbox vs www host. Do not infer live from hostname.

---

## S12.1 Column

- [ ] Add `Environment` string on `GatewayCredentialRow`, default `test`
- [ ] Allowed values: `test` | `live` (normalize lowercase)
- [ ] Billplz: `test` → `https://www.billplz-sandbox.com/api/v3/`
- [ ] Billplz: `live` → `https://www.billplz.com/api/v3/`
- [ ] Other rails may ignore (CHIP test is a dashboard toggle; Stripe key prefix is enough)

## S12.2 Must not

- [ ] Do not infer live from `Pay:PublicBaseUrl` containing `lazuar.com` (Hub `BillplzPublicBase` warning: `pay-local.lazuar.com` must never go live)
- [ ] Do not send sandbox keys to www or live keys to sandbox

## S12.3 Exit

- [ ] Column on the row type
- [ ] Unblocked for B12
