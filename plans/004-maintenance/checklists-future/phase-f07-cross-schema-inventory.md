# F07 — Cross-schema SQL inventory (FW-4)

**Goal:** Fresh, ticketable list of runtime boundary leaks.  
**Depends on:** F00 SQL track selected  
**Do not:** Fix leaks in this phase (that is F08)

---

## F07.1 Search

- [ ] Grep for raw SQL / `FromSql` / Dapper across module boundaries
- [ ] Grep schema-qualified names (`one.`, `commerce.`, `billing.`, `lhdn.`, etc.) outside owning module
- [ ] Review `PlatformMetricsCollector` multi-schema SQL
- [ ] Review Communications / Commerce / Payments known suspects from plan 04

## F07.2 Ticket table

For each leak:

| # | Location (file) | Foreign schema | Consumer module | Proposed fix (port / event / denorm) | Priority |
|---|-----------------|----------------|-----------------|--------------------------------------|----------|
| 1 | | | | | P0/P1/P2 |
| 2 | | | | | |

- [ ] Write table into `plans/004-maintenance/cross-schema-leaks.md` or this file

## F07.3 Exit

- [ ] Inventory committed
- [ ] F08 can pick P0/P1 items one PR each
