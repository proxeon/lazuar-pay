# 019 — Evaluate newest Lazuar Pay (018 merchant shell): bugs and gaps

**Date:** 26 August 2026  
**Branch:** `feat/018-merchant-shell`  
**HEAD at analysis start:** `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome`  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`.

Parent judgment: [00-evaluation.md](./00-evaluation.md) (written after `01`–`10`). Evidence: the ten uncondensed reports (~11,900 lines). **Do not treat this index or the parent as a substitute for those reports.** Read the file. Line counts at write time:

| File | Lines |
|------|------:|
| [00-evaluation.md](./00-evaluation.md) | parent |
| [01-pay-host-seams.md](./01-pay-host-seams.md) | 1368 |
| [02-merchant-frontend.md](./02-merchant-frontend.md) | 1491 |
| [03-checkout-frontend.md](./03-checkout-frontend.md) | 1488 |
| [04-processors-vault-test.md](./04-processors-vault-test.md) | 1023 |
| [05-payment-links-occupancy.md](./05-payment-links-occupancy.md) | 1273 |
| [06-rails-webhooks-fulfillment.md](./06-rails-webhooks-fulfillment.md) | 1039 |
| [07-identity-authz-cors.md](./07-identity-authz-cors.md) | 1056 |
| [08-contracts-spec-honesty.md](./08-contracts-spec-honesty.md) | 1262 |
| [09-tests-inventory.md](./09-tests-inventory.md) | 966 |
| [10-honesty-bugs-gaps.md](./10-honesty-bugs-gaps.md) | 921 |

**Problem:** where the new stack actually is after 017 folder-by-job layout and 018 merchant-shell work (Aura chrome, independent processor vault, Test rail, pay-link capacity, restyled buyer page), and which **bugs / gaps** are live in source. Live files on this SHA are authority. [014](../014-evals/README.md), [016](../016-adapters-check/README.md), and [018-evals](../018-evals/001-evals.md) are historical / product papers; if they disagree with live files, live files win.

New stack (the thing under evaluation):

| Path | Role today (re-check in the reports; this row is a pointer) |
|------|--------------------------------------------------------------|
| `apps/lazuar-pay` | Focused C# host on **8081**. One façade, Postgres on 5435, six hosted rails including local **Test**, independent processor vault, pay-link occupancy. |
| `apps/lazuar-pay-merchant` | Vite **5178**. Staff shell (Aura chrome). One OIDC. Not `lazuar-ops`. |
| `apps/lazuar-pay-checkout` | Vite **5179**. Hosted buyer page. Buyers have no One account. Not `lazuar-portal`. |

018 delta vs 016 (map, not proof — reports re-verify):

- 017: folders by job (`Credentials/`, `Rails/`, `Webhooks/`, `PublicPay/`, `Identity/`, `PaymentLinks/`), not a Gateways dump
- Aura-style merchant shell; last workspace after login; staff email in sidebar
- Vault processors independently; bind rail at mint
- Local Test processor with no secrets
- Processor keys in an Edit dialog; always offer Test when minting
- Pay links as a table; mint from a dialog
- Capacity: how many people can pay a pay link
- Buyer checkout restyled with aura-ui chrome
- Payments / receipts tables matched to pay-link chrome
- Keep local Postgres password when loading `.env`; apply four-adapter columns on Development start

| File | Subagent | Assigned slice |
|------|----------|----------------|
| [00-evaluation.md](./00-evaluation.md) | Parent (orchestrator) | Verdict, P0s, how to solve, next ten |
| [01-pay-host-seams.md](./01-pay-host-seams.md) | Pay host seams | `Program.cs`, schema, mint doors, layout |
| [02-merchant-frontend.md](./02-merchant-frontend.md) | Merchant Vite | `:5178` Aura shell, vault UI, tables |
| [03-checkout-frontend.md](./03-checkout-frontend.md) | Checkout Vite | `:5179` restyle, poll, occupancy UX |
| [04-processors-vault-test.md](./04-processors-vault-test.md) | Processors | Independent vault, bind-at-mint, Test rail |
| [05-payment-links-occupancy.md](./05-payment-links-occupancy.md) | Pay links | Capacity / occupancy races |
| [06-rails-webhooks-fulfillment.md](./06-rails-webhooks-fulfillment.md) | Rails + Plane B | Six rails, webhook TX, Official Receipt |
| [07-identity-authz-cors.md](./07-identity-authz-cors.md) | Identity | One gates, OIDC, CORS, staff session |
| [08-contracts-spec-honesty.md](./08-contracts-spec-honesty.md) | Contracts | `pay-spec` vs live doors vs SPA |
| [09-tests-inventory.md](./09-tests-inventory.md) | Tests | What each method locks; one method per hole |
| [10-honesty-bugs-gaps.md](./10-honesty-bugs-gaps.md) | Honesty | Ranked bugs/gaps; refuse; sequence |

Write uncondensed. Do not summarize a report into a bullet list and delete the evidence.
