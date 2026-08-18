---
number: "189"
id: B01-C19
severity: P2
status: resolved
resolved_branch: fix/189-session-coupon-ignore-filters
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 189 — B01-C19 — Session-by-id and coupon-by-id repository loads honour the fail-closed tenant filter

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/189-session-coupon-ignore-filters`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C19 — Session-by-id and coupon-by-id repository loads honour the fail-closed tenant filter

**Severity:** P2  
**One-sentence fault:** `GetCheckoutSessionByIdAsync` and `GetCouponByIdAsync` do not `IgnoreQueryFilters`; a worker or empty ambient tenant cannot see the row.

**Evidence.** `CommerceRepository.cs` 54–56 and 78–81. `PlatformDbContext` filter: `OrganizationId == ExecutionContext.TenantId`; empty tenant matches nothing. ProcessZeroAmount and mark-paid use those methods. HTTP sets `TenantId` first, so today’s portal/admin paths work. Webhook correctly uses `IgnoreQueryFilters` + org predicate.

**Reproduction in words.** If ProcessZeroAmount is ever dispatched from a background scope with empty tenant (outbox replay of a command, a job), it throws “invalid or already processed” on a perfectly OPEN session.

**Blast radius.** Latent. Not the current HTTP initiate (ambient tenant is set in `PublicCheckoutEndpoints`).

**Why tests missed it.** Substitutes do not apply query filters.

**Fix direction.** Mirror `GetCheckoutSessionByIdempotencyKeyAsync`: `IgnoreQueryFilters` + explicit org id on every load that already has an org in the command.

---

