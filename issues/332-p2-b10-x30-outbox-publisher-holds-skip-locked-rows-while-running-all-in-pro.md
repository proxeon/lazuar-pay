---
number: "332"
id: B10-X30
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 332 — B10-X30 — Outbox publisher holds SKIP LOCKED rows while running all in-process handlers

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X30 — P2 — Outbox publisher holds SKIP LOCKED rows while running all in-process handlers

`OutboxPublisherJob` begins a transaction, locks ≤20 rows, then `await eventBus.PublishAsync` which runs every handler (Billing + Commerce + Communications on `GatewayPaymentCompleted`, etc.) **before** commit. Long MyInvois-adjacent or HTTP-inside-handler work (there should be none; outbound HTTP is the dispatcher) extends lock time. A throwing handler mid-list fails the **event**; already-run handlers in that same `PublishAsync` have already committed their own DbContexts. Retry re-runs everyone. Idempotency is per-handler (`HasEntryBeenProcessedAsync`, unique disputes). Untested as a composition.

`InboxConsumerJob` same lock-across-mediatR shape.

