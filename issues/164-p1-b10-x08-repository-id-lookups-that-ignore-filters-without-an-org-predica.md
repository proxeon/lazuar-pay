---
number: "164"
id: B10-X08
severity: P1
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 164 — B10-X08 — Repository ID lookups that **ignore** filters without an org predicate

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X08 — P1 — Repository ID lookups that **ignore** filters without an org predicate

```22:28:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
```

Same shape: `GetSubscriptionByIdAsync`, `GetTransactionLogByIdAsync`.

`CrossTenantIdorTests` prove **some** command handlers re-check `OrganizationId` after the load. They do not prove every caller. `SubscriptionLifecycleIntegrationEventHandlers` loads subscription by id only, then uses `sub.Status` and `sub.OrganizationId` from the **row**, not from the event, for payload status. If a future bug ever passed the wrong id, this would leak another tenant’s commercial fields into a webhook signed as the event’s org.

`GetProductByIdAsync` in the same handler is not org-scoped. Product GUIDs are unique; the residual risk is a swapped id, not a guess.

Architecture tests do **not** ban `IgnoreQueryFilters()` without an `OrganizationId` predicate.

