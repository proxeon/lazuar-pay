# Phase 04 — Outbound webhooks convergence

**Depends on:** Phase 00.2 decision.  
**Goal:** One durable delivery model (or explicit permanent exception with docs).  
**Evidence:** residual B.4.4 / report 10 dual webhook stack.

---

## 04.1 Inventory

- [ ] Document One path: outbox table, dispatcher job, signing, multi-endpoint, retries/DLQ
- [ ] Document Lhdn path: `WebhookSenderService` (or equivalent) fire-and-forget
- [ ] List event types Lhdn sends outbound to customers
- [ ] List One workspace webhook event types
- [ ] Note HMAC / signature header differences

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

- [ ] ADR or module README: “LHDN outbound is fire-and-forget by design until …”
- [ ] Add observability (log/metric) so failures are visible
- [ ] No further “improvements” to a second full stack without reopening 00.2

## 04.3 Cleanup after convergence (A or B)

- [ ] Delete or gut unused fire-and-forget service if unused
- [ ] Grep for duplicate HMAC helpers; consolidate if safe
- [ ] Update `apps/lazuar-api/docs` webhook runbooks

## 04.4 Exit criteria

- [ ] Option A/B/C implemented as decided
- [ ] No silent dual “full stacks” without docs
- [ ] Tests cover the chosen durable path (or freeze metrics exist for C)
