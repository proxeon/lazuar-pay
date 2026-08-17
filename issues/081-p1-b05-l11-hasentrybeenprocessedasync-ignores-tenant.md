---
number: "081"
id: B05-L11
severity: P1
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 081 — B05-L11 — `HasEntryBeenProcessedAsync` ignores tenant

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L11 — P1 — `HasEntryBeenProcessedAsync` ignores tenant

```18:24:apps/lazuar-api/Modules/Billing/Infrastructure/Repositories/LedgerRepository.cs
        return await _context.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.ReferenceType == referenceType && e.ReferenceId == referenceId, ct);
```

Same grain as the unique index. Workers **must** `IgnoreQueryFilters` (empty tenant). They also **must** then filter by org; this method does not. Org A’s `GATEWAY_PAYMENT` / `pi_123` makes org B’s same id look “already processed” **if** the unique insert did not already throw. The check and the index agree with each other and disagree with tenancy.

Refund tax lookup (`GatewayRefundCompletedHandler:94-100`) **does** filter `OrganizationId`. Inconsistent, and the one that matters for money on refunds is the org-scoped one. The sale idempotency path is the unscoped one.

---

