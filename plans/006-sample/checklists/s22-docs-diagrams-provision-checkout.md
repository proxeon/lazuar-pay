# S22 — Diagrams: provision + create-checkout

**Track:** Docs diagrams · **Analysis:** `../01` SEQ-PROVISION*, SEQ-CHECKOUT, STM-CHECKOUT  
**Depends on:** S20, S21 recommended first for E2E SSoT  
**Goal:** Page-local deep dives without forking E2E story.

---

## S22.1 Provision (`integrations/provision.md`)

- [x] Sequence: first provision (secret auth, external_product+org_id, once secrets)
- [x] Sequence or note: idempotent re-call (`created=false`, plain_key null)
- [x] Bootstrap scopes called out: payments checkouts write/read + webhooks.endpoints:manage
- [x] Prose: secrets returned once
- [x] Link to payment-flow for full E2E

## S22.2 Create checkout (`integrations/create-checkout.md`)

- [x] Sequence: POST checkouts → checkout_url → guest redirect → (optional GET status)
- [x] State diagram: open → completed/failed (optional expired)
- [x] Flowchart or note: success_url / poll vs webhook unlock
- [x] Note PAYMENTS_NOT_CONFIGURED when BYOK missing
- [x] Amount units: major (e.g. 25.00 MYR), not cents
- [x] Link architecture create-payment matrix (payment-flow / cashier; architecture page when exists)

## S22.3 Consistency

- [x] Auth: Bearer sk_test_/sk_live_ only for machine path
- [x] No Commerce paths mixed in

## S22.4 Exit

- [x] Docs build green
- [x] Each diagram has prose summary
