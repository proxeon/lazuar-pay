# Phase 16 — Optional extract / merge (product-triggered only)

**Do not start** without Phase 00 trigger and a short design note.  
**Evidence:** `../04-module-boundaries-modularization.md`

---

## 16.0 Gate (must all be true)

- [ ] Horizon 1 dual-path work done or not blocked by this extract
- [ ] Written design note: why extract/merge, failure domain, migration of events
- [ ] Product owner agrees

---

## 16.A Credits / Wallet extract from Billing

**Trigger:** credit monetization fights ledger change cadence.

- [ ] Define aggregate/table ownership (credit balance, holds, deductions)
- [ ] Define Contracts events other modules consume
- [ ] Move DbContext schema or keep schema with module boundary carefully
- [ ] Migrate handlers that deduct/hold credits
- [ ] Architecture tests for new module
- [ ] TypeSpec surface for credits admin if any
- [ ] Dual-write/read cutover plan if needed

## 16.B Webhooks / Developer extract from One

**Trigger:** multi-endpoint delivery product dominates One changes.

- [ ] Move outbox, dispatcher, signing, endpoint CRUD
- [ ] One retains auth/workspace only
- [ ] Events/commands for “deliver this payload”
- [ ] TypeSpec routes move under developer/webhooks package
- [ ] Migration of tables or schema rename plan

## 16.C Messaging → Communications merge

**Trigger:** multi-channel (WhatsApp) implementation starts.

- [ ] Move Messaging domain/infra into Communications
- [ ] Single channel adapter folder
- [ ] Delete Messaging projects from solution after move
- [ ] Update Program.cs / MediatR assemblies
- [ ] Update TypeSpec messaging models ownership
- [ ] Architecture tests update

## 16.D Rejected for now (document only)

- [ ] Confirm still rejected: Catalog module, Identity module, microservices split
- [ ] Prefer internal namespaces: `Commerce/Dunning`, `Billing/Wallet` folders before projects

## 16.E Exit criteria (if executed)

- [ ] New/merged boundary compiles with Contracts-only references
- [ ] No cross-schema SQL regressions introduced
- [ ] Deploy path unchanged (still modular monolith host)
