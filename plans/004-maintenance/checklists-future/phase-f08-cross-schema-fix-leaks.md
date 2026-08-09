# F08 — Fix cross-schema leaks (one PR per leak family)

**Goal:** Replace foreign-schema SQL with Contracts ports / events / denormalized data.  
**Depends on:** F07 inventory  
**Rule:** Prefer multiple PRs over one mega-PR

---

## F08.0 For each P0/P1 leak (repeat)

- [ ] Leak id / title: ________
- [ ] Define read model DTO on **owning** module Contracts
- [ ] Implement query service in owning Infrastructure
- [ ] Replace consumer SQL with port call
- [ ] Tests (unit/module) for happy path + tenant isolation
- [ ] No new SQL added to BuildingBlocks to “fix” it
- [ ] PR merged / committed

*(Duplicate this subsection in PR description for each leak.)*

## F08.1 Metrics path (if in inventory)

- [ ] Introduce `IPlatformMetricsContributor` (or equivalent) **or** move LHDN/dunning SQL to owning module
- [ ] Thin BB collector to aggregate contributions only

## F08.2 Exit

- [ ] All P0 inventory items fixed
- [ ] P1 fixed or explicitly deferred with ticket ids
- [ ] FW-4 status updated in FUTURE-WORK.md
