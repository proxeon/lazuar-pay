---
number: "028"
id: B01-C02
severity: P1
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/028-coupon-lock-transaction
---

# 028 — B01-C02 — Coupon `FOR UPDATE` is not inside a transaction

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/028-coupon-lock-transaction`

Hop-1 coupon lock, reserve, and session insert run in one Commerce transaction so `FOR UPDATE` holds until `SaveChanges`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C02 — Coupon `FOR UPDATE` is not inside a transaction

**Severity:** P1  
**One-sentence fault:** `GetCouponByCodeWithLockAsync` issues `SELECT … FOR UPDATE` and then `Validate` / `Reserve` / `SaveChanges` run without an ambient transaction, so the row lock is gone before the reservation is committed.

**Evidence.**

```66:76:apps/lazuar-api/Modules/Commerce/Infrastructure/Repositories/CommerceRepository.cs
    public async Task<Coupon?> GetCouponByCodeWithLockAsync(...)
    {
        return await _context.Coupons
            .FromSqlRaw(@"
                SELECT * FROM commerce.""Coupons"" 
                WHERE ""OrganizationId"" = {0} AND ""Code"" = {1} AND ""IsActive"" = true 
                FOR UPDATE", organizationId, normalizedCode)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
    }
```

There is no `IPipelineBehavior` transaction wrapper in this repo (grep of `IPipelineBehavior` / `BeginTransaction` in Commerce HTTP handlers is empty). BillingEngineJob and InboxConsumerJob start their own transactions; initiate does not. In PostgreSQL a `FOR UPDATE` taken outside a transaction is released at the end of the statement.

`Coupon.Validate` then reads the in-memory `UsedCount + ReservedCount` snapshot from that SELECT.

```88:91:apps/lazuar-api/Modules/Commerce/Domain/Aggregates/Coupon.cs
        if (MaxUses > 0 && (UsedCount + ReservedCount) >= MaxUses)
        {
            CheckRule(new GenericBusinessRule("This coupon has reached its maximum usage limit."));
        }
```

**Reproduction in words.** Coupon `MaxUses = 1`. Two buyers submit hop-1 with that code in the same second. Both SELECTs see reserved=0, both `Validate`, both `Reserve` in memory (`ReservedCount = 1` on two tracked instances), both `SaveChanges`. Last writer wins on the integer columns. Two OPEN sessions hold the same coupon. Two payments can confirm (webhook only checks `ReservedCount > 0` at confirm time; after two last-write-wins the DB reserved count may be 1, so the second confirm can throw — or, if both confirm before the other’s save, used can go to 2 while max is 1).

**Blast radius.** Limited-run launch codes, “first 50 customers”, influencer one-use codes. Merchant promised a cap. The cap is not a serializable constraint; it is an unlocked integer.

**Why tests missed it.** `CouponLifecycleTests` are single-threaded in-memory. Completeness tests reserve once. There is no concurrent initiate test and InMemoryDatabase would not honour `FOR UPDATE` anyway.

**Fix direction.** Open a transaction on the Commerce context that covers lock + validate + reserve + session insert + `SaveChanges`. Or add a check constraint / trigger. A unique “one confirmed redemption per (coupon, client)” is a second line of defence, not a substitute for the lock.

---

