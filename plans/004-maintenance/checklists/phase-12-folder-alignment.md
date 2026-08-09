# Phase 12 — Folder layout alignment (DI-safe)

**Goal:** Modules look like Commerce without breaking MediatR/outbox.  
**Evidence:** `../03-folder-organization.md`  
**PR shape:** One module per PR preferred.

---

## 12.1 Messaging layout

- [x] Move hosted jobs into `Infrastructure/Workers/`
- [x] Move root-level event handlers into `Infrastructure/EventHandlers/` (or Application if that’s the module pattern)
- [x] Update namespaces to match folders
- [x] Confirm DI registration still finds hosted services
- [x] Confirm MediatR still registers handlers (assembly scan)

## 12.2 Endpoint monoliths → `Endpoints/` (if not done)

For each module still using single `Endpoints.cs`:

- [x] Billing — Admin ledger/credits/profile + public documents
- [x] Lhdn — documents + admin keys/webhooks/config
- [x] Ops — chat + stream + execute-action
- [x] Payments — confirm already split (`IntegrationEndpoints`, `PlatformEndpoints`); tidy only if needed

Rules:

- [x] Keep `Map*Endpoints` public names
- [x] No path changes

## 12.3 Contracts folder consistency (optional)

- [ ] CRM/Messaging flat contracts → `Commands/`, `Events/` subfolders when touching _(skipped — optional; single Messaging event; CRM not restructured)_
- [x] Ops empty Contracts: document “agent module intentionally hollow” or add placeholder README
- [x] Do not invent Contracts for Ops without product need

## 12.4 Handler layer documentation (not full rebalance)

- [x] Document in module README: Billing handlers live in Infrastructure today
- [x] Document CRM has no Application (arch-test exception)
- [x] Full Billing Application rebalance = separate epic (ports + tests) — **out of this phase**

## 12.5 Solution / packages hygiene

- [x] `Lazuar.slnx` empty folders already cleaned (Phase 01) or finish here
- [x] Confirm `api-types-dotnet` solution folder placement is intentional (fix path only if misleading) — **moved to `/Packages/`**

## 12.6 Exit criteria

- [x] Messaging matches Workers/EventHandlers convention
- [x] At least One (Phase 07) + Billing or Lhdn endpoints match Commerce style _(Billing + Lhdn + Ops)_
- [x] Build + architecture tests green
