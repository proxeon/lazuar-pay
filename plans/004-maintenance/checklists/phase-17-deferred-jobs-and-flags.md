# Phase 17 — Deferred jobs, probes, freeze list

**Goal:** Explicit ship/park/delete for residual surfaces.  
**Can run early after Phase 00** (small PRs).  
**Evidence:** `../01-removable-dead-code.md` §2, roadmap residual list

---

## 17.1 RevenueRecognitionJob

- [ ] Re-read Phase 00.3 decision
- [ ] If delete:
  - [ ] Remove unregistered job class if unused
  - [ ] Remove entity/table only with migration + product OK
  - [ ] Remove metrics/docs that imply revenue recognition works
- [ ] If park:
  - [ ] README/ADR note: unregistered by design
  - [ ] Ensure no UI claims it runs
- [ ] If implement:
  - [ ] Separate product epic (not this checklist’s micro-tasks)

## 17.2 Scope-probe / leftover Phase-1 endpoints

- [ ] Locate `_scope-probe` or similar leftover routes
- [ ] Confirm real M2M checkout covers the need
- [ ] Delete probe **or** document as internal-only + auth
- [ ] Remove from TypeSpec if present

## 17.3 Stale duplicate integration events

- [ ] Grep for obsolete event types listed in residual checklists
- [ ] Confirm no handlers subscribe
- [ ] Delete dead event classes
- [ ] Do not delete events still in outbox history without care

## 17.4 WhatsApp / console-only channels

- [ ] Align with Phase 00.4
- [ ] If freeze: document console-only; no half-UI
- [ ] If ship: track under product Phase D, not silent maintenance

## 17.5 Host should not reference Application projects incorrectly

- [ ] Audit host csproj references (Contracts vs Application)
- [ ] Fix if host takes Application dependencies it should not
- [ ] Architecture tests encode the rule

## 17.6 Exit criteria

- [ ] Every deferred surface is ship, park (documented), or delete
- [ ] No probe endpoints without owners
