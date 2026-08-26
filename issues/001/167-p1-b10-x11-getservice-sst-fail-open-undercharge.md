---
number: "167"
id: B10-X11
severity: P1
status: resolved
resolved_branch: fix/167-sst-fail-closed
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 167 — B10-X11 — `GetService` SST fail-open (undercharge)

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/167-sst-fail-closed`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X11 — P1 — `GetService` SST fail-open (undercharge)

```65:73:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            return false;
        }
```

`BillingEngineJob` and `DunningEngineJob.Claim` resolve `IBillingQueryService` with `GetService` (optional). If the billing module is not composed (test host, future extract, registration typo), every renewal and dunning AUTO_CHARGE is **net, no SST**. Production `AddAllModules` registers billing, so this is a composition footgun, not today’s happy path.

Public arrears also `GetService<IBillingQueryService>()` (`PublicArrearsEndpoints.cs` 56, 143). Same fail-open on the buyer-facing amount.

`SubscriptionLifecycleIntegrationEventHandlers` takes `IBillingQueryService? billingQueryService = null`. In production DI this is resolved. In tests that construct the handler without it, webhook `amount` is net.

Fail-**open** (charge too little) rather than fail-closed (refuse to bill).

