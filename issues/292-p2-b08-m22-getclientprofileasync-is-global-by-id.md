---
number: "292"
id: B08-M22
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 292 — B08-M22 — `GetClientProfileAsync` is global-by-id

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M22 — P2 — `GetClientProfileAsync` is global-by-id

**Where:** `CrmQueryService.cs` 54–61.

**What:** `IgnoreQueryFilters`, no `OrganizationId`. A leaked UUID is a PII read. Callers today pass ids from their own tenant rows.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`CrmQueryService.GetClientProfileAsync` is the in-process CRM read used by Communications mail, dunning, billing, arrears, and Commerce anonymize. At audit HEAD it took only a profile GUID, used `IgnoreQueryFilters`, and had no `OrganizationId` predicate, so any caller with a leaked UUID could load another tenant’s name, email, phone, TIN, company, id numbers, and address. `GetClientProfilesAsync` was the same hole. Callers already had org ids from their own rows, so the live blast was a tenancy landmine rather than a painted HTTP leak. Issue 165 (B10-X09, P1) filed the same defect against the tenancy slice and was resolved on `fix/165-crm-profile-org-scope`.

### Still present?
**ALREADY FIXED**

The port and implementation now require both ids and filter on both:

```54:61:apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs
    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Id == profileId);
```

```10:12:apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs
    Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId);
    Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(Guid organizationId, IEnumerable<Guid> profileIds);
    Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email);
```

`GetClientProfilesAsync` (65–76) is the same org+id filter. Production callers pass the owning org: `LifecycleEventHandlers.cs:46`, `FulfillmentRequestedIntegrationEventHandler.cs:77`, `DunningStepDispatcher.cs:72`, `AnonymizeSubscriberCommandHandler.cs:35`. Isolation is locked by `CrmQueryServiceTenantIsolationTests.GetClientProfileAsync_Does_Not_Return_Another_Tenant` (`apps/lazuar-api/tests/Lazuar.ModuleTests/CRM/CrmQueryServiceTenantIsolationTests.cs:18–47`). Likely commit: `42b7ad37` (`fix(crm): require organization id on profile-by-guid reads`) on issue 165. Do not change YAML `status` here.

### Related files
- `apps/lazuar-api/Modules/CRM/Infrastructure/CrmQueryService.cs` — the query that used to be global-by-id.
- `apps/lazuar-api/Modules/CRM/Contracts/ICrmQueryService.cs` — two-arg contract every caller must match.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/CRM/CrmQueryServiceTenantIsolationTests.cs` — tenant-isolation regression.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/GatewayPaymentFailedIntegrationEventHandlerTests.cs:49` — comment documents the two-Guid signature as issue 165.
- `issues/165-p1-b10-x09-crmqueryservice-getclientprofileasync-is-a-global-pii-read-by-gu.md` — the P1 that already closed this hole.

### Tests
- Existing: `CrmQueryServiceTenantIsolationTests.GetClientProfileAsync_Does_Not_Return_Another_Tenant`. Many Communications/Commerce tests stub `GetClientProfileAsync(Arg.Any<Guid>(), …)` with two Guids.
- Would a test fail if the bug were still there? Yes — the isolation test asserts `GetClientProfileAsync(otherOrg, profile.Id)` is null and the owner org still sees TIN.
- First extra regression (only if someone reopens 165): assert `GetClientProfilesAsync(otherOrg, [profile.Id])` is empty too. That batch overload is not covered today.

### Reproduction today
Arrange two tenants in CRM, insert a profile under tenant A. Act: call `GetClientProfileAsync(tenantB, profile.Id)`. Assert: null. Act: call `GetClientProfileAsync(tenantA, profile.Id)`. Assert: DTO with that buyer’s PII. There is no public HTTP surface on CRM; reproduce via the service or any Communications handler that already has both ids.

### Blast radius
Cross-tenant PII (name, email, phone, TIN, NRIC/BRN, address) if a GUID leaked into another org’s job. After 165, a wrong org id returns null and mail/dunning fail closed instead of leaking. Residual risk is only if a future overload drops the org predicate again.

### Suggested fix
None. Keep the two-arg port. If you touch this file, do not restore a GUID-only overload. Do not regenerate TypeSpec. This is not a Stripe/WhatsApp/Xero change.

### Evaluation notes
Duplicate of resolved **165** (B10-X09, P1). 292 is the Communications-slice restatement of the same method. Severity as a live P2 is stale — the tree is already fail-closed. Leave YAML `status: open` per instructions. No product work remains unless 165 is reverted.

