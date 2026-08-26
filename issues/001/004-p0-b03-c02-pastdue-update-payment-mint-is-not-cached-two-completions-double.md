---
number: "004"
id: B03-C02
severity: P0
status: resolved
source: plans/009-bugs/03-commerce-dunning-arrears-portal.md
head: "297ba98"
resolved_branch: fix/004-pastdue-renewal-checkout-cache
---

# 004 — B03-C02 — PAST_DUE update-payment mint is not cached; two completions double-capture and skip a cycle

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/03-commerce-dunning-arrears-portal.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/004-pastdue-renewal-checkout-cache`

PAST_DUE/SUSPENDED update-payment now stores `CurrentRenewalCheckoutUrl` keyed to `NextBillingDate`, so a second POST reuses the same hosted session. A second `PAYMENT_COMPLETED` after the row is already paid-through stores the vault but does not roll `NextBillingDate` or fire another `subscription.activated`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B03-C02 — P0 — PAST_DUE update-payment mint is not cached; two completions double-capture and skip a cycle

**Evidence.** Persist branch is ACTIVE-only:

```197:204:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PublicArrearsEndpoints.cs
                if (isActiveUpdate)
                {
                    await Dapper.SqlMapper.ExecuteAsync(connection, @"
                        UPDATE commerce.""Subscriptions""
                        SET ""CurrentRenewalCheckoutUrl"" = @Url, ""CurrentRenewalCheckoutForDate"" = @ForDate
                        WHERE ""Id"" = @SubId",
                        new { Url = checkoutUrl, ForDate = DateTime.UtcNow.Date, SubId = subId });
                }
```

Completed handler after first recover:

```71:83:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs
        if (wasSuspended) { existingSub.Resume(updatedNextBilling); }
        else if (existingSub.Status == "PAST_DUE")
        {
            existingSub.RecoverFromPayment(periodEnd, updatedNextBilling);
        }
        else
        {
            existingSub.Activate(periodEnd, updatedNextBilling, existingSub.IsReminderOnly);
        }
```

Arrears Gross mint does **not** set `update_payment`. Second completion is the `else` branch.

**Repro.** Stripe vault fail (no `CurrentRenewalCheckoutUrl`). Open Complete Payment twice before paying. Pay both Checkout sessions.

**Blast.** Buyer charged 2× Gross. `NextBillingDate` jumps two intervals. Dunning recovery metrics record only the first (`DunningRecoveryAttribution` returns null when `wasInArrears` is false). Ledger (report 05) books both.

**Tests.** None. Need: two POSTs without a stored URL create two sessions; after first recover, second completion must **not** call `Activate` / must treat as already-settled (idempotent on `GatewayTransactionId` or leftover PAST_DUE checkout).

**Fix direction.** Persist URL + `NextBillingDate` for PAST_DUE/SUSPENDED the same way Billing does. On completed, if already ACTIVE and metadata is a dunning/arrears pay (no `update_payment`), ignore date roll; refund-or-credit is a product decision but must not silently skip a cycle.

---

