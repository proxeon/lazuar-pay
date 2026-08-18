---
number: "191"
id: B01-C21
severity: P2
status: resolved
resolved_branch: fix/191-confirm-reservation-guard
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 191 — B01-C21 — Mark-paid / zero-amount `ConfirmReservation` throws if reserved was already released

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/191-confirm-reservation-guard`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C21 — Mark-paid / zero-amount `ConfirmReservation` throws if reserved was already released

**Severity:** P2  
**One-sentence fault:** Unlike the webhook (`ReservedCount > 0` guard), ProcessZeroAmount and mark-paid call `ConfirmReservation()` unconditionally once the coupon row exists.

**Evidence.** `Coupon.ConfirmReservation` throws when `ReservedCount <= 0`. Webhook:

```25:28:apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.OpenCheckout.cs
            if (coupon != null && coupon.ReservedCount > 0)
            {
                coupon.ConfirmReservation();
            }
```

Zero/offline have no such guard (`ProcessZeroAmountCheckoutCommand.cs` 48–53; mark-paid 83–87).

**Reproduction in words.** Expiry also sets EXPIRED, so mark-paid will not run on an expired row. Remaining path: two zero-amount calls, or a reserve lost to B01-C02 last-write-wins so this instance’s coupon has `ReservedCount` 0. Confirm throws, session not completed, buyer 400.

**Blast radius / tests / fix.** Narrow. Happy-path reserve-then-confirm. Use the webhook’s `ReservedCount > 0` guard.

---

