# S22 — Diagrams: provision + create-checkout

**Track:** Docs diagrams · **Analysis:** `../01` SEQ-PROVISION*, SEQ-CHECKOUT, STM-CHECKOUT  
**Depends on:** S20, S21 recommended first for E2E SSoT  
**Goal:** Page-local deep dives without forking E2E story.

---

## S22.1 Provision (`integrations/provision.md`)

- [ ] Sequence: first provision (secret auth, external_product+org_id, once secrets)
- [ ] Sequence or note: idempotent re-call (`created=false`, plain_key null)
- [ ] Bootstrap scopes called out: payments checkouts write/read + webhooks.endpoints:manage
- [ ] Prose: secrets returned once
- [ ] Link to payment-flow for full E2E

## S22.2 Create checkout (`integrations/create-checkout.md`)

- [ ] Sequence: POST checkouts → checkout_url → guest redirect → (optional GET status)
- [ ] State diagram: open → completed/failed (optional expired)
- [ ] Flowchart or note: success_url / poll vs webhook unlock
- [ ] Note PAYMENTS_NOT_CONFIGURED when BYOK missing
- [ ] Amount units: major (e.g. 25.00 MYR), not cents
- [ ] Link architecture create-payment matrix

## S22.3 Consistency

- [ ] Auth: Bearer sk_test_/sk_live_ only for machine path
- [ ] No Commerce paths mixed in

## S22.4 Exit

- [ ] Docs build green
- [ ] Each diagram has prose summary
