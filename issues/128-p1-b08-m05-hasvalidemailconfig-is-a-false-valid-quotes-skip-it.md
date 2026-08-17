---
number: "128"
id: B08-M05
severity: P1
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 128 — B08-M05 — HasValidEmailConfig is a false “valid”; quotes skip it

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M05 — P1 — HasValidEmailConfig is a false “valid”; quotes skip it

**Where:** `CommunicationsQueryService.HasValidEmailConfigAsync` 117–132; `InitiateCheckoutCommandHandler` 54–58; `CreateCustomCheckoutCommandHandler` (no call); `CreateProductCommandHandler` 43–48; `UpdateProductCommandHandler` 34–40; `ResendEmailService` 66–69.

**What:** Any active row with non-empty ciphertext + sender opens catalog checkout. Create-quote never asks. A revoked or undecryptable key still returns true. Hop-2 / receipt / dunning then throw no-fallback.

**Why it matters:** The gate’s job is “do not take money you cannot receipt.” It gates the presence of a row, not a working sender. Buyer pays; Official Receipt mail dies in Messaging; outbox retries; delivery log FAILED.

Silent `product.Archive()` on create without email config is a smaller sibling (returned id looks live).

---

