# Phase 04 — Outbound webhooks convergence

**Depends on:** Phase 00.2 decision.  
**Goal:** One durable delivery model (or explicit permanent exception with docs).  
**Evidence:** residual B.4.4 / report 10 dual webhook stack.

**This phase outcome:** **C freeze** active + inventory + Lhdn failure observability. End-state **A** deferred (not trivial).

---

## 04.1 Inventory

- [x] Document One path: outbox table, dispatcher job, signing, multi-endpoint, retries/DLQ
- [x] Document Lhdn path: `WebhookSenderService` (or equivalent) fire-and-forget
- [x] List event types Lhdn sends outbound to customers
- [x] List One workspace webhook event types
- [x] Note HMAC / signature header differences

→ Evidence: `plans/004-maintenance/phase-04-analysis.md`

## 04.2 Implement chosen option from 00.2

### If A — Route LHDN through One dispatcher

- [ ] Map LHDN lifecycle events → One delivery commands/outbox
- [ ] Preserve payload schema customers already expect (or version bump + docs)
- [ ] Remove/stop calling fire-and-forget sender for those events
- [ ] Tests: publish LHDN event → outbox row → dispatch attempt

### If B — Lhdn gets same primitives

- [ ] Introduce Lhdn delivery outbox **or** shared BB outbox helper for HTTP webhooks
- [ ] Retries + dead-letter parity with One
- [ ] Signing parity or documented second scheme
- [ ] Tests for retry/failure

### If C — Freeze special-case

- [x] ADR or module README: “LHDN outbound is fire-and-forget by design until …”
- [x] Add observability (log/metric) so failures are visible
- [x] No further “improvements” to a second full stack without reopening 00.2

→ Evidence: `Modules/Lhdn/README.md` §5; `Modules/One/README.md` §7; `WebhookSenderService` structured logs + `RecordWebhookFailed("lhdn")`

## 04.3 Cleanup after convergence (A or B)

- [ ] Delete or gut unused fire-and-forget service if unused
- [ ] Grep for duplicate HMAC helpers; consolidate if safe
- [ ] Update `apps/lazuar-api/docs` webhook runbooks

*(Deferred until A ships.)*

## 04.4 Exit criteria

- [x] Option A/B/C implemented as decided — **C freeze interim; A end-state documented, not coded**
- [x] No silent dual “full stacks” without docs
- [x] Tests cover the chosen durable path (or freeze metrics exist for C) — metric tag `source=lhdn` on failure; One path already tested
