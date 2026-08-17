---
number: "131"
id: B08-M08
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 131 — B08-M08 — Digital Product Delivery fires for every one-time order and lies about the file

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M08 — P1 — Digital Product Delivery fires for every one-time order and lies about the file

**Where:** `OrderCompletedDigitalDeliveryHandler.cs` 15–85; wiki `CommunicationsQueryService.cs` 109; catalog `DefaultMessageTemplates.cs` 43–50.

**What:** No digital-asset check. `plan_name` is the literal `"your purchase"`. `fulfillment_url` and `portal_magic_link` are the same portal home URL, no 24h token. Wiki: “Cloudflare R2 Download Link” and “24-hour auto-login.”

**Why it matters:** A one-time consulting SKU emails “Your download is ready.” The button is a logged-out portal. There is no test file.

Subscription activations do **not** take this path (they use Portal Access). Do not file a renewal double-mail that does not exist.

---

