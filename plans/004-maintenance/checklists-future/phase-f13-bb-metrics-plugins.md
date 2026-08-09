# F13 — Metrics contributors (FW-3 / FW-4)

**Goal:** Stop growing god SQL in `PlatformMetricsCollector`.  
**Depends on:** optional after F07/F08 inventory of metrics leaks

---

## F13.1 Design

- [ ] Define `IPlatformMetricsContributor` (or equivalent) registration
- [ ] BB aggregator only sums contributions + technical outbox lag

## F13.2 Move product SQL

- [ ] LHDN stuck-document metrics → Lhdn contributor
- [ ] Dunning-related counters → Commerce contributor if still in BB
- [ ] Webhook product metrics ownership clarified (One vs Lhdn)

## F13.3 Tests

- [ ] Metrics endpoint still returns expected shape
- [ ] No private foreign-schema SQL left in BB collector (or exception documented)

## F13.4 Exit

- [ ] 009 map updated; FUTURE-WORK FW-3/4 notes metrics done or residual listed
