# Phase 16 — Optional extract / merge (product-triggered only)

**Do not start** without Phase 00 trigger and a short design note.  
**Evidence:** `../04-module-boundaries-modularization.md`  
**Status (2026-08-09):** **GATE NOT MET** — extract deferred. See `../phase-16-done.md`, `../phase-16-analysis.md` (N/A extract), `../decisions.md` (00.4 / 00.5 / 00.6).

---

## 16.0 Gate (must all be true)

**Result: GATE NOT MET — no extract/merge executed.**

- [x] Gate evaluated: Horizon 1 dual-path work done or not blocked by this extract — **N/A for extract** (no extract started; dual-path orthogonal)
- [x] Gate evaluated: Written design note: why extract/merge, failure domain, migration of events — **NOT MET** (no product trigger → no design)
- [x] Gate evaluated: Product owner agrees — **NOT MET** (`decisions.md`: no WhatsApp 6mo; credits in Billing; no new modules)

---

## 16.A Credits / Wallet extract from Billing

**Trigger:** credit monetization fights ledger change cadence.  
**Trigger status:** **Not met** — 00.5 credits stay in Billing through ≥ 2027-02-09.

- [ ] Define aggregate/table ownership (credit balance, holds, deductions)
- [ ] Define Contracts events other modules consume
- [ ] Move DbContext schema or keep schema with module boundary carefully
- [ ] Migrate handlers that deduct/hold credits
- [ ] Architecture tests for new module
- [ ] TypeSpec surface for credits admin if any
- [ ] Dual-write/read cutover plan if needed

## 16.B Webhooks / Developer extract from One

**Trigger:** multi-endpoint delivery product dominates One changes.  
**Trigger status:** **Not met** — 00.2 webhooks stay in One for this track.

- [ ] Move outbox, dispatcher, signing, endpoint CRUD
- [ ] One retains auth/workspace only
- [ ] Events/commands for “deliver this payload”
- [ ] TypeSpec routes move under developer/webhooks package
- [ ] Migration of tables or schema rename plan

## 16.C Messaging → Communications merge

**Trigger:** multi-channel (WhatsApp) implementation starts.  
**Trigger status:** **Not met** — 00.4 no WhatsApp / multi-channel in next 6 months.

- [ ] Move Messaging domain/infra into Communications
- [ ] Single channel adapter folder
- [ ] Delete Messaging projects from solution after move
- [ ] Update Program.cs / MediatR assemblies
- [ ] Update TypeSpec messaging models ownership
- [ ] Architecture tests update

## 16.D Rejected for now (document only)

- [x] Confirm still rejected: Catalog module, Identity module, microservices split — **documented** in `../phase-16-done.md` (+ 00.6 non-goals)
- [x] Prefer internal namespaces: `Commerce/Dunning`, `Billing/Wallet` folders before projects — **documented** (allowed without reopen; no new `Modules/*`)

## 16.E Exit criteria (if executed)

**N/A** — extract not executed.

- [ ] New/merged boundary compiles with Contracts-only references
- [ ] No cross-schema SQL regressions introduced
- [ ] Deploy path unchanged (still modular monolith host)

---

*Phase closed without code. Reopen only when product reopens 00.x + design note + 16.0 all true.*
