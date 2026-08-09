# F15 — Module extract / merge gate (FW-5)

**Goal:** Only run extract/merge when product reopens Phase 16.  
**Default:** **SKIP** this phase

---

## F15.0 Gate (all required)

- [ ] Written product trigger (credits / webhooks product / multi-channel)
- [ ] `decisions.md` reopened and updated
- [ ] Design note (events, schemas, dual-write)
- [ ] Product sign-off

If any unchecked → mark phase **N/A** and stop.

---

## F15.A Credits/Wallet extract (only if triggered)

- [ ] Contracts, DbContext ownership, handlers, TypeSpec, arch tests, cutover plan

## F15.B Webhooks/Developer extract (only if triggered)

- [ ] Move outbox/dispatcher/signing/endpoints; One keeps auth/workspace

## F15.C Messaging → Communications merge (only if triggered)

- [ ] Move projects; update Program/MediatR; TypeSpec ownership; delete Messaging projects

## F15.D Exit

- [ ] Gate failed (N/A) **or** extract merged with Contracts-only boundaries
