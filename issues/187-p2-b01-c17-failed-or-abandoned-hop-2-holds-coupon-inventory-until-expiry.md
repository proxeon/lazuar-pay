---
number: "187"
id: B01-C17
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 187 — B01-C17 — Failed or abandoned hop-2 holds coupon inventory until expiry

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C17 — Failed or abandoned hop-2 holds coupon inventory until expiry

**Severity:** P2  
**One-sentence fault:** `GatewayPaymentFailed` does not look at OPEN checkout sessions; `ReleaseReservation` only runs in the expiry job.

**Evidence.** `GatewayPaymentFailedIntegrationEventHandler` resolves a **subscription** id and returns if none. Coupon `Reserve` happens at initiate. Expiry is 24h (product) or longer (quotes, unused). `Validate` counts reserved toward `MaxUses`.

**Reproduction in words.** MaxUses=1. Buyer initiates, Billplz fails, session stays OPEN, reserved=1. A second buyer cannot use the code for up to 24 hours.

**Blast radius.** Tight caps during a launch. Not money loss; inventory freeze.

**Why tests missed it.** Failed-payment tests are subscription-shaped.

**Fix direction.** On hop-2 failure (or explicit cancel webhook), release if the session is still OPEN, or shorten product session TTL. Do not confirm on failure.

---

