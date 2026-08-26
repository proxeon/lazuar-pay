---
number: "171"
id: B10-X15
severity: P1
status: resolved
resolved_branch: fix/171-m2m-status-filter
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 171 — B10-X15 — M2M `?status=` filters the current page and rewrites `total_count`

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/171-m2m-status-filter`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X15 — P1 — M2M `?status=` filters the current page and rewrites `total_count`

```37:42:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/IntegrationSubscriptionEndpoints.cs
            if (!string.IsNullOrWhiteSpace(status))
            {
                var filtered = response.Data.Where(s =>
                    string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
                response = new PaginatedResponse<CommerceSubscriptionDto>(filtered, filtered.Count, p, l);
            }
```

`GetSubscribersAsync` already loaded **every** non-`PENDING` row for the org (no SQL `LIMIT`), then paged in memory (`CommerceQueryService.Subscribers.cs` 52–72). The endpoint then filters **that page** and sets `total_count` to the filtered page size. `GET ?status=TRIALING&page=1` can return 3 rows and `total_count=3` when the tenant has 40 trials.

008 H9. Still present. `cbe17c2` added the paths to combined OpenAPI; it did not fix the semantics. Honesty cannot see this.

The unbounded load is itself a P2 ops risk at large subscriber counts (admin list and M2M share the query).

