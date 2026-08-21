# 013 — Production-ready new Pay, then replace the old tree

**Date:** 21 August 2026  
**Branch at analysis:** `feat/012-connect-one`  
**Type:** Uncondensed analysis. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells to `done`.  
**HEAD at analysis:** Pay `6f866ff0`. One `0f79fe4` (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`).

Ten subagents, ten papers. **Do not treat this index as the analysis.** Read the file.

**Problem:** how the new stack becomes something you can run in production and then **replace** the old Hub backend and frontends — without cloning the cathedral, without retargeting `lazuar-ops` / `lazuar-portal` at 8081, and without selling Hub feature-parity as the bar.

New stack (the thing that must become production-ready):

| Path | Role today |
|------|------------|
| `apps/lazuar-pay` | Focused C# host on **8081**. Whoami, org ready, in-memory checkout fixture. |
| `apps/lazuar-pay-merchant` | Vite **5178**. Health probe only. No OIDC. |
| `apps/lazuar-pay-checkout` | Vite **5179**. Health probe only. Buyers have no One account. |

Old stack (the thing to replace, not grow):

| Path | Role today |
|------|------------|
| `apps/lazuar-api` | Modular monolith Hub API on **8080** (collides with One). |
| `apps/lazuar-ops` | Merchant console **3003**, Hub cookie, Hub `/one/auth/*`. |
| `apps/lazuar-portal` | Buyer/checkout **3004**, Hub cookie. |
| `apps/lazuar-admin` | Hub staff UI **3005** (collides with One Login V2). Not a Pay merchant destination. |

Binding from [011](../011-new-lazuar-pay/README.md) and [012](../012-one-to-pay/README.md): Pay is Consumer-0 of One. Merchants are One humans. Buyers are not. One tenant id is Pay `org_id`. Wrap-rails. Receipt ≠ tax invoice. No homemade LHDN. No Pay password/IdP. No MediatR cathedral in the new host.

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [01-production-ready-bar.md](./01-production-ready-bar.md) | Production-ready bar | What “prod ready” means; refuse Hub parity |
| [02-replace-old-cutover.md](./02-replace-old-cutover.md) | Replace / cutover | Kill criteria for old API + UIs; dual-run |
| [03-host-production-seams.md](./03-host-production-seams.md) | Pay host seams | DB, config, secrets, health, deploy of `lazuar-pay` |
| [04-merchant-frontend.md](./04-merchant-frontend.md) | Merchant Vite | `:5178` OIDC, whoami, steal judgment not ops routes |
| [05-checkout-frontend.md](./05-checkout-frontend.md) | Checkout Vite | `:5179` hosted pay; no Zitadel |
| [06-money-rails.md](./06-money-rails.md) | Gateways | BYOK, Stripe/CHIP, webhooks, wrap-rails |
| [07-fulfillment-ledger-docs.md](./07-fulfillment-ledger-docs.md) | Fulfillment | Same-handler journal + `RCPT-`; SST judgment |
| [08-one-identity-production.md](./08-one-identity-production.md) | One in prod | SPA, `lzr_sk_`, HMAC, `tenant.suspended` |
| [09-data-migration.md](./09-data-migration.md) | Data | What to migrate vs greenfield; no second org table |
| [10-ci-observability-decommission.md](./10-ci-observability-decommission.md) | CI / ops / kill | Tests, staging, compose, when Hub goes dark |

Implementation of these papers is a later program (checklists, not this folder). Analyses `01`–`10` stay the evidence; do not condense them into an index or a mega-PR plan in this README.
