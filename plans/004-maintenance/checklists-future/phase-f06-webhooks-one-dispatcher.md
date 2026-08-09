# F06 — Route LHDN lifecycle webhooks through One dispatcher

**Goal:** End-state A — durable One delivery for LHDN customer webhooks.  
**Depends on:** F05 locked  
**Reject:** Building a second Lhdn outbox stack (decision B)

---

## F06.1 Design in code comments / short design note

- [ ] Event → outbox body mapping table implemented as code + tests
- [ ] Signing per F05 (dual-verify if needed)

## F06.2 Enqueue path

- [ ] On LHDN validated/invalid (etc.), enqueue One `WebhookDeliveryOutbox` (or existing delivery command)
- [ ] Correct `OrganizationId` / endpoint selection per F05
- [ ] Correlation ids preserved
- [ ] Tests: event → outbox row(s)

## F06.3 Disable fire-and-forget

- [ ] Stop calling `WebhookSenderService` for migrated events
- [ ] Delete or gut unused service paths
- [ ] Retire or re-tag Lhdn failure metrics once path is One

## F06.4 Docs

- [ ] Lhdn README: remove freeze section; document One path
- [ ] One README: LHDN events listed
- [ ] Integrator / hub docs + changelog if breaking

## F06.5 Exit

- [ ] No customer LHDN webhook relies on fire-and-forget
- [ ] Staging + (when ready) prod verified
- [ ] FW-2 marked done in FUTURE-WORK.md
