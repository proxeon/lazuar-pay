---
number: "132"
id: B08-M09
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 132 — B08-M09 — Empty `Jwt:Secret` is a working HMAC key on unsubscribe

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M09 — P1 — Empty `Jwt:Secret` is a working HMAC key on unsubscribe

**Where:** `PublicComplianceEndpoints.cs` 49, 77; `BroadcastFanoutJob.cs` 152; `appsettings.json` 23–24; contrast `DocumentLinkSigner.ResolveSecret` 57–60.

**What:** `??` leaves `""` in place. Forged `sig = hex(HMAC-SHA256("", "{org}:{email}"))` unsubscribes anyone. Document links on the same process fall back to the 32-char dev string.

**Why it matters:** Marketing-lane only (receipts survive). Still a one-click unsub of a competitor’s list if org ids leak (they are in every unsubscribe URL and every Resend `org` tag).

`FixedTimeEquals` length mismatch → 500 is B08-M20.

---

