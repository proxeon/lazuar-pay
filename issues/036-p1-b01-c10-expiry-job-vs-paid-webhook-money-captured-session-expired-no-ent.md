---
number: "036"
id: B01-C10
severity: P1
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 036 — B01-C10 — Expiry job vs paid webhook: money captured, session EXPIRED, no entitlement

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C10 — Expiry job vs paid webhook: money captured, session EXPIRED, no entitlement

**Severity:** P1  
**One-sentence fault:** The expiry job expires any OPEN row past `ExpiresAt` without locking against an in-flight payment; a late webhook then no-ops because status ≠ OPEN.

**Evidence.** Expiry (`CheckoutSessionExpiryJob.ExpireSessionsAsync` 56–91) loads OPEN + past ExpiresAt, `Expire()`, `ReleaseReservation()`, save. Webhook only fulfills `session.Status == "OPEN"`. Product sessions expire 24h after create (`AddHours(24)`). Custom sessions can be 30d + due+14.

**Reproduction in words.** Buyer opens hop-2 at hour 23:59. Pays at hour 24:02. Expiry tick at 24:00 already released the coupon and set EXPIRED. Webhook finds EXPIRED, looks up a subscription by session id, returns. Processor settled. Commerce has no sub. Status poller shows EXPIRED, not COMPLETED.

**Blast radius.** Slow banks / FPX / abandoned-then-returned Billplz pages near the 24h edge. Combined with B01-C04, an idempotent retry after expiry hands the same dead-or-still-payable processor URL back.

**Why tests missed it.** Expiry test uses `AddHours(-2)` and never pays. Webhook tests use `AddHours(1)`.

**Fix direction.** Expiry should skip rows that have a `GatewayCheckoutUrl` and a recently updated timestamp, or use a compare-and-swap that loses to Complete. Prefer: if a payment arrives for EXPIRED, **revive and fulfill** (and re-confirm or increment used without requiring reserved). Do not silently drop a paid event.

---

