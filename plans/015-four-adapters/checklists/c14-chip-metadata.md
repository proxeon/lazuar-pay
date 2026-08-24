# C14 — CHIP metadata checkout_id and org_id

**Track:** CHIP · **Depends:** C12  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** —  
**Goal:** Webhook can join without Hub `tenant_id` folklore.

---

## C14.1

- [ ] `purchase.metadata.checkout_id` = `checkout.Id`
- [ ] `purchase.metadata.org_id` = `checkout.OrgId`
- [ ] Do not stamp Hub `platform_tenant_id` / `hub_payment_environment`
- [ ] Webhook parse reads those keys (C19)

## C14.2 Must not

- [ ] Do not rely only on CHIP’s purchase id without storing `ProviderSessionId` (P18)

## C14.3 Exit

- [ ] Mocked POST body includes both keys
