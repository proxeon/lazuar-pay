# C14 — CHIP metadata checkout_id and org_id

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** —  
**Goal:** Webhook can join without Hub `tenant_id` folklore.

---

## C14.1

- [x] `purchase.metadata.checkout_id` = `checkout.Id`
- [x] `purchase.metadata.org_id` = `checkout.OrgId`
- [x] Do not stamp Hub `platform_tenant_id` / `hub_payment_environment`
- [x] Webhook parse reads those keys (C19)

## C14.2 Must not

- [x] Do not rely only on CHIP’s purchase id without storing `ProviderSessionId` (P18)

## C14.3 Exit

- [x] Mocked POST body includes both keys
