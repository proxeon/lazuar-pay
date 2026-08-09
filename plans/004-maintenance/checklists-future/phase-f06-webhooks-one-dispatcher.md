# F06 — Route LHDN lifecycle webhooks through One dispatcher

**Goal:** End-state A — durable One delivery for LHDN customer webhooks.  
**Depends on:** F05 locked  
**Reject:** Building a second Lhdn outbox stack (decision B)  
**Status:** Done via R40–R43 (`plans/005-remaining/`)

---

## F06.1 Design in code comments / short design note

- [x] Event → outbox body mapping table implemented as code + tests
- [x] Signing per F05 (dual-verify if needed) — One Standard Webhooks; dual-sign skipped

## F06.2 Enqueue path

- [x] On LHDN validated/invalid (etc.), enqueue One `WebhookDeliveryOutbox` (or existing delivery command)
- [x] Correct `OrganizationId` / endpoint selection per F05
- [x] Correlation ids preserved
- [x] Tests: event → outbox row(s)

## F06.3 Disable fire-and-forget

- [x] Stop calling `WebhookSenderService` for migrated events
- [x] Delete or gut unused service paths
- [x] Retire or re-tag Lhdn failure metrics once path is One

## F06.4 Docs

- [x] Lhdn README: remove freeze section; document One path
- [x] One README: LHDN events listed
- [x] Integrator / hub docs + changelog if breaking — R40/R42 notes + module READMEs

## F06.5 Exit

- [x] No customer LHDN webhook relies on fire-and-forget
- [ ] Staging + (when ready) prod verified
- [x] FW-2 marked done in FUTURE-WORK.md
