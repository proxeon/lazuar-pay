---
number: "063"
id: B04-P06
severity: P1
status: resolved
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
resolved_branch: fix/063-webhook-tenant-eventid
---

# 063 — B04-P06 — No inbound `tenant_id` vs URL tenant check; EventId unique is not tenant-scoped

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/063-webhook-tenant-eventid`
- **Follow-up:** `fix/063-platform-webhook-paying-tenant` (allow paying `tenant_id` when `platform_tenant_id` is the system-org URL)

Inbound `tenant_id` must match the URL tenant, except platform checkout: paying `tenant_id` is allowed when `platform_tenant_id` equals the system-org URL. Webhook logs are unique per `(OrganizationId, Provider, EventId)`.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P06 — P1 — No inbound `tenant_id` vs URL tenant check; EventId unique is not tenant-scoped

**Where.** `ProcessGatewayWebhookCommandHandler` publishes `OrganizationId: request.TenantId` (`170-208`). Merge only **fills** missing `tenant_id` (`Metadata.cs:56-59`). `GetByEventId` / `GetByBusinessKey` ignore tenant (`PaymentRepositories.cs:48-65`). Unique indexes are `(Provider, EventId)` (`PaymentConfigurations.cs:30`).

**What.** Two tenants sharing a CHIP brand (same PEM) or a Xendit callback token:

- Replay of tenant A’s body to tenant B’s URL verifies (same secret). If A already logged the EventId, B hits the existing log (`HandleExistingLogAsync`) and may requeue **A’s** outbox or skip. B does not fulfill; A already did — or B stole the first processing slot and A’s later delivery is treated as a duplicate.
- If EventIds are globally unique per provider object (usually true), the second tenant cannot insert a second log for the same object. Shared-account multi-tenant is a first-writer-wins race, not isolation.

Stripe `evt_` ids are globally unique; the practical risk is CHIP/Xendit shared credentials.

