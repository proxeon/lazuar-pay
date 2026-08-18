---
number: "165"
id: B10-X09
severity: P1
status: resolved
resolved_branch: fix/165-crm-profile-org-scope
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 165 — B10-X09 — `CrmQueryService.GetClientProfileAsync` is a global PII read by GUID

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/165-crm-profile-org-scope`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X09 — P1 — `CrmQueryService.GetClientProfileAsync` is a global PII read by GUID

```54:62:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == profileId);
```

Returns name, email, phone, TIN, company, id numbers, address. Any in-process caller with a profile GUID (Commerce document lookup, lifecycle webhooks, billing engine, arrears) can read another tenant’s CRM row. `GetClientProfilesAsync(IEnumerable<Guid>)` is the same.

`GetClientProfileByEmailAsync` **does** take `organizationId`. The id-based overloads do not.

This is the widest remaining `IgnoreQueryFilters` leak that is not “worker must see all orgs.” CRM resolve/create **do** constrain org. The query service used everywhere else does not.

