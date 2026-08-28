# K00 — Align and freeze

**Track:** Program  
**Analysis:** [`../11-what-next.md`](../11-what-next.md), [`../00-evaluation.md`](../00-evaluation.md)  
**Goal:** Lock Job A vs Job B so PRs cannot grow a second IdP or a Hub dispatcher.  
**No product code.**

**Why:** 020 reports are ~13k lines. Without a freeze, a PR will “helpfully” mint Pay `sk_live_`, import Hub outbound, or mix Stripe/One/Pay `whsec_`. This phase is the contract for every later checkbox.

**Related files (read, do not implement)**

| Path | Role |
|------|------|
| `plans/020-evals/11-what-next.md` | Two jobs |
| `plans/020-evals/00-evaluation.md` | Parent verdict |
| `plans/020-evals/checklist/decisions.md` | This freeze |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | Cathedral bans |
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | Live Map* list |

**Current (`6d730d15`):** Kernel doors absent. Occupancy 002 closed. IsolationTests already red on Hub tokens.

---

## K00.1 Scope of this program

- [x] Confirm **named program is Job A**: One `lzr_sk_` on Pay mint doors, `pay_url` on mint, Plane C `payment.completed`, `examples/pay-node`
- [x] Confirm Job B **cheap track H** may land in parallel (`/ready` bool, Production fail-boot)
- [x] Confirm Job B **track G** (persist-before-PSP, compose volume, captured One pause) does **not** gate K99a
- [x] Confirm occupancy lock is **off the front** of both queues
- [x] Confirm One repo is **not** required to change for M (Pay consumes live `lzr_sk_` mint)
- [x] Confirm Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum

## K00.2 Anti-goals (must stay refused)

- [x] No Pay-minted `sk_test_` / `sk_live_` / `pay_sk_` table
- [x] No Pay `users` / `members` / `organizations` table
- [x] No Zitadel PAT / OpenFGA admin / masterkey
- [x] No `DefaultRequestHeaders.Authorization` on `OneClient` from config
- [x] No MediatR, `IEnumerable<IHostedRail>`, `@repo/api-types-ts`, `Modules.One`
- [x] No project reference into `apps/lazuar-api`
- [x] No Hub `OutboundWebhookDispatcherJob` / `GatewayPaymentCompletedIntegrationEvent`
- [x] No HTTP to the merchant URL inside fulfill `SaveChanges`
- [x] No Standard Webhooks npm; no “we implement Standard Webhooks” while signing `{unix}.{body}` hex
- [x] No retarget of root `docker-compose.yml` onto 8081
- [x] No SST / LHDN / tax invoice title on the pay path
- [x] No refunds / subscriptions / pagination **in Job A** (parked P10–P14)
- [x] Do not flip [011/11](../../011-new-lazuar-pay/11-checklist.md) from this program except notes

## K00.3 Ports

- [x] One API **8080** + login **5175**
- [x] Pay **8081** + merchant **5178** + checkout **5179**
- [x] Hub `task dev` / root compose `lazuar-api` **off** while One owns 8080
- [x] Sample later on a port **not** 3002–3005 (product) and **not** Hub 3020 if that collides — pick in E11 (e.g. **3021**)

## K00.4 Freeze artifact

- [x] [`decisions.md`](./decisions.md) matches the table the team will implement
- [x] Key writer rule frozen: bound active key = Pay writer of that org; human JWT writer still owner/admin
- [x] Plane C dialect frozen: One split headers, full `whsec_` UTF-8
- [x] `pay_url` shape frozen: `{CheckoutBaseUrl}/c/{token}`

## K00.5 Exit

- [x] This checklist complete or amended in-place
- [x] Unblocked for M10, U10, W10, H10, D10
