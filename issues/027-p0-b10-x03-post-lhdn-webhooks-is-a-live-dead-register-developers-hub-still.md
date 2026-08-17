---
number: "027"
id: B10-X03
severity: P0
status: resolved
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
resolved_branch: fix/027-lhdn-webhooks-dual-write
---

# 027 — B10-X03 — `POST /lhdn/webhooks` is a live dead register; Developers hub still teaches it

- **Severity:** P0
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/027-lhdn-webhooks-dual-write`

`POST /lhdn/webhooks` dual-writes a workspace endpoint for `invoice.valid` / `invoice.invalid`. Developers hub teaches `POST /one/workspaces/{id}/webhooks` and `t=,v1=` signing.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X03 — P0 — `POST /lhdn/webhooks` is a live dead register; Developers hub still teaches it

**Files:** `Modules/Lhdn/Application/Commands/WebhookCommands.cs` 26–32; `Modules/Lhdn/Application/Queries/LhdnQueries.cs` 129–136; `apps/lazuar-developers/app/webhooks/page.tsx` 223–258.

Register persists `lhdn.WebhookSubscriptions` (url + secret, **no events column**). List invents `Events = ["invoice.valid", "invoice.invalid"]`. Dispatch does **not** read that table. Runtime LHDN deliveries are `OutboundWebhookRequestedIntegrationEvent` → One `TenantWebhookEndpoints` → `t=,v1=` envelope.

The Developers hub still says:

- register via `POST /lhdn/webhooks`
- emit JSON with top-level `event`
- “LHDN path currently signs with HMAC-SHA256 hex of the raw body”

All three sentences are false after R43. An ERP that follows Scalar LHDN / Kiota `WebhooksRequestBuilder` / that page will persist a row that never receives a delivery.

TypeSpec still documents `/lhdn/webhooks` as a first-class product route (`lhdn/routes.tsp`). Honesty is **green** because the Maps exist. Honesty does not ask “does anyone read the table.”

008 H1. Still present. `cbe17c2` did not touch this.

