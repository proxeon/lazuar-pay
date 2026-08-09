# Phase 12 — Folder layout alignment (DI-safe)

**Goal:** Modules look like Commerce without breaking MediatR/outbox.  
**Evidence:** `../03-folder-organization.md`  
**PR shape:** One module per PR preferred.

---

## 12.1 Messaging layout

- [ ] Move hosted jobs into `Infrastructure/Workers/`
- [ ] Move root-level event handlers into `Infrastructure/EventHandlers/` (or Application if that’s the module pattern)
- [ ] Update namespaces to match folders
- [ ] Confirm DI registration still finds hosted services
- [ ] Confirm MediatR still registers handlers (assembly scan)

## 12.2 Endpoint monoliths → `Endpoints/` (if not done)

For each module still using single `Endpoints.cs`:

- [ ] Billing — Admin ledger/credits/profile + public documents
- [ ] Lhdn — documents + admin keys/webhooks/config
- [ ] Ops — chat + stream + execute-action
- [ ] Payments — confirm already split (`IntegrationEndpoints`, `PlatformEndpoints`); tidy only if needed

Rules:

- [ ] Keep `Map*Endpoints` public names
- [ ] No path changes

## 12.3 Contracts folder consistency (optional)

- [ ] CRM/Messaging flat contracts → `Commands/`, `Events/` subfolders when touching
- [ ] Ops empty Contracts: document “agent module intentionally hollow” or add placeholder README
- [ ] Do not invent Contracts for Ops without product need

## 12.4 Handler layer documentation (not full rebalance)

- [ ] Document in module README: Billing handlers live in Infrastructure today
- [ ] Document CRM has no Application (arch-test exception)
- [ ] Full Billing Application rebalance = separate epic (ports + tests) — **out of this phase**

## 12.5 Solution / packages hygiene

- [ ] `Lazuar.slnx` empty folders already cleaned (Phase 01) or finish here
- [ ] Confirm `api-types-dotnet` solution folder placement is intentional (fix path only if misleading)

## 12.6 Exit criteria

- [ ] Messaging matches Workers/EventHandlers convention
- [ ] At least One (Phase 07) + Billing or Lhdn endpoints match Commerce style
- [ ] Build + architecture tests green
