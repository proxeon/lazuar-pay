---
number: "240"
id: B05-L36
severity: P2
status: resolved
resolved_branch: fix/240-saas-dispute-reverse-fee
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 240 — B05-L36 — Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/240-saas-dispute-reverse-fee`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L36 — P2 — Hub SaaS dispute does not reverse `SYSTEM_SAAS_FEE`

`PAST_DUE` only. Expense/cash stay. A later win has nothing to unwind because nothing was reversed. A later loss that Stripe refunds is B05-L15. Period dates still grant access time.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
A Hub SaaS dispute (`metadata.type == platform_saas_fee`) only calls `WorkspaceSaasSubscription.MarkPastDue()`. It does not reverse the `SYSTEM_SAAS_FEE` journal (`EXPENSE_SOFTWARE_SUBSCRIPTION` / `ASSET_CASH`). Period dates (`CurrentPeriodStart` / `CurrentPeriodEnd`) are untouched, so access time still looks paid through period end. A later dispute win has nothing to unwind because nothing was reversed — status can go `ACTIVE` again on the next successful pay (`ActivateFromPayment` does not check `PAST_DUE`). A later loss that Stripe refunds is a different path: `GatewayDisputeLostHandler` **explicitly skips** platform-collected types, and inbound refunds (085) would book a `GATEWAY_REFUND` against a sale that is not `GATEWAY_PAYMENT` (lookup by `ReferenceType == GATEWAY_PAYMENT` misses `SYSTEM_SAAS_FEE`). Tenant books keep saying they paid Lazuar for the period.

### Still present?
**STILL BROKEN**

```50:54:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
        if (type == PlatformCheckoutTypes.PlatformSaasFee)
        {
            await MarkSaasPastDueAsync(@event);
            return;
        }
```

```124:128:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs
        subscription.MarkPastDue();
        await _dbContext.SaveChangesAsync();
        _logger.LogWarning(
            "Marked Hub SaaS subscription PAST_DUE for tenant {TenantId} after dispute {GatewayTxId}; credits unchanged.",
            tenantId, @event.GatewayTransactionId);
```

`MarkPastDue` only flips status (`WorkspaceSaasSubscription.cs:69-73`). Sale journal is written in `PlatformSaasFeeHandler.cs:91-103` as `SYSTEM_SAAS_FEE` and is never mirrored. Lost-dispute handler:

```34:37:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs
        if (@event.Metadata is not null
            && @event.Metadata.TryGetValue("type", out var type)
            && PlatformCheckoutTypes.IsPlatformCollected(type))
            return;
```

`ChargebackClawbackHandlerTests.PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits` asserts `PAST_DUE` and no credit claw; it does **not** assert absence/presence of a reverse ledger row (and the test never seeds a `SYSTEM_SAAS_FEE` row). Default `Saas:Plan:AmountMyr = 0` still means most workspaces never book this journal (**090**).

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ChargebackClawbackHandler.cs) — SaaS branch = PAST_DUE only.
- [`apps/lazuar-api/Modules/Billing/Domain/Aggregates/WorkspaceSaasSubscription.cs`](apps/lazuar-api/Modules/Billing/Domain/Aggregates/WorkspaceSaasSubscription.cs) — `MarkPastDue` / `ActivateFromPayment`.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/PlatformSaasFeeHandler.cs) — the journal that is not reversed.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayDisputeLostHandler.cs) — skips platform-collected.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ChargebackClawbackHandlerTests.cs) — locks PAST_DUE, not reverse.
- [`apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/PlatformSaasFeeHandlerTests.cs`](apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/PlatformSaasFeeHandlerTests.cs) — sale path.

### Tests
- Existing: `PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits`; `WorkspaceSaasSubscriptionTests.MarkPastDue_And_Cancel`; `PlatformSaasFeeHandlerTests` (book + idempotent + no `InvoiceIssued`).
- The SaaS dispute test **passes while the journal stays**. It would fail only if we started clawing credits (wrong fix).
- First regression: seed a `SYSTEM_SAAS_FEE` of 99, dispute `type=platform_saas_fee`, assert one balanced reverse (new ref type or mirrored lines), status `PAST_DUE`, period dates unchanged **or** explicitly cut, and a second Handle is idempotent. A win/lost-closed case must not double-reverse.

### Reproduction today
Operator must set `Saas:Plan:AmountMyr > 0` (default 0 throws on checkout — **090**). Arrange a paid Hub period (`SYSTEM_SAAS_FEE` + `ACTIVE`). Act: Stripe dispute webhook with `metadata.type=platform_saas_fee` and `tenant_id`. Assert: subscription `PAST_DUE`; `CurrentPeriodEnd` still in the future; ledger still has the original expense/cash and no reverse; `GET /admin/billing/saas` still shows the paid period window.

### Blast radius
Paying Hub workspaces only (not default config). Tenant’s own books overstate “we paid Lazuar” during an open dispute. Lazuar’s view of that tenant’s access window still includes the disputed period. Not GMV / not buyer SST. Frequency: rare (Hub disputes). Still P2 given **090** (most orgs unpaid). Becomes money-wrong the day Hub is sold at a positive MYR price.

### Suggested fix
On SaaS dispute, post an idempotent reverse of the matching `SYSTEM_SAAS_FEE` (same shape as utility `SYSTEM_CREDIT_CHARGEBACK`: negate original lines, unique on gateway tx). Keep `PAST_DUE`. Do **not** cancel Commerce subscriptions (LP-059 / wrap-rail: Hub is not Commerce; do not emit `subscription.updated`). Do not treat a later Stripe refund as GMV `GATEWAY_REFUND` — either skip or map it to the same reverse id. A win path should be a no-op if you never reversed, or a re-book if you did (product call). No TypeSpec. No Xero.

### Evaluation notes
Audit “later loss that Stripe refunds is B05-L15” is stale: **085** now delivers inbound refunds, but they will not find a `GATEWAY_PAYMENT` for this tx. Pair with **243** (comment still says utility-only). **009** claw-retry (L04) was fixed on the utility branch; SaaS still has no ledger write to make idempotent. Still P2. Not blocked on 090, but 090 hides the blast radius.

