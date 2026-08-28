# 020 — Production-ready Pay: second-app integration gaps

**Date:** 28 August 2026  
**Branch:** `fix/002-pay-host-bugs`  
**HEAD at analysis start:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`.

Parent judgment: [00-evaluation.md](./00-evaluation.md) (written after `01`–`10`). Evidence: the ten uncondensed reports (~12,700 lines). **Do not treat this index or the parent as a substitute for those reports.** Read the file. Line counts at write time:

| File | Lines |
|------|------:|
| [00-evaluation.md](./00-evaluation.md) | 231 |
| [01-public-http-api.md](./01-public-http-api.md) | 1155 |
| [02-machine-keys-m2m.md](./02-machine-keys-m2m.md) | 1227 |
| [03-outbound-webhooks.md](./03-outbound-webhooks.md) | 1525 |
| [04-inbound-webhooks.md](./04-inbound-webhooks.md) | 1231 |
| [05-identity-authz-tenancy.md](./05-identity-authz-tenancy.md) | 1204 |
| [06-host-production.md](./06-host-production.md) | 1315 |
| [07-money-remaining.md](./07-money-remaining.md) | 1455 |
| [08-headless-vs-spa.md](./08-headless-vs-spa.md) | 1209 |
| [09-spec-docs-sample.md](./09-spec-docs-sample.md) | 1232 |
| [10-honesty-production-bar.md](./10-honesty-production-bar.md) | 1184 |

**Problem:** [019-evals](../019-evals/README.md) audited the 018 merchant-shell **hosted cashier** and extracted [issues/002](../../issues/002/README.md) (001–080, now resolved on this SHA). That work does **not** make Pay a kernel other products can swallow. 019’s parent already said there is still no machine key and no outbound `payment.completed`. This program re-reads **live files on this SHA** and asks:

1. What still blocks **production** of first-party dogfood (One + Pay merchant + Pay checkout)?
2. What is missing so **another app** can integrate without cloning this repo — secret key / M2M, outbound webhooks, a clean `/v1` they can call, docs/sample?
3. Which of those are bugs vs missing features vs refuse?

Live files on this SHA are authority. [012-one-to-pay](../012-one-to-pay/README.md) (machine keys, One webhooks), [013-prods](../013-prods/README.md) (production bar), [006-sample](../006-sample/README.md) (Hub second-app, museum), [011-new-lazuar-pay/08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md), and 019 are historical / product papers. If they disagree with live files, live files win; the reports name the disagreement.

New stack (the thing under evaluation):

| Path | Role today (re-check in the reports; this row is a pointer) |
|------|--------------------------------------------------------------|
| `apps/lazuar-pay` | Focused C# host on **8081**. One façade, Postgres on 5435, six hosted rails including Test, independent processor vault, pay-link occupancy, per-org One `whsec_`. |
| `apps/lazuar-pay-merchant` | Vite **5178**. Staff shell. One OIDC. Not `lazuar-ops`. |
| `apps/lazuar-pay-checkout` | Vite **5179**. Hosted buyer page. Buyers have no One account. Not `lazuar-portal`. |
| Sibling `lazuar-one` | Identity. `/api/v1` on **8080**. Mints `lzr_sk_`. Outbound tenant webhooks. Pay must not copy `Modules/One`. |

002 closed occupancy races, Plane B HMAC, CORS, spec honesty, and One inbound per-org secret **as a hosted cashier**. Kernel doors (M2M Bearer that is not a human JWT, Pay→app events, a second-app sample) were **out of 002**.

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [00-evaluation.md](./00-evaluation.md) | Parent (orchestrator) | Verdict, production bar, how to solve, next ten. Written after 01–10. |
| [01-public-http-api.md](./01-public-http-api.md) | Public HTTP API | Clean `/v1` for a stranger: doors, errors, idempotency, versioning |
| [02-machine-keys-m2m.md](./02-machine-keys-m2m.md) | Secret keys / M2M | `lzr_sk_` vs Pay homemade keys vs staff JWT; scopes |
| [03-outbound-webhooks.md](./03-outbound-webhooks.md) | Pay → app events | Plane C: `payment.completed` and friends; signing; retries |
| [04-inbound-webhooks.md](./04-inbound-webhooks.md) | Inbound planes | Plane A One→Pay and Plane B PSP→Pay, ops, secrets |
| [05-identity-authz-tenancy.md](./05-identity-authz-tenancy.md) | Identity / tenancy | MemberGate, writer, One coupling vs standalone Pay |
| [06-host-production.md](./06-host-production.md) | Host production | Compose, images, CORS, WrapKey, rate limit, health, obs |
| [07-money-remaining.md](./07-money-remaining.md) | Money leftover | Occupancy/fulfill after 002; refunds, disputes, subscriptions |
| [08-headless-vs-spa.md](./08-headless-vs-spa.md) | Headless vs SPA | Merchant/checkout as clients of `/v1` vs API-only integrator |
| [09-spec-docs-sample.md](./09-spec-docs-sample.md) | Spec / docs / sample | `pay-spec`, honesty, SDK, second-app example |
| [10-honesty-production-bar.md](./10-honesty-production-bar.md) | Honesty | Ranked bugs / missing feats / refuse; production-ready bar |
| [11-what-next.md](./11-what-next.md) | Direction | Shared picture of the next program. Not tickets. |
| [checklist/](./checklist/README.md) | Implementation | Phase-by-phase checklists (K00, M, U, W, H, E, D, G, parked, K99a/b). |

Write uncondensed. Do not summarize a report into a bullet list and delete the evidence.

Standing law the ten reports must not weaken:

- One Pay binary, one Pay database. Bezos is the **door** (`/v1`); Linux is the **room** (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path.
- Steal HTTP **judgment** from Hub; Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`).
