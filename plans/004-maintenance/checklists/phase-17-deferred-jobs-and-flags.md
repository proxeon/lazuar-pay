# Phase 17 — Deferred jobs, probes, freeze list

**Goal:** Explicit ship/park/delete for residual surfaces.  
**Can run early after Phase 00** (small PRs).  
**Evidence:** `../01-removable-dead-code.md` §2, roadmap residual list  
**Status:** **Done** 2026-08-09 — see `../phase-17-analysis.md`, `../phase-17-done.md`

---

## 17.1 RevenueRecognitionJob

- [x] Re-read Phase 00.3 decision
- [x] If delete: N/A (parked)
- [x] If park:
  - [x] README/ADR note: unregistered by design (`Billing/README.md` §6 + class XML remarks + DI comment)
  - [x] Ensure no UI claims it runs (no FE/metrics claim found)
- [x] If implement: N/A — product epic only

## 17.2 Scope-probe / leftover Phase-1 endpoints

- [x] Locate `_scope-probe` or similar leftover routes
- [x] Confirm real M2M checkout covers the need (`Payments` IntegrationEndpoints)
- [x] **Delete** probe (tests did not need the route; policy tests use `AuthorizeAsync` only)
- [x] Remove from TypeSpec if present — was never in TypeSpec

## 17.3 Stale duplicate integration events

- [x] Grep for obsolete event types listed in residual checklists
- [x] Confirm no handlers subscribe (Commerce twin unused)
- [x] Delete dead event class (`Commerce.ExecuteOffSessionChargeIntegrationEvent`)
- [x] Do not delete events still in outbox history without care — Payments event **kept**

## 17.4 WhatsApp / console-only channels

- [x] Align with Phase 00.4
- [x] Freeze: document console-only / not production WhatsApp (`Messaging/README.md`)
- [x] If ship: N/A — product Phase D / reopen 00.4

## 17.5 Host should not reference Application projects incorrectly

- [x] Audit host csproj references (Contracts vs Application)
- [x] Fix: removed direct Commerce + Communications Application ProjectReferences
- [x] Architecture tests encode the rule (`Host_Csproj_Must_Not_Directly_Reference_Module_Application_Projects`)

## 17.6 Exit criteria

- [x] Every deferred surface is ship, park (documented), or delete
- [x] No probe endpoints without owners
