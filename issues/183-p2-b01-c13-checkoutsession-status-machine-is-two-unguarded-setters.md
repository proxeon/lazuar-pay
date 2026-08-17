---
number: "183"
id: B01-C13
severity: P2
status: open
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
---

# 183 — B01-C13 — `CheckoutSession` status machine is two unguarded setters

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C13 — `CheckoutSession` status machine is two unguarded setters

**Severity:** P2  
**One-sentence fault:** `Complete()` and `Expire()` do not refuse COMPLETED→EXPIRED, EXPIRED→COMPLETED, or double complete.

**Evidence.** Domain block in §4.6. Expiry query is `Status == "OPEN"`, so the job will not expire a COMPLETED row if the filter is honoured. A future caller that loads by id and calls `Expire()` will.

**Reproduction in words.** Today only via a new caller or a missed filter. Not a current HTTP path by itself.

**Blast radius.** Status lies in the poller if it ever happens (`EXPIRED` after a real COMPLETED, or COMPLETED of an EXPIRED without fulfillment).

**Why tests missed it.** No domain test of illegal transitions.

**Fix direction.** Guard: Complete only from OPEN; Expire only from OPEN; throw otherwise.

---

