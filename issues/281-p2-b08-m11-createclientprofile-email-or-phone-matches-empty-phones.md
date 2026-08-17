---
number: "281"
id: B08-M11
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 281 — B08-M11 — CreateClientProfile `email OR phone` matches empty phones

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M11 — P2 — CreateClientProfile `email OR phone` matches empty phones

**Where:** `CreateClientProfileCommandHandler.cs` 25–28.

**What:** Latent. No production `new CreateClientProfileCommand`. Handler is live in the container. Empty phone ≡ every empty-phone row.

**Why it matters:** The next person who “just exposes CRM create” inherits a P0 merge. File it so they do not.

---

