# 012 — Connect Lazuar One to new Pay

**Date:** 20 August 2026  
**Branch:** `feat/012-one-to-pay`  
**Type:** Uncondensed analysis. **Not** an implementation of `apps/lazuar-pay`.  
**HEAD at analysis:** Pay `6ca8f19f`. One `0f79fe4` (`/Users/akmalfirdaus/Code/lazuar/lazuar-one`).

Ten subagents, ten papers. **Do not treat this index as the analysis.** Read the file.

Binding from [011](../011-new-lazuar-pay/README.md): Pay is Consumer-0. Merchants are One humans. Buyers are not. First connection is **HTTP trust** (`GET /v1/whoami` → One `GET /me`), not stubbing `/one/auth/login` on 8081, not pointing ops at the new host.

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [01-one-http-surface.md](./01-one-http-surface.md) | One HTTP surface | Routes Pay must call / never call |
| [02-one-authn-tokens.md](./02-one-authn-tokens.md) | AuthN tokens and ports | Bearer, PAT split, 8080 collision |
| [03-pay-host-seams.md](./03-pay-host-seams.md) | Pay host seams | Where whoami fits in `apps/lazuar-pay` |
| [04-pay-spec-contract.md](./04-pay-spec-contract.md) | TypeSpec | `pay-spec` vs One spec vs old `api-spec` |
| [05-local-topology.md](./05-local-topology.md) | Local topology | How to boot One + Pay 8081 together |
| [06-tenant-org.md](./06-tenant-org.md) | Tenant / org | One tenant id **is** Pay `org_id` |
| [07-authz-roles.md](./07-authz-roles.md) | Authz and roles | `authz/check`, VIEWER honesty gap |
| [08-machine-keys.md](./08-machine-keys.md) | Machine keys | `lzr_sk_`, scopes, JWT vs key |
| [09-webhooks-events.md](./09-webhooks-events.md) | Webhooks | HMAC later; skip for first connect |
| [10-dogfood-and-tests.md](./10-dogfood-and-tests.md) | Dogfood and tests | Pass/fail, sequence, anti-goals |

Implementation of whoami / `authz/check` is a later step on this branch (or a follow-up). Do not flip [011/12](../011-new-lazuar-pay/12-first-slice-tracker.md) cells to `done` from the analysis papers.

**Implement against:** [checklists/](./checklists/README.md) (small phases, one intent each). Freeze: [checklists/decisions.md](./checklists/decisions.md). Analyses `01`–`10` stay the evidence; do not condense them into the checklists.

**Connected (C99) on `feat/012-connect-one`:** `GET /v1/whoami` and `GET /v1/orgs/{orgId}/ready` on 8081. SPA, machine keys, webhooks, money, and ops-on-8081 stay parked (P10–P60).