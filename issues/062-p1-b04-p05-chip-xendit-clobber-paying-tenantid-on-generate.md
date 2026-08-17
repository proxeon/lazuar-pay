---
number: "062"
id: B04-P05
severity: P1
status: open
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 062 — B04-P05 — CHIP / Xendit clobber paying `tenant_id` on generate

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P05 — P1 — CHIP / Xendit clobber paying `tenant_id` on generate

**Where.** `ChipCollectGatewayAdapter.cs:51`; `XenditGatewayAdapter.cs:185`. Contrast `StripeGatewayAdapter.ApplyPayingTenantMetadata` (`427-438`) and Billplz which **reads** `tenant_id` (`73-75`).

**What.** `GenerateSystemCheckoutSessionQueryHandler` passes `PlatformCheckoutTypes.SystemOrganizationId` as the adapter tenant and puts the paying workspace in metadata (`44-59`). System-org CHIP/Xendit checkout overwrites that to the system guid. Webhook metadata `tenant_id` then names the platform, not the workspace that must be activated. Stripe tests explicitly lock the opposite behaviour (`CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant`). There is **no** CHIP/Xendit twin of that test.

Razorpay generate does not set `tenant_id` itself; notes are the incoming dictionary. Off-session Razorpay **does** set `tenant_id` to the adapter tenant (`221`) — dead while capability is false.

