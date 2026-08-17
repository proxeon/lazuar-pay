---
number: "330"
id: B10-X28
severity: P2
status: open
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 330 — B10-X28 — Honesty / docs residuals after `cbe17c2`

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X28 — P2 — Honesty / docs residuals after `cbe17c2`

- Scraper unresolved `MapPreview` / `MapReason` (noise).
- `docs/contracts/openapi-vs-minimal-api.md` §“Intentional frontend dark matter” still says ops invoicing / BillingProfile are **unrouted** (ADR 023). Ops `App.tsx` routes them. The contracts doc is a second SSoT that 023-erased itself.
- Combined spec now has M2M commerce (fixed). Product-scoped Scalar already had it. Clients committed in `cbe17c2` grew ~2k lines.
- Superadmin `/platform/*` TypeSpec still thin (doc residual; not a new bug).
- `CommerceWebhookEnvelope.event_type` union is still the five subscription names; cannot describe `order.completed` / `payment_link.paid`. Schema island.

