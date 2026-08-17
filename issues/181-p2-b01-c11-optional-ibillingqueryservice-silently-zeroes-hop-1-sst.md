---
number: "181"
id: B01-C11
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 181 — B01-C11 — Optional `IBillingQueryService` silently zeroes hop-1 SST

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C11 — Optional `IBillingQueryService` silently zeroes hop-1 SST

**Severity:** P2 (latent in production; pinned in tests)  
**One-sentence fault:** `InitiateCheckoutCommandHandler` takes `IBillingQueryService? = null`; `MerchantHasSstAsync` returns false when billing is null, so SST is skipped without a log.

**Evidence.**

```28:36:apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs
    private readonly IBillingQueryService? _billingQueryService;
    public InitiateCheckoutCommandHandler(..., IBillingQueryService? billingQueryService = null)
```

```65:73:apps/lazuar-api/Modules/Commerce/Application/SubscriptionBillingAmount.cs
    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            return false;
        }
        var profile = await billing.GetBillingProfileAsync(organizationId);
        return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
    }
```

Monolith registers `IBillingQueryService` in `Billing/Infrastructure/DependencyInjection.cs`. Production MediatR will inject it. Tests (`CreateInitiateHandler`, B2B tests, completeness) construct the handler **without** billing. `SubscriptionBillingAmountTests.Gross_NoSst_Is100` **asserts** `billing: null` → 100.

**Reproduction in words.** Any host that composes Commerce without Billing (a future extract, a test server, a worker) undercharges SST on hop-1. Today’s API process is not that host.

**Blast radius.** Test suite cannot see hop-1 SST at all (see §7). A module-split would ship undercharge.

**Why tests missed it / pin it.** They treat null billing as the happy path.

**Fix direction.** Required constructor parameter. A missing billing dependency should fail DI, not fail closed to “no tax”. Add an initiate test with a stub profile `Sst_registration_number = "W10-…"` and assert `GenerateCheckoutSessionQuery.Amount == 108` for a 100 / 8% unit.

---

