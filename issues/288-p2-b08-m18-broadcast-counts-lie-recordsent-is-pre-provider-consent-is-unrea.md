---
number: "288"
id: B08-M18
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 288 — B08-M18 — Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M18 — P2 — Broadcast counts lie; RecordSent is pre-provider; consent is unreachable from checkout

**Where:** `SendBroadcastCommandHandler` 35; `GetActiveSubscriberCountAsync` vs `GetActiveSubscriberRecipientsAsync`; `BroadcastFanoutJob` 174–185; `InitiateCheckoutCommand` (no consent field); `Resolve` (does not write consent).

**What:** Preview count ≥ people who will receive. `ConsentedToMarketing` defaults false and checkout never sets it, so fan-out of a “successful” broadcast to a fresh tenant is **zero consenting recipients** after a non-zero preview. `RecordSent` before Resend. `FailedCount` stays 0. ADR 021: do not productize. Still a lying API.

---

