# 10 — Honesty: production-ready bar + second-app kernel door

**Date:** 28 August 2026  
**Program:** 020-evals  
**Slice:** ranked honesty paper (not the parent). Parent [00-evaluation.md](./00-evaluation.md) is written **after** `01`–`10`.  
**Branch:** `fix/002-pay-host-bugs`  
**HEAD:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**Full SHA:** `6d730d155c871465c35c192cf7730bfd270b47fa`  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`.

**Sibling reports 01–09 and parent 00 were not on disk yet** when this file was written (`plans/020-evals/` contained only `README.md` plus this file). Disagreements in §8 are **predicted** from the index assignment and from live files, not observed against those papers.

Live files on **this SHA** are authority. [013-prods/01-production-ready-bar.md](../013-prods/01-production-ready-bar.md) defined “production-ready” on `6f866ff0` (21 Aug 2026). [019-evals](../019-evals/README.md) audited the 018 hosted cashier on `9f04ad58` (26 Aug 2026) and extracted [issues/002](../../issues/002/README.md) 001–080. 002 YAML says **resolved** on this branch. This paper re-reads **code and tests**, not YAML.

```text
6d730d15 fix(pay): store per-org One webhook secrets
1974ac10 fix(pay): add Pay images without retargeting Hub compose
713a399b fix(pay): close leftover host honesty holes
9e5fa8e6 fix(pay-spec): align TypeSpec with live Pay /v1 doors
e422c0be fix(pay): rate-limit public start per pay token
ad7e05d7 fix(pay): serialize pay-link occupancy and expire abandoned seats
23227d22 fix(pay): close Test/Stripe/One webhook holes and unique fulfill
```

---

## 0. Verdict (read this before the tables)

Pay on `6d730d15` is a **hosted cashier for One workspaces**. It is **not** a payments API platform. It is **not** production-ready under the 013 sentence, and it is **not** a kernel a second app can swallow in an afternoon.

Two bars must not be collapsed:

| Bar | Pass sentence | Status on this SHA | May we call it that word? |
|-----|---------------|--------------------|---------------------------|
| **013 Bar B — first-party dogfood** | Merchant signs in through One, pastes CHIP or Stripe keys, a buyer pays on `:5179` without a One account, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, an invited MEMBER can see ops, a VIEWER cannot charge. | **Partial.** Money path exists in hermetic tests and in source. The **human loop** (011/12 steps 1–12) is still `todo` in the tracker and still missing invite chrome, a proven live One `tenant.suspended` envelope, production CORS/WrapKey, and a named Malaysian dogfood rail. | **No.** Do not say production-ready. |
| **020 kernel door — second app** | A stranger mints `lzr_sk_`, `POST /v1/checkouts`, starts pay or hands the buyer a URL, and learns `payment.completed` without cloning this repo. | **Fail.** No machine-key scheme on 8081. No outbound Pay→app event. Hub sample `examples/hub-cashier-next` talks to **museum** 8080. | **No.** Do not say platform / Stripe-shaped kernel. |

002 closed the 019 **cash P0s of a hosted cashier** (occupancy race, Test unsigned Plane B, Stripe unpaid completed, unique charge/`RCPT-`, One HMAC **dialect** in Pay source, CHIP join via `HostedSessionId` fallback). Those P0s are **not** the kernel door. 019’s parent already said that. Live files still say that.

What we may honestly say, and what we must not, is §9. Ranked leftover is §5. Refuse is §6. Sequence splits in §7 because **other-app integration** and **first-party go-live** want different first tickets.

---

## 1. Method — what was opened, what was not

Nothing was implemented. Counts and claims below are from this SHA unless labelled historical.

### 1.1 Plans (consumer intent)

- `plans/020-evals/README.md` — this program’s index. Kernel doors named: machine key, outbound `payment.completed`. 002 out of scope for those doors.
- `plans/013-prods/01-production-ready-bar.md` — the **bar**. SHA `6f866ff0`, branch `feat/012-connect-one`. Bar A connected / Bar B dogfood sentence / Bar C product v1. Hub parity is the failure mode.
- `plans/013-prods/03-host-production-seams.md` — process seams on the same old SHA: no Dockerfile, in-memory checkout, laptop CORS.
- `plans/019-evals/00-evaluation.md` — P0 occupancy, Test unsigned, One HMAC disagreement 07 vs 10.
- `plans/019-evals/10-honesty-bugs-gaps.md` — cited via parent; not re-quoted as live.
- `plans/018-evals/001-evals.md` — kernel vs escrow vs WhatsApp SME. Kernel idea strong; door absent.
- `plans/012-one-to-pay/08-machine-keys.md` and `checklists/p20-machine-key.md` — `lzr_sk_` is **One’s** mint. P20 still unchecked.
- `plans/012-one-to-pay/checklists/p30-one-webhooks.md` — not re-opened as a flip; inbound One HMAC **is** live on 8081 now.
- `plans/011-new-lazuar-pay/11-checklist.md` and `12-first-slice-tracker.md` — Status cells **frozen**. Live code has outrun them. Do not treat `todo` as “not in source.”
- `plans/006-sample/README.md` — Hub second-app sample. Museum.
- `plans/011-new-lazuar-pay/08-bezos-door.md` — `/v1` is the door; Linux is the room. Still binding.

### 1.2 Issues (YAML vs body)

- `issues/002/README.md` — 001–080 listed, “resolved on `fix/002-pay-host-bugs`.”
- Bodies of 001, 006, 011, 014 (and the index). YAML `status: resolved`; markdown heading still says **Status: open**. That is a docs miss, not a source miss.

### 1.3 Live Pay host

Opened in full or in the cited ranges:

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `PaymentLinks/PaymentLinkOccupancy.cs`, `PublicPay/PublicPayEndpoints.cs`, `PublicPayLimiter.cs`
- `Rails/PayProviders.cs`, `Rails/Test/TestWebhook.cs`, `Rails/Stripe/StripeWebhook.cs`, `Rails/Stripe/StripeHosted.cs`, `Rails/Chip/ChipHosted.cs`, `Rails/Chip/ChipWebhook.cs`, `Rails/Xendit/XenditWebhook.cs`
- `Identity/OneWebhooks/OneWebhookSignature.cs`, `OneWebhookEndpoints.cs`
- `Identity/Client/Bearer.cs`, `OneClient.cs`, `MemberGate.cs`
- `Identity/WhoamiEndpoints.cs`, `OrgReadyEndpoints.cs`
- `Money/Fulfillment.cs`, `Money/Queries/PaymentQueryEndpoints.cs`
- `Webhooks/WebhookEndpoints.cs`
- `Data/PayDbContext.cs`
- `Hosting/PayCors.cs`, `HealthEndpoints.cs`
- `Secrets/SecretBox.cs`
- `Checkouts/CheckoutEndpoints.cs`, `PaymentLinks/PaymentLinkEndpoints.cs`
- `Dockerfile`, `docker-compose.pay.yml`, `README.md`, `.env.example`, `appsettings.json`

### 1.4 Live tests

- `IsolationTests.cs`
- `PaymentLinks/PaymentLinkTests.cs` (concurrent start, occupancy copy)
- `Webhooks/PostgresTxTests.cs`, `FillTests.cs`, `WebhookTests.cs`
- `Rails/Test/TestRailTests.cs`
- `Identity/OneWebhookTests.cs`
- `Infrastructure/PayPostgres.cs`

### 1.5 Live UIs + spec

- `apps/lazuar-pay-merchant` — OIDC, occupancy copy, processors, nav, `oneApi.ts` (create tenant only), `roles.ts`, `package.json`
- `apps/lazuar-pay-checkout` — `pay.ts`, `App.tsx`, `package.json` (no oidc)
- `packages/pay-spec/main.tsp`, `README.md`

### 1.6 Museum (contrast only)

- Root `docker-compose.yml` still Hub 8080 / ops 3003 / portal 3004.
- `examples/hub-cashier-next` still Bearer `sk_` + `payment.completed` against Hub.
- Hub `Modules/One` outbound signer (combined `t=,v1=`) is **not** the IdP Pay’s README points at.

### 1.7 Commands run

| Command | Result |
|---------|--------|
| `git rev-parse HEAD` | `6d730d155c871465c35c192cf7730bfd270b47fa` |
| `git branch --show-current` | `fix/002-pay-host-bugs` |
| `git log -1 --oneline` | `6d730d15 fix(pay): store per-org One webhook secrets` |
| `git log --oneline -40` | 002 cash/UI/spec/image commits listed in the header |
| `git status -sb` | branch tracks origin; `?? plans/020-evals/` |

This paper did **not** boot One, Pay, Stripe, or CHIP. It did **not** run `task pay:test`. Runtime claims are from source + tests. Where a test is hermetic (`FakeOneHandler`, InMemory, or Testcontainers that `Assert.Ignore` if Docker is down), it does **not** prove the live One wire or a live PSP.

### 1.8 What we cannot pretend

- A live `lazuar-one` `tenant.suspended` POST captured in this repo against 8081.
- One staging PASSED.
- A stranger following Hub docs and hitting 8081.
- Multi-replica occupancy under two Pay processes without Postgres (SemaphoreSlim is process-local; `FOR UPDATE` is Npgsql-only).
- SST, refunds, subscriptions as a product, outbound Pay events.

---

## 2. What “production ready” meant in 013 vs live this SHA

013/01 (`6f866ff0`, 21 Aug 2026) locked a sentence and forbade Hub parity:

> A merchant signs in through **One**, opens Pay, pastes CHIP or Stripe keys, a **buyer pays on the hosted page without a One account**, Pay shows one `RCPT-` and a balanced journal, a webhook retry no-ops, a One-invited MEMBER can see ops and a VIEWER cannot charge.

It also locked three bars. **Bar A (connected)** was already Pass on that SHA. **Bar B** was the only sentence allowed to be called production-ready. **Bar C** (renew, refund, SST, magic-link portal) was explicitly **not** the gate.

The host on that SHA: health, whoami, dummy `/ready`, **in-memory** checkout fixture. Merchant and checkout Vite apps were **health probes**. No Postgres, no Dockerfile, no rails, no receipt, no OIDC.

Live `6d730d15` is a different animal. The **definition** of Bar B has not changed. The **inventory** has. Collapsing “we shipped money” into “production-ready” is the same class of lie 008 caught in Hub READMEs.

### 2.1 Three-process map

| Process | 013 SHA `6f866ff0` | This SHA `6d730d15` |
|---------|--------------------|---------------------|
| `apps/lazuar-pay` :8081 | Health, whoami, dummy ready, in-memory checkout | One façade, Postgres 5435, six hosted rails including Test, independent vault, pay-link occupancy, Plane B, Official Receipt, per-org One `whsec_`, Dockerfile |
| `apps/lazuar-pay-merchant` :5178 | Health probe of 8081. No OIDC. | Aura shell, `react-oidc-context` PKCE, create tenant via One HTTP, processor cards, pay-link table, payments, receipts. Not ops :3003. |
| `apps/lazuar-pay-checkout` :5179 | Health probe. Copy says no One account. Does not take a card. | Hosted buyer page, `slot_key`, poll `?status=verifying`, no oidc in `package.json`. Not portal :3004. |

### 2.2 Bar B MUST table — 013 item → live / missing / refuse

Status column:

- **live** — implemented in focused Pay (or the named Vite app) on this SHA, with a test or an obvious handler. Not “a human has dogfooded it.”
- **partial** — source exists, hole remains, or the 013 job is only half-done.
- **missing** — not in focused Pay.
- **refuse** — 013 said never, and live still must not grow it.
- **ops** — live code cannot close it; an operator / One registration / live envelope must.

013 IDs that 013 itself marked **should / Bar C / later** are listed so nobody “fixes” Bar B by shipping them.

#### 2.2.1 One façade (NP-ONE)

| 013 bar item | 013 gate | Live this SHA | Tag |
|--------------|----------|---------------|-----|
| NP-ONE-001 Register Pay SPA via One `POST /tenants/{id}/apps` | MUST Bar B | Merchant has `client_id` **env**. No Pay code registers the app. 011/12 step 1 still `todo`. | **ops** / missing-feat (One-side) |
| NP-ONE-002 OIDC code + PKCE | MUST | `apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`: code, PKCE via `oidc-client-ts`, sessionStorage, silent-renew.html. Empty `VITE_ZITADEL_CLIENT_ID` is a boot-time nothing. | **live** (laptop) / **ops** (real `client_id`) |
| NP-ONE-003 access_token as Bearer, never id_token | MUST stay | `bearerToken.ts` `pickApiBearerToken` only JWT access_token. Whoami forwards Bearer. Isolation of id_token is a vitest. | **live** |
| NP-ONE-004 Redirects + REDIRECT_ALLOWLIST | MUST | Env defaults `:5178/callback`. Allowlist is **One**. Not in Pay. | **ops** |
| NP-ONE-005 Login `:5175`; never `:3005` / `:5173` | MUST | Merchant `LoginPage` `signinRedirect` to Zitadel authority (One login in the real topology). Nav has no admin port. Unproven as a human redirect matrix. | **partial** (code intent live; 011/12 step 2 not flipped) |
| NP-ONE-006 whoami → One `/me` once | MUST stay | `WhoamiEndpoints` + `OneClient.GetWhoamiAsync`. Health never calls One (`HealthTests`). | **live** |
| NP-ONE-007 Path `{orgId}` SoT | MUST stay | MemberGate uses path org. Header is hint only. | **live** |
| NP-ONE-008 Roles from `/me`, not Zitadel project roles | MUST stay | `OneMeMapper` copies One `role`. No claim parse. | **live** |
| NP-ONE-009 Create workspace = `POST /tenants` | MUST | Merchant `oneApi.ts` `createTenant` → One `/tenants`. IsolationTests ban `ToTable("organizations")`. | **live** |
| NP-ONE-010 PATCH tenant profile | should | No Pay door. | missing-feat (not Bar B) |
| NP-ONE-011/012 Invite + non-email accept | MUST | **No** `invite` string in merchant `src/`. Second engineer must use One / `lazuar-app`. | **missing-feat** |
| NP-ONE-013 Roster chrome | should | No roster in `:5178`. | missing-feat (not Bar B) |
| NP-ONE-014 Mint `lzr_sk_` explicit scopes | MUST in 013 step 5 | One mints (012/08). **Pay source has zero `lzr_sk_`**. Bearer is forwarded blindly to One `/me` / `authz/check`. No Pay scopes, no Pay key table (good), no Pay auth that *is* a machine key. | **missing-feat** (kernel) / 013 dogfood of mint is **ops** on One |
| NP-ONE-015 `authz/check member` before admin | MUST stay | `MemberGate.RequireMemberAsync` posts `relation=member`, `type=tenant`. | **live** |
| NP-ONE-016 batch-check chrome | should | Absent. | missing-feat |
| NP-ONE-017 HMAC `tenant.suspended` / `reactivated` | MUST | `POST /v1/one/webhooks` verifies HMAC; sets `ChargesPaused`. Per-org `PUT /v1/orgs/{orgId}/one-webhook`. Pay does **not** register the URL with One. | **live** (Pay receiver) / **ops** (One subscription + secret paste) |
| NP-ONE-018 Stop charges on suspended | MUST | Public start 403 when `ChargesPaused`. Writer 403 on suspended tenant status. Pause expires open reservations. **Live wire** still needs a real One envelope (see §3.3). | **partial** |
| NP-ONE-019 provision on `tenant.created` | should | Not handled (only suspend/reactivate). | missing-feat |
| NP-ONE-020 secrets inventory | MUST as refuse of PAT | `.env.example`: One BaseUrl, WrapKey, OneWebhookSecret, BYOK wrap. README: never Zitadel PAT. No PAT in Pay src. **Does not hold `lzr_sk_`.** Process `Pay:OneWebhookSecret` is a one-shop **god-key fallback**. | **partial** (PAT refuse live; god-key leftover; no machine key) |
| NP-ONE-021 VIEWER cannot charge / keys / refund | MUST | One has no VIEWER. Pay: `RequireWriterAsync` requires `owner`/`admin`; `member` 403 `"Writer role required"`. Tests: `Member_cannot_create_checkout`, `Member_cannot_create_payment_link`, `Member_cannot_put_one_webhook_secret`. Refunds **do not exist**, so “cannot refund” is vacuously true. Writer is `/me` role overlay, **not** `authz/check admin`. | **partial** (write gate live; 013 asked stricter authz; refunds absent) |
| NP-ONE-022 invited MEMBER can see ops | MUST | Member can GET lists if they have a Bearer and membership. **No invite in merchant.** Second engineer is not a Pay UX. | **missing-feat** (invite) / live (member GET if they already exist) |

#### 2.2.2 Catalog, checkout, buyer

| 013 bar item | 013 gate | Live this SHA | Tag |
|--------------|----------|---------------|-----|
| NP-CAT-001 name | MUST | `POST /v1/orgs/{orgId}/products` requires name+amount. | **live** |
| NP-CAT-002 prices monthly/yearly | MUST “at least one price” | Product has a price row. Interval stored. Mint **ignores catalog amount** and types amount on the pay-link dialog (002/023 honesty). | **partial** |
| NP-CAT-003 MYR | MUST | Default MYR; non-MYR 400 on catalog. | **live** |
| NP-CAT-004 seats | should Bar C | Absent as SST math. Occupancy `max_payers` is **people on a link**, not seats on a subscription. | missing-feat / do not confuse |
| NP-CAT-005 merchant list/create | MUST | `:5178` Pay links page creates product+link. | **live** |
| NP-CHK-004 open → paid / expired | MUST | Fulfill writes `paid`. Occupancy TTL writes `expired`. Abandoned open no longer immortal. | **live** |
| NP-CHK-005 hosted buyer page | MUST | `:5179` `/c/{token}`. | **live** |
| NP-CHK-006 shareable pay link | MUST | `POST /v1/payment-links` + public token. | **live** |
| NP-CHK-007 buyer pays without One | MUST + fail lock | Checkout `package.json` has **no** oidc. No whoami on the buyer path. Public GET/start have no Bearer. | **live** (code). Human “card succeeded” is **ops** on a real rail. |
| NP-BUY-001 payer email/name | MUST | Start body; stored on checkout; CHIP/Billplz require usable email. | **live** |
| NP-BUY-002–005 portal / magic link | should Bar C | Absent. | missing-feat (not Bar B) |

#### 2.2.3 Gateways

| 013 bar item | 013 gate | Live this SHA | Tag |
|--------------|----------|---------------|-----|
| NP-GW-001 encrypted BYOK per workspace | MUST | `SecretBox` AES-GCM, `Pay:WrapKey` required outside Testing. Vault keyed `(OrgId, Provider)`. | **live** |
| NP-GW-002 Stripe **or** NP-GW-003 one MY rail | MUST one of | **Five** real rails + Test. 013 said not five adapters. Live has Stripe, CHIP, Billplz, Xendit, Razorpay. Capability still `hosted_link`. | **live** (over-shipped vs 013 “one of”) / **ops** (which rail is actually dogfooded) |
| NP-GW-004 webhook verify signature | MUST | Per-rail parse: Stripe-Signature, CHIP PEM, Billplz, Xendit token, Razorpay HMAC, Test `X-Pay-Test-Signature`. | **live** |
| NP-GW-005 empty body 400 | MUST with 004 | `WebhookEndpoints` empty raw → 400. `Empty_webhook_is_400`. | **live** |
| NP-GW-006 idempotent `(tenant, provider, event_id)`; retry no-ops | MUST | PK on `psp_webhook_events`. Replay `{duplicate:true}`. Unique `charges.CheckoutId` and `documents.CheckoutId`. Postgres TX test: throw after save rolls back event; retry pays. | **live** (hermetic + Testcontainers; Ignore if Docker down) |
| NP-GW-007 honest reminder-only matrix | MUST as label | Merchant `processors.ts` copy: Billplz/Xendit/Razorpay “we do not auto-debit.” Hosted_link only. No silent debit code path. | **live** as copy+capability |
| NP-GW-008 setup ≠ paid | MUST | Stripe `mode==setup` or amount 0 ignored. `WebhookTests` covers unpaid `payment_status` and `async_payment_succeeded`. | **live** |
| NP-GW-009 paste/rotate keys; VIEWER cannot | MUST | PUT gateway writer-only. Test PUT → 400. | **live** |

#### 2.2.4 Fulfillment, money, documents, mail, audit, door

| 013 bar item | 013 gate | Live this SHA | Tag |
|--------------|----------|---------------|-----|
| NP-FUL-001 same handler: access + ledger | MUST | `Fulfillment.FulfillPaidAsync`: status paid, charge, optional subscription if interval `mo`/`yr`, journal cash/revenue, `RCPT-`, audit `checkout.paid`. | **live** (one-off). Interval on links is `one_off`. Subscriptions are a column, not a product. |
| NP-FUL-002 buyer access = Pay row | MUST | Buyer is checkout/payer row. No One grant. | **live** |
| NP-FUL-003 merchant payments + subscribers | MUST | Payments + receipts pages. **No subscribers UI.** Subscriptions table exists. | **partial** |
| NP-FUL-004/005 renew / PAST_DUE | should Bar C | Absent. | missing-feat |
| NP-MON-001 journal balanced on first pay | MUST | Two lines: cash D, revenue C, same amount. **No tax, no fee.** 013 listed cash/revenue/**tax/fee**. | **partial** (balanced one-off; SST/fee not written) |
| NP-MON-002 fee only when PSP sent it | should | Not written. | missing-feat |
| NP-MON-003/004 SST × seats, fail-closed | should Bar C; “must not undercharge even in Bar B” | **No SST.** `SstRegistered` was a dead column in 019. Host README: “Pay does not compute SST or file e-invoices.” Qty=1 dogfood with tax=0 is only honest if the merchant is **known not** SST-registered. There is no such column in use. | **missing-feat** + honesty risk if someone sells “MYR including tax” |
| NP-MON-005/006 refund / disputes | should Bar C | Zero `refund` in Pay src. | missing-feat |
| NP-DOC-001 `RCPT-…` | MUST | `RCPT-{year}-{n:00000}` Malaysia year. Title **Official Receipt**. | **live** |
| NP-DOC-002 number never UUID | MUST | Sequence, not checkout Guid. Unique `(OrgId, Number)` and unique `CheckoutId`. | **live** |
| NP-DOC-003 not Tax Invoice | MUST + refuse | Title is Official Receipt. IsolationTests ban `Lhdn` / `MyInvois` / `UBL`. | **live** |
| NP-DOC-004 no VALID | MUST honesty | No VALID print. | **live** (absence) |
| NP-DOC-005 merchant can open receipt | MUST | Receipts list + GET by id. | **live** |
| NP-MAIL-001 receipt email | should | `MailOutbox` table **only**. No writer. | missing-feat |
| NP-AUD-001 audit on charge, same TX | should | `AuditEvents` row `checkout.paid` in fulfill SaveChanges. | **live** |
| NP-AUD-003 audit on key change | should | Gateway PUT and One webhook PUT write audit. | **live** |
| NP-API-002 provider webhook URL | MUST | `POST /v1/webhooks/{provider}/{orgId}`. Merchant hint still a product/ops question (`Pay:PublicBaseUrl`). | **live** |
| NP-API-004 merchant is client of `/v1` | MUST | `:5178` uses `VITE_PAY_API_URL`. No `@repo/api-types-ts`. IsolationTests lock that. Bearer is **user JWT**, not `lzr_sk_`. | **live** as SPA client / **missing-feat** as M2M |
| NP-API-005 tenant isolation | MUST | Other-org 403/404. Path org. | **live** |
| NP-API-006 idempotency on money POSTs | MUST | Checkout create has Idempotency-Key. Payment-link create does **not**. Start is occupancy+slot, not Idempotency-Key. | **partial** |

#### 2.2.5 Per-app 013 “must, to call production-ready”

013 §3.5 host musts vs live:

| 013 host must | Live |
|---------------|------|
| Listen 8081 only | launchSettings + Dockerfile `EXPOSE 8081`, `ASPNETCORE_URLS=http://+:8081`. | **live** |
| Public `/v1` only money door | Mapped under `/v1` except unversioned `/health` and `/ready`. | **live** |
| Whoami + member + **role** on money | Writer overlay. | **live** / partial vs `authz/check admin` |
| One Postgres, one schema, no per-module DbContext | `PayDbContext`, `public`. | **live** |
| Checkout persists; paid from verified webhook | Postgres; Plane B; Test start auto-fulfill. | **live** |
| Webhook verify, empty 400, retry no-ops | See NP-GW-004–006. | **live** |
| Same handler access + journal + RCPT | `Fulfillment`. | **live** |
| Notify/audit functions in process | Audit yes. Mail table empty. No notify binary. | **partial** |
| IsolationTests ban cathedral | Still red on MediatR / `IEnumerable<IHostedRail>` / Hub types. | **live** |
| `pay:test` hermetic | Fake One + Fake PSP. Postgres tests Ignore without Docker. | **live** with that caveat |
| TypeSpec in `pay-spec` only | `main.tsp` grown to live doors. Kernel doors still absent from spec (honest). | **live** as cashier spec |
| CORS 5178/5179; never 3003 | Dev defaults those. Production **requires** `Pay:CorsOrigins`. Tests deny 3003. | **live** lock / **ops** for deployed origins |
| Must not: bind 8080, login route, id_token, PAT, UUID receipt, Tax Invoice | Holds. | **live** refuse |

013 merchant musts: OIDC **live**; screens for keys / pay link / payments / receipts **live**; invite **missing**; types from `@repo/pay-types-ts` **not generated into the SPA** (apps follow the host; IsolationTests ban Hub types).

013 checkout musts: no One login **live**; session by token **live**; payer email **live**; success URL not paid (poll verifying) **live**.

#### 2.2.6 013 fail locks

| Lock | 013 on `6f866ff0` | This SHA |
|------|-------------------|----------|
| No Pay password form | hold | **hold** — no login route on 8081 |
| No second org table | hold (no DB) | **hold** — IsolationTests `ToTable("users")` etc. Payers are not One humans |
| Buyer is not a Zitadel human | unproven | **hold in code** (no oidc on 5179). Unproven as a live card run |
| Setup ≠ paid | unproven | **live** in Stripe parse + tests |
| Receipt not Tax Invoice; number not UUID | unproven | **live** |
| Webhook retry does not double-journal | unproven | **live** in tests (unique charge + TX). InMemory still not a TX proof; PostgresTxTests exist |
| Merchant not sent to `lazuar-admin` | hold in copy | **hold** in merchant nav (Money only). Human redirect unproven |

#### 2.2.7 013 process seams that 03 said the fixture lacked

| 013/03 seam | Live |
|-------------|------|
| Dockerfile | **live** `apps/lazuar-pay/Dockerfile`, image `ghcr.io/proxeon/lazuar-pay:local`. Runtime `ASPNETCORE_ENVIRONMENT=Production`. HEALTHCHECK `/health`. |
| Pay compose | **live** `docker-compose.pay.yml` Postgres 5435 + profile `apps`. |
| Root compose still Hub | **still Hub**. Comment forbids retarget. | **refuse** to retarget |
| Persistence | Postgres, not ConcurrentDictionary for money. `CheckoutStore` still exists as a façade over DB. |
| CORS from config | `Pay:CorsOrigins`. Production/Staging empty **throws at boot**. |
| WrapKey | Required outside Testing. Testing hash fallback only. |
| `/ready` Postgres CanConnect | Unversioned `/ready`. Org ready is `/v1/orgs/{id}/ready`. |
| Rate limit | In-process `PublicPayLimiter` per token. Not a gateway. Multi-replica is a hole. |
| Observability | Console logging. No Serilog cathedral. Fine. |
| TLS / public hostname | **missing** (ops). |

**Summary of §2:** 013 Bar B is **mostly coded** as a hosted cashier and **not lived** as a production sentence. 013 Bar C is still out. The kernel door was **never** 013 Bar B; 013 even parked M2M as NP-SOON-007. 020 is allowed to call that park **the** remaining product lie if anyone sells “platform.”

---

## 3. What 019 said was P0 — re-verify against live tests/code, not YAML

019 parent (`9f04ad58`) named three cash P0s plus two more money holes. 002 YAML marks 001–080 resolved. Bodies still say `Status: open`. YAML is a changelog; **tests are the proof**.

### 3.1 Occupancy (019 P0-1 / P0-3; issues 001–005, 034, 079)

**019 claim:** count-then-insert; two `slot_key`s both mint; unique index is `(PaymentLinkId, SlotKey)` and does not cap `N`; sequential tests green; copy says “successful payment” while code counts `open` **or** `paid`; abandoned `open` never expires; fulfill does not re-check cap.

**Live code:**

`PaymentLinkOccupancy` now:

- Counts `open` or `paid` toward capacity (reservation model, **named**).
- `ReservationTtl` default 30 minutes (`Pay:ReservationTtlMinutes`).
- `ExpireStaleAsync` / `ExpireOpenAsync` (pause expires all open).
- `SerializeAsync` — **process-local** `SemaphoreSlim` per link id.
- `LockParentAsync` — `SELECT 1 FROM public.payment_links WHERE "Id" = {linkId} FOR UPDATE` **only when provider is Npgsql**. InMemory returns immediately.

`MintOrResume` (`PublicPayEndpoints`):

- Requires `slot_key` 8–128 or 400.
- Begins a relational transaction when `IsRelational()`.
- `LockParentAsync` then expire stale then count then insert or 409 `"This pay link is full"`.
- `DbUpdateException` → resume same slot or 409, not 500 (002/013).
- PSP HTTP is **after** the seat commit (comment in 019’s suggested fix). Failed PSP `ExpireFailedReservation` if still open and no redirect URL (002/002).

`Fulfillment` re-counts **paid** children and expires the over-cap child instead of paying it (002/005).

Merchant copy (`CheckoutsPage.tsx`):

```text
The link closes after one person starts Pay. Unpaid starts free after 30 minutes.
```

`locks.test.ts` asserts that sentence and **forbids** `"The link closes after one successful payment."`

GET for a stranger on a one-person **paid** link returns `already_paid` without payer email (002/052).

**Live tests (not YAML):**

| Test | What it locks |
|------|----------------|
| `Two_people_can_pay_a_link_of_two` | Sequential cap of 2; third 409 full |
| `Same_slot_start_twice_does_not_take_two_seats` | CHIP, one PSP HTTP |
| `Two_chip_starts_hold_open_seats_on_a_link_of_two` | Reservation: taken=2, paid=0, third full |
| `Concurrent_start_on_one_person_link_admits_one_psp` | Two clients, sleep in FakePsp; one 200, one 409; `Psp.SendCount == 1`; documents 0 (CHIP not Test) |
| `Concurrent_test_start_on_one_person_link_mints_one_receipt` | One `RCPT-`, one paid |
| `PostgresTxTests.Concurrent_starts_on_one_seat_leave_one_open` | **Npgsql** two starts, taken_count=1, status full |
| `Pause_expires_open_reservations` | ChargesPaused frees the seat |
| `Chip_start_without_email_does_not_occupy_the_only_seat` | 002/002 |

**Remaining occupancy honesty (not a re-open of 001 as written):**

1. **InMemory does not run `FOR UPDATE`.** Concurrent tests on `PayApiFactory()` without Postgres prove the SemaphoreSlim + unique-slot path, not the SQL lock. The test 019 demanded (“InMemory cannot prove this”) is `PostgresTxTests` — **exists**, and `Assert.Ignore`s if Testcontainers cannot start Docker. CI without Docker is a silent skip, not a green lock.
2. **SemaphoreSlim is per process.** Two Pay replicas on one Postgres **are** serialized by `FOR UPDATE` if they both hit Npgsql. Two replicas on a non-relational store are not. Production is Postgres. Say that.
3. **Rate limit is in-process** (`PublicPayLimiter`). A second replica resets the window. That is P2/ops, not occupancy overfill.
4. Occupancy grain is **start-reservation + TTL**, not “paid.” Copy now matches. A Stripe start still fills a 1-person link **before** Plane B. That is the written rule, not a lie, **if** we keep saying “starts Pay.”

**Verdict on 001–005:** the 019 P0 as written is **closed in source and in named tests**. Do not re-file count-then-insert. Do not call occupancy “unfixed” because InMemory skipped `FOR UPDATE`. Do call the Docker-skip and multi-replica limiter **leftover**. YAML `resolved` is earned for 001–005; the markdown `Status: open` in the issue body is stale.

### 3.2 Test Plane B unsigned (019 P0-2; issues 006–008, 042)

**019 claim:** `AllowsTest = !IsProduction()` so Staging is a forge path; `TestWebhook.Parse` has no HMAC; missing `id` mints a new Guid; amount/currency optional; receipts look like Stripe; suite **locked the hole** (`Webhook_pays_open_test_checkout` unsigned).

**Live `PayProviders.AllowsTest`:**

```csharp
env.IsDevelopment() || env.IsEnvironment("Testing")
```

Staging **false**. Production **false**. Test `AllowsTest_is_laptop_and_hermetic_only` locks all four names.

**Live `TestWebhook.Parse`:**

- Requires `Pay:TestWebhookSecret` or throws `"webhook secret missing"` (503 path in the webhook host).
- Requires header `X-Pay-Test-Signature` HMAC-SHA256 hex of the **raw JSON**.
- Fixed-time compare.
- Requires `id`, `checkout_id`, `amount_total`, `currency` or `PspVerifyException` → 400.
- Missing id does **not** mint a Guid.

**Live tests:**

| Test | Lock |
|------|------|
| `Unsigned_test_webhook_is_400` | No header → 400, documents 0 |
| `Test_webhook_without_amount_is_400` | |
| `Test_webhook_without_id_is_400` | |
| `Test_webhook_wrong_amount_does_not_consume_event` | 400, events 0 (fail-closed; 015 style) |
| `Test_webhook_replay_same_id_is_duplicate` | |
| `Webhook_pays_open_test_checkout` | Now uses `SignedTestWebhook` — the suite **no longer locks the hole** |
| `Mint_and_start_pays_without_keys` | Intended dogfood: start = paid on Test, no PSP HTTP |

Merchant `readyMintRails` / `defaultMintRail`: **do not invent Test**. Test is listed only if the host listed it. `withTest` injection from 019 is gone. Production host 400s Test mint. SPA can still **show** Test when the host lists it (Dev). Copy: “Local only. No secrets.”

**Remaining Test honesty:**

1. Test start still auto-fulfills. That is the laptop door. It is not Plane B. Do not delete it in the name of 006.
2. Test webhook **still exists**. 019 offered “delete the route.” 002 kept the route and signed it. A leaked `Pay:TestWebhookSecret` in Development with a tunnel is still a fulfill path for `provider=test` checkouts. That is a **dev secret** problem, not unsigned-in-Staging.
3. Official Receipt title is still the same as Stripe. Receipts now expose `provider` on the wire (`PaymentQueryEndpoints`). Merchant must show it (019 P1). Not re-verified UI-by-UI here; host field exists.
4. Factory `TestWebhookSecret` default `test_whsec_local` is a test constant, not a Production fallback. `AllowsTest` already false in Production.

**Verdict on 006–008:** **closed** as written. Staging is not a Test environment. Unsigned is 400. Missing id is 400.

### 3.3 One HMAC (019 P0-5 / parent §4; issue 011, 029)

**019 split:** paper 10 said Hub dialect FIXED (`t=,v1=` in one header). Paper 07 said product One sends `X-Lazuar-Signature: v1=<hex>` + `X-Lazuar-Timestamp`. Parent: treat pause as **unproven on the live product One wire**.

**Live `OneWebhookSignature`:**

Comment on the type now names **product One** first:

> Product One signs `X-Lazuar-Signature: v1=<hex>` and `X-Lazuar-Timestamp` over `{unix}.{body}`. Combined `t=<unix>,v1=<hex>` in one header is accepted as compat. Raw body hex is rejected.

`TryParse` reads `t=` / `v1=` inside the signature header **or** fills `t` from `X-Lazuar-Timestamp`. HMAC is still `{timestamp}.{body}` SHA256 hex, 300s skew, fixed-time.

**Live `OneWebhookEndpoints`:**

- Reads **both** headers.
- Resolves secret: peek `org_id` / `tenant_id` from body → org `OneWebhookCiphertext` via `SecretBox` → else process `Pay:OneWebhookSecret`.
- Invalid HMAC 401. Empty/garbage signed body 400 (002/063).
- `tenant.suspended` / `tenant.reactivated` set `ChargesPaused`.
- Delivery id from body `id` or `X-Lazuar-Event-Id`; unique `DeliveryId`.

**Live tests:**

| Test | Dialect |
|------|---------|
| `Valid_tenant_suspended_sets_charges_paused` | Combined `t=,v1=` (compat / Hub) |
| `Product_one_split_headers_suspend_charges` | **`v1=` + `X-Lazuar-Timestamp`** — this is the test 011 demanded |
| `Body_only_uppercase_hex_is_401` | Old Hub body-hex rejected |
| `Two_orgs_only_matching_secret_pauses` | Per-org secret; steal 401 |
| `Stored_secret_wins_over_process_fallback` | God-key does not override shop secret |
| `Member_cannot_put_one_webhook_secret` | Writer gate |

**What is still not proven:**

- Nobody in this repo has checked in a **captured** `lazuar-one` dispatcher POST. The new test **mints** the product dialect. Algorithm match is now the **intent**. Envelope field names (`type` vs `event_type`, `tenant_id` vs nested `data`) are guessed from 019. If live One wraps the tenant id differently, `PeekOrgId` misses, the process fallback is used (or 503 if empty), and the **wrong shop** might pause — or nobody pauses.
- Pay does **not** POST One `/tenants/{id}/webhooks`. Operator must create the One endpoint pointing at 8081 and PUT the `whsec_` into Pay. One SSRF blocks loopback (README). Laptop HMAC dogfood needs a tunnel or a One allowlist change. That is **ops**, not a verifier bug.
- Process `Pay:OneWebhookSecret` remains a **one-shop god-key**. 002/029 YAML resolved by adding per-org storage **plus** keeping the fallback. Multi-shop Production that forgets to PUT a secret, but sets the process env, will verify **every** org with one key if `PeekOrgId` fails, or 503 if peek works and ciphertext is empty. `Stored_secret_wins` only applies when ciphertext is present.

**Verdict on 011:** the **dialect P0 as written is closed in Pay source and in a named test**. The **live wire** is still unproven. Do not call pause “production-proven.” Do not call 011 open as if `TryParseHeader` still required `t=` inside the signature header — it does not.

### 3.4 Other 019 money P0s (009, 010, 012)

**Stripe `payment_status` (009):** live parse ignores `checkout.session.completed` unless `payment_status` is `paid` or `no_payment_required`. Honors `checkout.session.async_payment_succeeded`. Tests in `WebhookTests` include unpaid completed and async succeeded. **Closed.**

**Concurrent fulfill / unique `RCPT-` (010):** unique indexes on `charges.CheckoutId` and `documents.(OrgId,Number)` / `documents.CheckoutId`. In-process `SemaphoreSlim` per checkout id in `Fulfillment`. `FillTests.Concurrent_fulfill_of_one_checkout_mints_one_receipt` and `PostgresTxTests.Concurrent_fulfill_same_checkout_one_receipt`. SaveChanges `DbUpdateException` detaches. **Closed** with the same Docker-skip caveat.

**CHIP metadata-only join (012):** `ChipWebhook` still **prefers** `purchase.metadata.checkout_id`. It also sets `HostedSessionId = purchaseId`. `WebhookEndpoints` falls back: if `CheckoutId` empty, look up `ProviderSessionId == HostedSessionId`. `ChipHosted` stores the purchase `id` as `ProviderSessionId`. **Closed as “metadata-only”** if the purchase id is persisted. Still metadata-first. Hostile metadata pointing at another checkout in the same org is a leftover (P2, same-org).

### 3.5 Any 002 miss?

YAML 001–080 resolved is **too clean**. The following are **not** the 019 occupancy/Test/HMAC P0s coming back; they are 002 items whose **source still matches the bug description** or whose “fix” was a product decision.

| # | YAML | Live miss? | Severity now |
|---|------|------------|--------------|
| 001–008, 009–012 | resolved | Source+tests contradict the original bug. | Closed. Leftovers in §3.1–3.4. |
| 014 PSP HTTP then persist | resolved | **Still in source.** `PublicPayEndpoints` comment: “PSP HTTP then persist. A SaveChanges failure after the processor already created a session may mint a second session on retry.” Stripe has `IdempotencyKey = "lazuar-checkout:" + checkout.Id`. CHIP/Billplz/Xendit/Razorpay do not. Stored-URL short-circuit only helps **after** a successful SaveChanges. | **P1 bug** still. YAML over-claimed. |
| 015 amount mismatch does not consume event | resolved | Tests **assert** events stay 0 on mismatch. Fail-closed against hostile PSP. Lost-cash if **our** minor-units are wrong. 002 “fix” did not consume. | **P1 product-true** (keep fail-closed) / honesty: do not say “we ingested the poison event” |
| 023 catalog amount not money | resolved | Amount still typed at mint. Catalog is a label. Merchant dialog is the money. | **P1 missing-feat / honesty** |
| 029 process vs per-tenant `whsec_` | resolved | Per-org **exists**. Process fallback **remains**. | **P1 ops / leftover god-key** |
| 030 writer is `/me` overlay | resolved | Still true. `RequireWriterAsync` after member check reads `/me` role. Not `authz/check admin`. 013 wanted both. | **P1 leftover** (not cash-wrong today) |
| 014’s cousin start-idempotency | 020 | Stripe only. | P1 |
| Issue **bodies** still `Status: open` | docs | Index says resolved. | P2 docs |
| 011/11 Status still `todo` for money Y-rows | docs | Live has rails, receipts, OIDC. Tracker is a **lie by omission** if someone reads only 011. 020 law: do not flip from this paper. | P2 / parent must name the drift |
| Kernel doors | out of 002 | Still absent. 002 did its job. | missing-feat, not a 002 miss |

**002 did not miss occupancy, Test unsigned, or One dialect** in source. It **did** miss (or choose not to finish) persist-before-PSP on non-Stripe rails, and it **resolved** 015/023/030 by documenting or by keeping the safer half. Anyone quoting “080/080 closed therefore production-ready” is doing YAML faith.

---

## 4. Kernel doors 019 already named absent

019 parent §1:

> It is **not** a kernel other apps can swallow in an afternoon: there is still no machine key (`lzr_sk_`) and no outbound `payment.completed`. 018-evals already said that. Live files still say that.

018-evals 001:

> Until a second app (not the merchant Vite) can `POST` a checkout with a machine token and get a signed `payment.completed`, you do not have a kernel. You have a dashboard that mints links.

020 index repeats the same two doors as **out of 002**.

### 4.1 Machine key — still absent on 8081

Evidence:

- `rg lzr_sk_` on `apps/lazuar-pay` **src**: no matches.
- `rg lzr_sk_|payment.completed|MachineKey` on `packages/pay-spec`: no matches.
- `rg lzr_sk_` on `apps/lazuar-pay-merchant`: no matches.
- `Bearer.TryGet` accepts any non-empty `Bearer ` string and forwards it to One. There is **no** prefix check, no hash lookup, no Pay-issued key, no scope (`payments.checkouts:write` or otherwise).
- `OneClient` always calls One `GET me` and `POST tenants/{id}/authz/check` with the caller’s Authorization. A **valid** One `lzr_sk_` **might** work **if** One’s `/me` and `authz/check` accept it **and** `/me` returns `tenants[].role` of owner/admin for writer routes. That is an **accident of forwarding**, not a Pay machine-key product. 012/08 warned: API-key `authz/check` requires `user_id` in the body and **must not** send the key id as `user_id`. Pay’s `CheckMemberAsync` body is only `{ relation, object }`. A real `lzr_sk_` hitting Pay writer routes may 400/403 from One even if the key is valid. **Unproven either way because Pay has no test that sends `lzr_sk_`.**
- P20 checklist still all `[ ]`.
- Merchant `pickApiBearerToken` **rejects** non-JWT tokens. A human SPA will never send `lzr_sk_`. Good for the SPA. Useless for M2M.
- Homemade Pay API-key table: IsolationTests + no table. **Correct refuse.** Do not “fix” the door by minting `sk_live_` in Pay.

Hub museum still has `sk_` cashier (`examples/hub-cashier-next`, `docs/payments-integration-quickstart.md`). Pointing that sample at 8081 is a lie.

### 4.2 Outbound `payment.completed` — still absent on 8081

Evidence:

- `Program.cs` maps health, whoami, org ready, checkouts, payment-links, catalog, public pay, gateways, **inbound** PSP webhooks, payment queries, **inbound** One webhooks. No `MapOutbound`, no dispatcher, no `TenantWebhookEndpoint`.
- `Fulfillment` writes charge, journal, document, audit. It does **not** enqueue an HTTP delivery to a merchant URL.
- `MailOutbox` is a table with no producer.
- `pay-spec` Webhooks tag is inbound only (`POST /webhooks/{provider}/{orgId}`, `POST /one/webhooks`, PUT/GET org One secret).
- IsolationTests **ban** `GatewayPaymentCompletedIntegrationEvent` — the Hub in-process event name. Do not “fix” outbound by importing Hub’s outbox.
- Hub `OutboundWebhookDispatcherJob` lives under `apps/lazuar-api/Modules/One`. That is museum. Pay’s README points `One__BaseUrl` at **product One** `:8080`, not Hub.

Inbound vs outbound must not be confused in a README:

| Plane | Direction | This SHA |
|-------|-----------|----------|
| **A** | One → Pay | `POST /v1/one/webhooks` HMAC. Pause charges. **Live.** |
| **B** | PSP → Pay | `POST /v1/webhooks/{provider}/{orgId}` signed. Fulfill. **Live.** |
| **C** | Pay → **app** | `payment.completed` / `payment.failed`. **Absent.** |

A merchant who pastes a Stripe `whsec_` is **not** receiving Pay events. They are letting Stripe talk to Pay. The second app still has to **poll** `GET /v1/pay/{token}` or merchant `GET /v1/orgs/{orgId}/payments` with a **human** JWT.

### 4.3 Clean `/v1` vs stranger `/v1`

`packages/pay-spec/main.tsp` on this SHA describes the **cashier** doors (health, whoami, org ready, checkouts, payment-links, public pay, catalog, gateways, payments/receipts, inbound webhooks, per-org One secret). 019 counted 22 live / 13 tsp / 11 stale OpenAPI. 002/067–074 grew the spec. Honesty scrape exists (`check-pay-openapi-honesty.mjs`).

That is **cleaner than 019**. It is still not a stranger SDK:

- Every merchant mint door is Bearer + One round-trip.
- Public start is a **browser** door (`slot_key`, occupancy), not an Idempotency-Key M2M door.
- No versioning policy beyond “this is 0.1.0.”
- No `POST /v1/webhook-endpoints` for Plane C.
- No `Authorization: Bearer lzr_sk_`.

Bezos is the **prefix**. Linux is the room. The room is a cashier. The prefix is not Stripe Checkout + Webhooks.

---

## 5. Integrator checklist a stranger would run

A stranger who has read Stripe’s first-hour docs, or Hub’s `payments-integration-quickstart.md`, will try four steps. Name where it **dies today**.

### Step 1 — Get a token

**They want:** `lzr_sk_test_…` with explicit scopes, shown once, hashed at rest in **One**, sent as `Authorization: Bearer`.

**What exists:** One can mint `lzr_sk_` (012/08, sibling product). Pay does not document a Pay scope set. Merchant SPA mints nothing. Curl of a **human** One `access_token` works for whoami (`README.md` live whoami). `id_token` is refused by convention, not by a parser on the host (host forwards whatever Bearer; One rejects).

**Dies:** as an integrator path. They can steal Ada’s access_token from `:5175` and pretend. That is a demo, which 013 already forbade counting as production. M2M from a backend **dies** unless they experimentally send `lzr_sk_` and One happens to accept Pay’s `authz/check` body. No test, no spec, no sample.

**Workaround we must not recommend in a Pay README:** “just use the staff JWT in your server.” That is how you leak a human session into a worker.

### Step 2 — Mint a checkout

**They want:** `POST /v1/checkouts` `{ amount, currency, org_id, success_url, cancel_url }` + `Idempotency-Key` → `{ id, url }`.

**What exists:** `POST /v1/checkouts` **does** exist. Requires writer (owner/admin) via One. Requires `provider` (spec now says so). Amount > 0. Test only in Dev/Testing. Real rail requires vault keys **already pasted in the merchant UI** (or PUT `/gateway` with the same human JWT). Payment-links are a **second** mint door without Idempotency-Key.

**Dies:** if they do not have a writer human JWT **and** a vault row. The “app creates a checkout, merchant keys stay in Pay” story is the kernel story; the vault is there, the **app identity** is not. If they **are** Ada with keys pasted, mint **works**.

### Step 3 — Start pay / send the buyer

**They want:** hosted URL. Buyer completes on the PSP (or Test).

**What exists:** `GET /v1/pay/{token}` and `POST /v1/pay/{token}/start` are **public**. Checkout SPA does this. `slot_key` required on payment-link tokens. Rate limit 20/min/token default, in-process. Buyer has no One account.

**Does not die** for a human buyer hitting `:5179/c/{token}`. This is the one step that is actually a product.

**Dies for an API integrator** who expected `POST /v1/checkouts` to **return** the hosted PSP URL. Merchant mint returns a Pay public token; **start** is a second call, often from the browser, and for CHIP/Billplz needs a usable email. Headless M2M start is possible with curl **without** Bearer — which is a **feature** (buyer plane) and a **griefing** surface (002/019 slot_key). Not an OAuth client credentials flow.

### Step 4 — Learn paid

**They want:** signed POST `payment.completed` with retry, or at worst a `GET` they can poll with the same machine key.

**What exists:**

- Buyer SPA polls public GET until `status=paid` (Test start is immediate; Stripe waits Plane B).
- Merchant lists `GET /v1/orgs/{orgId}/payments` and `/receipts` with **member JWT**.
- Audit row `checkout.paid` is **not** an HTTP API for them.
- No Plane C.

**Dies.** A second app cannot learn paid without either embedding the poll in a browser they do not own, or polling merchant GETs with a human token. Hub sample’s `/webhooks/hub/payments` will never ring.

### Where the path dies, in one line

**Step 1 dies for M2M. Step 4 dies for everyone who is not the hosted page or the staff shell. Steps 2–3 work only as the first-party cashier.**

Four processes before a second caller: One (8080 + Zitadel 8085 + OpenFGA 8090 + login 5175), Pay 8081, merchant 5178, checkout 5179 — and still no second caller. That is the refuse in §6.

---

## 6. Ranked list this SHA

Tags: **bug** (wrong vs written law / vs own copy), **missing-feat** (not built), **ops** (code cannot finish it), **refuse** (must not build).

P0 means: money can be wrong, or the sentence we want to print is a lie that takes cash or identity. Kernel absence is **P0 for the kernel bar** and **P1 for the cashier bar**. This list **splits** that so parent 00 cannot flatten it.

### 6.1 P0 — cashier money still, or a README lie if we say “platform”

| ID | Tag | Item | Evidence | Notes |
|----|-----|------|----------|-------|
| K1 | **missing-feat** | No machine key on Pay | §4.1 | P0 **if** we sell API platform. P2 if we only sell hosted links to One merchants. **This program’s question includes the kernel.** Rank P0 here. |
| K2 | **missing-feat** | No outbound `payment.completed` | §4.2 | Same split. P0 for second app. |
| C1 | **bug** leftover | Persist-after-PSP on CHIP/Billplz/Xendit/Razorpay | `PublicPayEndpoints` comment + 014 still true | Stripe idempotency key only. Retry after SaveChanges fail can double-purchase. Not 019 occupancy. Still cash. |
| C2 | **ops** | Live One `tenant.suspended` envelope unproven | Tests mint dialect; no captured fixture | Pause is the **buyer** belt. Staff 403s via membership. Public start does not. |
| C3 | **ops** / **bug** if mis-set | Production `Pay:WrapKey` / `Pay:CorsOrigins` empty | SecretBox throws; PayCors throws in Production/Staging | First vault PUT 500s; host may fail boot. `.env.example` is honest now (no Dev wrap fallback). Operators who skip `.env` still burn. |
| C4 | **honesty** | 011/12 and 011/11 still `todo` on money | Tracker vs live | Not a runtime bug. A **meta P0** if someone quotes the tracker as the product. |

Occupancy last-seat, Test unsigned, Stripe unpaid completed, unique `RCPT-`, One **dialect** are **not** P0 on this SHA.

### 6.2 P1 — product-false, dogfood, leftover 016/019, kernel-adjacent

| ID | Tag | Item |
|----|-----|------|
| P1-1 | missing-feat | Invite / copy-link / MEMBER dogfood in `:5178`. NP-ONE-011/012/022. |
| P1-2 | missing-feat | SPA registration as a Pay-owned runbook (NP-ONE-001/004). Env-shaped `client_id`. |
| P1-3 | ops | One HMAC subscription: Pay does not register. Tunnel vs One SSRF. Per-org PUT. |
| P1-4 | leftover god-key | Process `Pay:OneWebhookSecret` fallback (029). |
| P1-5 | leftover | Writer = `/me` role, not `authz/check admin` (030). Member cannot mint; good enough for cashier, not for a confused FGA story. |
| P1-6 | missing-feat | Catalog amount ignored at mint (023). |
| P1-7 | bug | 014 persist-after-PSP (also P0-C1). Ranked in both so it cannot be dropped as “kernel only.” |
| P1-8 | missing-feat | Payment-link create has no Idempotency-Key. |
| P1-9 | missing-feat | SST unknown: no fail-closed column. Do not print prices as tax-inclusive. |
| P1-10 | missing-feat | Refunds, disputes, renewals, PAST_DUE. Bar C. |
| P1-11 | missing-feat | Receipt email (`MailOutbox` dead). |
| P1-12 | missing-feat | Second-app sample against **8081**. Hub sample is museum. |
| P1-13 | missing-feat | Docs/SDK for Pay `/v1` that do not mention Hub `sk_` / `payment.completed`. |
| P1-14 | leftover | CHIP/Xendit/Razorpay join still metadata-heavy; SETTLED ignored (Xendit `SETTLED` → ignored, PAID pays). Confirm that is still the honest matrix. |
| P1-15 | ops | Which Malaysian rail is the first **live** dogfood (CHIP vs Billplz). Five adapters shipped; 013 wanted one. |
| P1-16 | leftover | In-process start limiter; occupancy SemaphoreSlim. Fine on one replica. |
| P1-17 | leftover | Amount mismatch 400 does not consume event (015 keep). Document it. |
| P1-18 | leftover | `slot_key` client-supplied. Memory+localStorage+sessionStorage in checkout. Private mode is one tab. Two devices = two seats on unlimited; on max=1 the second 409s. Griefing a capped link with many slots until full is still possible until TTL. Rate limit helps. |
| P1-19 | missing-feat | M2M checkout (NP-SOON-007) — the honest name of K1+K2. |
| P1-20 | ops | Public Billplz HTTPS `Pay:PublicBaseUrl`. Localhost callbacks 400 by design. |

### 6.3 P2 — polish after money is boring

| ID | Tag | Item |
|----|-----|------|
| P2-1 | leftover | Dummy-ish `/v1/orgs/{id}/ready`: now `!charges_paused && (vault \|\| AllowsTest)`. Still not “SST known / can legally charge.” Better than 013’s `ready: true` meaning member. |
| P2-2 | leftover | Unversioned `/ready` vs org ready. Spec host-only. Easy to curl wrong. |
| P2-3 | leftover | GET receipt-by-id exists; 019 said untested; 002/075. Assume tested or not — PaymentQueryTests exist for list. |
| P2-4 | leftover | Child checkout public tokens still extra URLs; they load parent occupancy (test exists). |
| P2-5 | leftover | Issue markdown Status: open vs YAML resolved. |
| P2-6 | leftover | 011 tracker drift. |
| P2-7 | leftover | Malay copy absent. |
| P2-8 | leftover | Vitest in merchant/checkout; whether CI job `pay` runs them is 06’s job. |
| P2-9 | leftover | `AddDataProtection()` still in `Program.cs`; wrap is `SecretBox`, not DP. |
| P2-10 | leftover | Root compose Hub; mprocs folklore. Pay compose is a **second** file — easy to boot the museum by habit. |
| P2-11 | leftover | Journal has no tax/fee lines. Honest if we say “gross cash=revenue.” |
| P2-12 | leftover | No subscribers page. |
| P2-13 | leftover | CORS tests vs `/v1/pay` OPTIONS — 002/066 claimed; CorsTests grew. Not re-counted method-by-method here. |
| P2-14 | refuse-adjacent | Dead Hub `ActiveProvider` stories — PUT must not write a single active rail (019 said independent vault). IsolationTests do **not** lock “do not write ActiveProvider.” |

### 6.4 Explicit kernel / secret / webhook / M2M rows (the index asked for these by name)

| Topic | This SHA | Rank |
|-------|----------|------|
| **Secret key / `lzr_sk_`** | One can mint. Pay does not consume as a product. SPA rejects non-JWT. | P0 missing-feat (kernel bar) |
| **M2M** | No client-credentials, no service user, no Pay scopes. Forwarded Bearer is a human JWT in every test. | P0 missing-feat |
| **Webhooks inbound Plane A One→Pay** | Live, dual dialect, per-org secret, process fallback. | Live cashier / P1 ops |
| **Webhooks inbound Plane B PSP→Pay** | Live, six names, unique event, fulfill TX. | Live cashier |
| **Webhooks outbound Plane C Pay→app** | Absent. | P0 missing-feat (kernel bar) |
| **Clean `/v1`** | Cashier `/v1` + pay-spec aligned better than 019. Not a stranger API. | P1 missing-feat (docs/sample/M2M) vs live as first-party door |

---

## 7. Refuse list (do not staff these as “how we become production-ready”)

The index named these. Live IsolationTests already make some of them CI-red. Keep them red.

| Refuse | Why it shows up | Live lock |
|--------|-----------------|-----------|
| **MediatR** | Second handler on checkout. | `IsolationTests` Banned + BannedSrc |
| **`IEnumerable<IHostedRail>`** | Factory gravity. Six rails are a `switch` in `PublicPayEndpoints` / `WebhookEndpoints`. Ugly and **honest**. | BannedSrc token |
| **Hub types `@repo/api-types-ts`** | SPA “just generate.” | IsolationTests Vite `package.json` |
| **Zitadel PAT** | “Whoami is flaky, hold a PAT.” | `.env.example` / README. No PAT in src |
| **SST on the pay path** | “Production in MY means e-invoice.” Receipt ≠ tax invoice. LHDN IsolationTests ban. | NP-XX-001–003; host README |
| **Pay-local user table** | “Buyers need login.” | IsolationTests `ToTable("users")` / `members` / `organizations` |
| **God-key** | One `lzr_sk_` in Pay env that speaks for every merchant; or one process `whsec_` for every One tenant as the **only** story. | 012/08: a key is bound to one tenant. Process One webhook secret is a **one-shop fallback**, not a platform key. Do not grow it. Do not add `One:ApiKey` as a default Authorization on `OneClient` (013/03 lock: no `DefaultRequestHeaders.Authorization` from config). |
| **Retarget Hub compose** | “Production is `VITE_API_URL=8081` on ops.” | Root compose comment; CORS deny 3003; P60 |
| **Four processes before a second caller** | Shipping merchant+checkout+Pay+One and calling that “platform.” The second caller is a **fifth** identity (machine key + Plane C), not another Vite. | This paper |
| Homemade LHDN / TIN-at-checkout / Tax Invoice title | Hub parity magnet | NP-XX-001–003 |
| WhatsApp / Xero / escrow-on-Processor / Web3 | 018 vitamins | NP-XX-004–006 |
| Cookie JWT / `POST /v1/auth/login` | Hub IdP | NP-XX-007 |
| Pay `authz/write` / FGA types `payment` | AUTHZ-05 without a consumer | NP-XX-015/016 |
| `POST /platform/tenants` | Staff directory | NP-XX-023 |
| Parse Zitadel project-role claims | | NP-XX-024 |
| Notify/Audit/Media as processes | | NP-XX-019/020 |
| Go rewrite of this host in the next slice | 011/05 out of band | refuse for 020 |
| Closing Hub issues 261–334 on `apps/lazuar-api` | | 011 |
| One Okta / SCIM / hosted SKU as the next **Pay** ticket | One staging NOT PASSED | NP-XX-022 |
| Un-refuse NP-XX without editing 01-product.md | | 011/11 |

**God-key, restated so 02-machine-keys cannot “clarify” it:** Pay must not put a single `lzr_sk_` in env and attach it to every outbound One call. Interactive whoami **forwards the caller**. A later worker key is **one tenant**. Multi-merchant workers need **per-request** merchant keys, not a platform PAT.

---

## 8. Sequence — two first tickets, do not mix them

They **differ**. Saying “fix occupancy then kernel” was 019’s order when occupancy was P0. Occupancy is no longer that P0. The next slice is a **choice of product**.

### 8.1 Build first so **other apps** can integrate

Goal: a stranger (or Lazuar’s next app) without cloning Vite.

1. **Define the Pay machine-key contract without minting in Pay.** One already mints `lzr_sk_`. Pay must: document required scopes; send `authz/check` in the body shape One actually accepts for keys (012/08 `user_id` rule); accept `Bearer lzr_sk_` on mint doors; **never** attach a god-key on `OneClient`. Tests with a fake One that distinguishes JWT vs key. IsolationTests stay red on homemade `api_keys` tables.
2. **Plane C: `payment.completed` / `payment.failed`.** Same fulfill handler enqueues a signed delivery (One-style HMAC, Pay-owned secret the **app** stores). Retry with backoff **in process** (no `lazuar-notify`). Idempotent event id = charge id / document number. Spec it in `pay-spec`. Ban Hub `GatewayPaymentCompletedIntegrationEvent`.
3. **A sample that is not `:5178` and not Hub.** `examples/pay-cashier-*` against **8081**. Provision: mint One key, PUT Pay vault **or** assume keys, POST checkout, verify webhook, unlock a toy row. Do **not** retarget `examples/hub-cashier-next`.
4. Then: Idempotency-Key on payment-links; persist-before-PSP or per-rail idempotency (C1); docs that do not mention Hub `sk_`.

Do **not** start this list with Aura chrome, Malay copy, or Hub cutover.

### 8.2 Build first so **first-party dogfood** can go live

Goal: Lazuar charges a real test card on 8081+5178+5179. Kernel still absent. That is allowed **if the README says cashier**.

1. **Ops that 013 Bar B still lacks:** register the merchant SPA in One (client_id, redirects, allowlist). `Pay:WrapKey`, `Pay:CorsOrigins`, `Pay:PublicBaseUrl` (if Billplz), `Pay:CheckoutBaseUrl` as public HTTPS. Postgres backups. Tunnel that is not Test-unsigned (Test is already signed and Staging-disabled).
2. **Pick one live rail** (Stripe test-mode **or** CHIP). Stop pretending five BYOK rails are five production dogfoods. Xendit SETTLED ignored / Razorpay captured tests exist; they are not a reason to dogfood all five.
3. **Invite the second engineer on One** (copy-link). Optional: deep-link from merchant. Do not homemade email. MEMBER GET already works; owner/admin PUT already 403s member.
4. **Replay a real One `tenant.suspended`** against 8081. If it 401s, fix envelope mapping — not IsolationTests, not Hub `Modules/One`. Store the fixture as a test vector (sanitized).
5. **014 persist-before-PSP** on the rail you actually dogfood.
6. Then: occupancy TTL ops tuning, receipt email, SST **column** if you print MYR tax, not LHDN.

Do **not** block first-party go-live on Plane C or on a second-app sample. Do **not** call go-live “platform.”

### 8.3 What not to invert

- Do not staff escrow, factory, registrar, SST XML, e-mandate, Hub cutover, MediatR, or ops :3003 retarget in either sequence.
- Do not wait on One staging PASSED (NP-XX-022).
- Do not start **soon** quotes/partial-refunds until Bar B is boring (011/10). Bar B is not boring.
- Occupancy lock is **done enough** to leave it off the front of both queues.

---

## 9. Disagreements predicted with 01–09

Sibling files were **not on disk**. These are traps, not accusations.

### vs 01 — public HTTP API

01 may call `/v1` “clean” because `main.tsp` now lists payment-links, gateways, Test, receipts, `slot_key`, 201 vs 200, dual webhook unions. **Agree that 019 spec-lag is largely closed.** Disagree that a clean cashier OpenAPI is a stranger API. Version `0.1.0`, no M2M, no Plane C, writer-only mint. If 01 ranks “spec honesty” as the production bar, that is 013 Bar A-shaped thinking.

### vs 02 — machine keys / M2M

02 will correctly find One’s mint and say Pay should not clone the table. **Agree.** Risk: concluding “M2M is One’s job, Pay is done.” Pay **forwarding** Bearer is not a machine-key **product**. If 02 says “a `lzr_sk_` might already work,” demand a test. 012/08 `user_id` on `authz/check` is the likely silent 400.

Risk the other way: 02 proposes a Pay-local key table because One staging is NOT PASSED. That is NP-XX-014/007 gravity. **Refuse.**

### vs 03 — outbound webhooks

03 may inventory Hub `OutboundWebhookDispatcherJob` and Hub docs `payment.completed`. **Those are museum.** If 03 says “outbound exists in the repo,” the honesty paper says **not on 8081**. If 03 proposes importing Hub One outbox into Pay, IsolationTests and 011 binding #1 refuse it. Copy **judgment** (HMAC, retry, event id), Pay-owned.

### vs 04 — inbound webhooks

04 should agree Plane A/B are live. Predicted split: 04 treats 011 live-wire as still P0; this paper treats **dialect** as closed and **envelope/ops** as P1/ops. Do not paper over: both can be true. Parent 00 must not pick a winner in a one-liner.

04 may call Test webhook leftover unsigned — **false** on this SHA.

### vs 05 — identity / tenancy

05 may rank writer `/me` overlay as P0 vs 013’s `authz/check admin`. This paper: **P1 leftover**. Member cannot mint; tests exist; One has no VIEWER. Fail-closed for the **wrong** people would be mapping VIEWER onto `member`. We did the opposite: member is read-only on money. Good.

05 may want a Pay user table for buyers. **Refuse.**

### vs 06 — host production

06 may say Dockerfile + compose.pay = production process. **Partial.** Images exist; root compose is still Hub; CORS/WrapKey fail-closed; rate limit in-process; `/health` is liveness not money; Production image `USER app` is good. 013/03’s “fixture not a process” is **stale** if quoted as live. 013/03’s “do not copy Serilog+KeyVault+nine migrators” is **still law**.

### vs 07 — money remaining

07 may still lead with occupancy P0 from 019. **Disagree.** Occupancy is closed with Docker-skip and replica caveats. 07 should spend ink on 014 persist-after-PSP, SST absence, refunds, journal without tax/fee, Xendit SETTLED ignored, catalog-as-label.

If 07 says fulfill TX is unproven: **PostgresTxTests exist**; InMemory still is not a proof. Both sentences belong.

### vs 08 — headless vs SPA

08 may say merchant is already a `/v1` client so a second app can copy `payApi.ts`. **Disagree.** The SPA holds a **human** JWT and OIDC. Copying it is cloning a staff shell, not integrating. Headless mint without Plane C still cannot learn paid.

Checkout **is** the headless buyer client. That is not an integrator SDK.

### vs 09 — spec / docs / sample

09 may say pay-spec honesty is restored. **Mostly agree** vs 019. Kernel doors absent from spec is **correct** (do not TypeSpec `payment.completed` until the handler exists). Risk: generating SPA types from tsp and calling that a sample. 019 already said generating from a lagging spec makes UIs worse; the spec caught up to the **cashier**, not to the kernel.

09 must not list `examples/hub-cashier-next` as the Pay second-app. It is Hub.

### vs parent 00 (when it exists)

00 must not flatten K1/K2 into “P2 polish after 002.” 00 must not flatten 014 into “YAML resolved.” 00 must keep the two sequences. 00 must not flip 011/11 from this program.

---

## 10. Honest sentences we may say in a README

### 10.1 Allowed (cashier, this SHA, local / first-party)

- Lazuar Pay is a **hosted cashier** for **One workspaces**.
- Staff sign in through One (`:5175`). Pay does not have a password form.
- One tenant id is Pay `org_id`. There is no second organizations table.
- Staff paste BYOK keys **per rail**. Saving a vault does **not** pick the pay-link rail.
- Mint a hosted pay link with an explicit `provider` that already has keys. Test needs no keys and exists in Development/Testing only.
- Buyers pay on `:5179` **without** a One account, without a PAN on Pay, without a PSP picker on Pay.
- Success URL is **not** paid. The page polls until a verified Plane B webhook (or Test start) writes an Official Receipt `RCPT-…`.
- The receipt is a **commercial Official Receipt**, not a tax invoice, not MyInvois VALID, not SST-computed.
- Capability is `hosted_link`. We do not auto-debit. Billplz-class rails are reminder + hosted page.
- Occupancy: a capped link counts **open reservations and paid** children. Unpaid opens expire after 30 minutes (configurable). “1 person” means one **start**, not one webhook, and concurrent starts are serialized on Postgres with `FOR UPDATE` plus a unique slot.
- Plane B retries no-op on `(org, provider, event_id)`. One checkout mints at most one charge and one `RCPT-` (unique indexes + tests).
- Plane A: One `tenant.suspended` **can** pause public charges when HMAC verifies. Per-org `whsec_` is PUT by a writer. Dialect accepts product One split headers and Hub-style combined headers. **Live One dispatcher replay is not in this repo.**
- Isolation from Hub Payments / MediatR / `IEnumerable<IHostedRail>` / `@repo/api-types-ts` still holds.
- Listen **8081**. Root `docker-compose.yml` is Hub museum. Pay images are `docker-compose.pay.yml`.

### 10.2 Forbidden (marketing lies)

- “Pay is a payments API platform.”
- “Pay is Stripe-clean: one key, one checkout, one webhook.”
- “Other apps can integrate in an afternoon.”
- “`lzr_sk_` works on Pay.” (One mints it; Pay has no product for it.)
- “We send `payment.completed`.” (Hub did. Pay does not.)
- “Production-ready.” (013 Bar B human loop unrun; tracker still todo; kernel absent.)
- “002 closed 080 issues therefore the kernel shipped.”
- “1 person only” without the reservation sentence.
- “Test is local-only / Production-safe” as if Staging used to be the hole **and we did not check `AllowsTest`.** We **did** narrow it. Still do not run Test in Production. Still do not tunnel unsigned Test — unsigned is 400, signed Test in Development with a leaked secret is not.
- “Five (or six) rails are production BYOK.” They are **hosted_link adapters** with uneven live dogfood. Xendit SETTLED is ignored. CHIP persist-after-HTTP remains.
- “`task pay:spec` is the host” — closer than 019, still not a kernel spec.
- “One `tenant.suspended` pauses charges on the live product One wire” without a captured replay.
- “Catalog prices the link.” Amount is typed at mint.
- “Official Receipt is an e-invoice / SST invoice.”
- “Hub adapters run on 8081.” HTTP extracts only.
- “Replace ops :3003 with merchant :5178 and you have Hub parity.” Merchant nav is Overview / Processor / Pay links / Payments / Receipts. That is the product.
- “Connected (whoami 200) is production-ready.” Bar A, 012 C99, already true last month.

### 10.3 One paragraph that may sit under the title `Lazuar Pay`

> Pay is the hosted cashier for workspaces that already live in Lazuar One. A merchant pastes their own Stripe or Malaysian hosted-link keys, mints a URL, and a buyer pays on that URL without a One login. Pay writes an Official Receipt and a two-line journal. It is not a tax system, not a general payments API, and not a kernel other products can call with a secret key and a webhook. Those doors are not built.

If product wants the kernel paragraph instead, **build §8.1 first**, then change the README. Do not change the README first.

---

## 11. 013 Bar B executable loop vs this SHA (so parent can score)

011/12 steps, live honesty, still **do not flip** the tracker from this file.

| Step | Job | Live code | Lived by a human? |
|------|-----|-----------|-------------------|
| 1 | Register SPA | Env-shaped | **Unknown / todo** |
| 2 | Sign-in `:5175`, whoami | OIDC + whoami | **Unknown**; code path exists |
| 3 | Create workspace `POST /tenants` | `oneApi.ts` | Code path exists |
| 4 | Invite second engineer | **No merchant UX** | Missing |
| 5 | Mint `lzr_sk_` | One only; Pay unused | Missing as Pay door; optional for cashier |
| 6 | Subscribe suspend | Receiver live; Pay does not subscribe | Ops |
| 7 | Stop One-side extras | Still refuse | hold |
| 8 | BYOK keys | Vault + UI | Code path exists |
| 9 | Product + pay link | UI + POST | Code path exists |
| 10 | Buyer pays without One | Checkout SPA | Test rail yes in hermetic tests; real card **unknown** |
| 11 | Webhook, journal, RCPT, retry | Host + tests | Hermetic yes; live PSP **unknown** |
| 12 | MEMBER sees; VIEWER cannot | Member GET; writer 403 | Invite missing so second human unknown |

**Bar B is not production-ready.** It is no longer the 013 empty Vite. Calling it “still a fixture” would also be a lie.

---

## 12. Evidence scraps (so this file is not a slide)

### 12.1 Host composition root (live doors)

`Program.cs` maps: `MapHealth`, `MapWhoami`, `MapOrgReady`, `MapCheckouts`, `MapPaymentLinks`, `MapCatalog`, `MapPublicPay`, `MapGateways`, `MapWebhooks`, `MapPaymentQueries`, `MapOneWebhooks`. No outbound. No keys. No refunds. No login.

### 12.2 Occupancy lock snippet

`LockParentAsync` no-ops when the provider name does not contain `Npgsql`. Concurrent InMemory tests therefore prove `SemaphoreSlim` + insert, not `FOR UPDATE`. `PostgresTxTests.Concurrent_starts_on_one_seat_leave_one_open` is the SQL proof and is skipable.

### 12.3 Test HMAC snippet

`TestWebhook.SignatureHeader = "X-Pay-Test-Signature"`. Factory secret `test_whsec_local`. Unsigned test expects 400.

### 12.4 One dual parse

`TryParse` fills `t` from combined header **or** `timestampHeader`. `Product_one_split_headers_suspend_charges` is the 011 test. Combined `Sign` helper in tests still emits `t=,v1=` for compat cases.

### 12.5 Fulfillment journal

Cash debit, revenue credit, same `checkout.Amount`. No tax line. No fee line. Title `Official Receipt`. Sequence `RCPT-{year}-{n}`. Unique charge per checkout.

### 12.6 Merchant nav

Money: Overview, Processor, Pay links, Payments, Receipts. No Invoicing, no ops chat, no Hub Developer keys, no tax invoices.

### 12.7 Checkout identity

`package.json` dependencies: radix slot, cva, clsx, lucide, react, tailwind-merge. **No oidc-client.** `payApiOrigin()` requires `VITE_PAY_API_URL` in production builds; Dev may default localhost.

### 12.8 IsolationTests banned tokens

`lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`, `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`, `namespace Lazuar.Pay.One;`, factory/registrar/DNS folklore strings, LHDN/UBL, Hub types in Vite package.json, `ToTable("organizations"|"users"|"members")`.

### 12.9 Tracker drift (do not flip)

011/11 still: NP-GW-001 todo, NP-CHK-005 todo, NP-DOC-001 todo, NP-API-004 todo, NP-ONE-002 todo, while source has vault, hosted page, `RCPT-`, SPA client, OIDC. 020 must not treat 011 Status as live. 020 must not silently flip it. Parent 00 should **name** the drift as a documentation P0 for anyone using the tracker as a dashboard.

### 12.10 Issue YAML vs body

Frontmatter `status: resolved` + heading `- **Status:** open` on 001, 006, 011, 014. Index table “resolved on this SHA.” Readers of the body will think 001 is still the occupancy race. Point them at `PaymentLinkTests.Concurrent_*` and `PostgresTxTests`.

---

## 13. What “production-ready” may mean after this paper (not a new bar)

Keep 013’s three bars. Add the 020 kernel bar **beside** them, not instead of them.

| Bar | Name | This SHA | Word we may use |
|-----|------|----------|-----------------|
| A | Connected | Pass since 012 C99 | connected |
| B | First-slice live dogfood | Partial — cashier coded, loop unrun, invite missing, live One HMAC uncaptured, 014 open on MY rails | **hosted cashier (laptop / hermetic)** |
| C | Product v1 | Fail — refunds, SST, renew, portal | not v1 |
| K | Second-app kernel | Fail — no `lzr_sk_` product, no Plane C, Hub sample | **not a kernel** |

Production-ready **remains** Bar B lived, not Bar K, not Hub parity, not One Okta. Bar K is how other Lazuar apps (and then strangers) swallow Pay. It is the strongest **idea** (018). It is not shipped.

If leadership asks “can we take money next week?” the honest answer is: **maybe first-party Test and Stripe test-mode on a laptop, with One registered SPA and WrapKey set, if you accept no invite UX and no app webhook.** That is a dogfood. It is not production. It is not a platform.

If leadership asks “can Aura / the next app use Pay instead of Stripe?” the honest answer is **no, not on `6d730d15`.** Build §8.1.

---

## 13.1 002 001–080 re-verify (not YAML)

The index says 001–080 resolved. §3 covered the P0 cash trio. This table is the rest, so parent 00 cannot say “honesty skipped 013–080.” Status here is **this SHA live**, not issue-body `open`.

| # | 019/002 claim | Live | Honesty |
|---|----------------|------|---------|
| 001 occupancy count-then-insert | Two slots overfill | `FOR UPDATE` + SemaphoreSlim + concurrent tests + PostgresTxTests | **Closed.** Docker skip leftover. |
| 002 seat reserved before start succeeds | Email/PSP 400 occupies | `ExpireFailedReservation`; `Chip_start_without_email_does_not_occupy_the_only_seat` | **Closed** for email-required rails. Persist-after-PSP (014) is a different hole. |
| 003 abandoned open never expires | No TTL | `ExpireStaleAsync` 30 min | **Closed.** Copy matches. |
| 004 occupancy copy lies | “successful payment” | Dialog: “one person starts Pay” | **Closed.** |
| 005 fulfill pays over-cap children | No re-check | `Fulfillment` counts paid, expires extra | **Closed.** |
| 006 Test unsigned non-Production | `!IsProduction()` | Dev+Testing only; HMAC required | **Closed.** |
| 007 Test omits amount still pays | Optional amount | Required `amount_total` + currency | **Closed.** |
| 008 missing Test `id` mints Guid | Replay never duplicates | Missing id 400 | **Closed.** |
| 009 Stripe completed without payment_status | Unpaid books | `Unpaid_completed_session_is_ignored`; async succeeded pays | **Closed.** |
| 010 concurrent fulfill double RCPT | No unique charge | Unique indexes + gates + Postgres concurrent test | **Closed.** |
| 011 One HMAC dialect | Split headers 401 | Split headers test 200; body-hex 401 | **Closed in source.** Live wire ops. |
| 012 CHIP metadata-only | No session join | `HostedSessionId` fallback | **Closed as written.** Metadata still first. |
| 013 same-slot start 500 | Unique race | `DbUpdateException` → resume or 409 | **Closed.** |
| 014 PSP then persist | Comment admitted it | **Comment still there.** Stripe idempotency only. | **Open P1/P0-C1.** YAML miss. |
| 015 mismatch 400 no consume | Fail-closed | Tests assert events=0 | **Kept.** Document, do not “fix” by consuming poison. |
| 016 CHIP create no currency | Missing field | `ChipHosted` `purchase.currency = checkout.Currency` | **Closed.** |
| 017 WrapKey docs lie | Dev fallback | `.env.example` “Required outside Testing. No Development fallback.” SecretBox throws outside Testing. | **Closed** as docs. Ops still burn if unset. |
| 018 CS password keep replaces whole CS | 018 bug | Not re-read line-by-line this paper; 002 commit `a36a3b7a` “honest CS”. | **Assume closed**; 06 should cite the loader. |
| 019 public slot_key grief | Client seat | Still client-supplied 8–128. Rate limit 20/min. TTL frees. | **Mitigated, not gone.** P1-18. |
| 020 checkout idempotency racy body-blind always 201 | | `IdempotencyConflictException` → 409 different body; replay 200 same body (`CheckoutEndpoints` 201 vs 200). | **Closed** for POST /checkouts. Links still have no key. |
| 021 Development MigrateAsync crash | Cors/Health boot real DB | `Program.cs` try/catch log “pay-db schema mismatch”. Tests use Testing env. | **Mitigated.** |
| 022 CheckoutUrls.Base throw 500 | Uncaught | MintOrResume catches → 503 | **Closed.** |
| 023 catalog not money | Typed at mint | Still true. | **Open honesty.** |
| 024 `.env.example` process whsec fallback | Advertised | Stripe process secret commented Testing-only. One process secret commented one-shop fallback. | **Closed as lie**; leftover as god-key option. |
| 025 ChargesPausedException catch order | Brittle | Dedicated catch before InvalidOperationException in WebhookEndpoints | **Closed.** |
| 026 null Provider leftover cannot start | | Backfill migration `20260828001728_BackfillNullCheckoutProvider`; never-started webhook 400 provider mismatch | **Closed** if migration ran. |
| 027 PUT any CHIP webhook_secret | PEM at verify | Not re-verified PUT validation this paper. 002 `c562b28f` “vault PUT checks”. | **Likely closed**; 04 should cite PUT tests. |
| 028 re-save non-Billplz writes environment=test | | Same commit family. | **Likely closed**; 04. |
| 029 process vs per-tenant One whsec | One secret | Per-org PUT + fallback | **Half.** P1-4. |
| 030 writer `/me` not authz admin | | Still `/me` overlay | **Open leftover** by 013 letter. Cashier-true. |
| 031 GET org checkouts mixes children | | List filters `PaymentLinkId == null` | **Closed.** |
| 032 child public tokens second pay URL | | Still exist; load parent occupancy | **Kept** with a test. P2. |
| 033 pause after mint stuck-occupies | | GET pause expires open | **Closed.** |
| 034 occupancy tests hide reservation | | CHIP open-seats test paid_count=0 taken=2 | **Closed.** |
| 035–047 merchant SPA | stuck JWT, silent GET, Test always on, webhook hint localhost, mint defaults Test, silent renew `/callback` | Commits `1479b039` `d4519da0` `66bb1cf9`; processors.ts no invent Test; silent-renew.html; occupancy copy tests | **Treat as closed in source** unless 02/08 reopen a named hole. Invite still missing (not in 035–047). |
| 048–061 checkout SPA | Loading graveyard, localhost API, slot_key rotate, Thank-you to strangers, verify timeout, start throw | `pay.ts` fail-build without VITE in prod; already_paid; memory slot store; 002 UI commits | **Treat as closed in source**; 03/08 own leftover pixels. |
| 062 GET checkout 404 before Bearer | Existence oracle | `Get` now 401 if missing Bearer **before** store lookup | **Closed.** Other-org still 404 after member deny (hides existence). |
| 063 invalid JSON Plane A after HMAC 500 | | try/catch → 400 `invalid event` | **Closed.** |
| 064 One 400/429 → Pay 503 | | MemberGate maps 400 and 429 | **Closed.** |
| 065 suspended copy “not a member” | | `SuspendedDetail` if One detail contains “suspend”; writer checks tenant status | **Partial.** Buyer pause is ChargesPaused, not this string. |
| 066 CORS tests not `/v1/pay` OPTIONS | | CorsTests grew (13 tests). Not method-audited here. | **Likely closed**; 06. |
| 067–074 spec lag | 22 vs 13 vs 11 | `main.tsp` has links, gateways, Test, slot_key, receipts, 201, unions, One timestamp header | **Mostly closed.** Kernel doors correctly absent. |
| 075 GET receipt by id untested unused | | Endpoint mapped; merchant receipts page lists. | **Partial.** |
| 076 unversioned `/ready` untested | | Mapped Postgres CanConnect. HealthTests may not cover it. | P2. |
| 077 InMemory not TX proof | | Still true **and** PostgresTxTests exist | **Honest both ways.** |
| 078 org ready dummy `ready: true` | Member ping only | Now `!paused && (vault \|\| AllowsTest)` | **Improved, not SST-ready.** |
| 079 remaining display clamps over-admit | | `RemainingUnclamped` for merchant; `over_capacity` status | **Closed.** |
| 080 CORS/compose laptop, no Pay image | | Dockerfile + compose.pay.yml; Production CORS required | **Closed as “no image.”** Root compose still Hub. Laptop CORS is Dev default. |

**002 miss list, compressed:** 014 definitely open in source; 023/030/029 leftover by design or half-fix; 019 slot grief mitigated; YAML vs body `Status: open` is a docs miss on **every** file that was not edited after resolve. Do not re-open 001–012 as if 002 never happened.

---

## 13.2 013 SHOULD / LATER / REFUSE vs live (so Bar C is not “forgotten”)

013 refused to hold Bar B hostage to renewals. Live must not accidentally ship Bar C by cathedral teleport, and must not claim Bar C.

| 013 item | Gate | Live |
|----------|------|------|
| NP-FUL-004 renew job | should C | **missing.** No hosted worker beyond in-request fulfill. |
| NP-FUL-005 honest PAST_DUE | should C | **missing.** Interval `one_off` on links. |
| NP-MON-003/004 SST | should C | **missing.** Honest README sentence exists. |
| NP-MON-005/006 refund / disputes | should C | **missing.** VIEWER cannot refund is vacuous. |
| NP-BUY-002–005 magic link portal | should C | **missing.** Checkout origin may share later; not built. |
| NP-MAIL-001–003 mail | should | Table only. |
| NP-SOON-001–008 quotes, partial refunds, **M2M checkout**, second gateway after two boring | soon | Five gateways already shipped (inverted vs 013). M2M still soon. |
| NP-LAT-001 tax provider | later | IsolationTests ban Lhdn. |
| NP-LAT-003 entitlement grant second app via HTTP | later | That **is** kernel Plane C + maybe a grant row. Not built. 013 put it in later; 018/020 want it earlier for *apps*. Sequence §8.1 is allowed to pull LAT-003 / SOON-007 forward **without** pulling LHDN. |
| NP-XX-001–024 | refuse | Holds in IsolationTests and product copy. |

013 anti-goals still true on this SHA (selected):

- No ops retarget (CORS deny 3003; compose comments).
- No `POST /one/auth/login` on Pay.
- No cookie JWT.
- No bind 8080.
- No merchants to `:5173`/`:3005` in nav.
- No tax-invoice title.
- No MediatR.
- No `lazuar-notify` process.
- Health does not call One.

013 anti-goal “do not count fixture `status: open` as paid” — **no longer the live lie.** Paid is a fulfill write. Do not quote 013 §2.12 “We write a receipt = No” as if it were this SHA.

---

## 13.3 Integrator transcript (what a stranger actually types)

Assume they cloned nothing, have a laptop, and believed a README that said “payments API.”

```bash
# 1. Token. They look for lzr_sk_ in Pay docs. Pay README says copy One access_token.
# There is no Pay mint. One mint is a different repo.
curl -sS -H "Authorization: Bearer lzr_sk_test_deadbeef" \
  http://localhost:8081/v1/whoami
# Likely 401/403/503 depending on One. No Pay test vector. Dies as a product.

curl -sS -H "Authorization: Bearer $HUMAN_ACCESS_TOKEN" \
  http://localhost:8081/v1/whoami
# Works if One is up. This is Bar A.

# 2. Mint. Spec wants provider. Writer only.
curl -sS -H "Authorization: Bearer $HUMAN_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"org_id":"'"$ORG"'","amount":10,"currency":"MYR","provider":"stripe"}' \
  http://localhost:8081/v1/checkouts
# 400 rail not configured unless Ada already PUT /gateway.
# 403 if the token is a member.
# 201 if writer + vault. public_token is a Pay URL, not a Stripe cs_ live URL.

# 3. Start. No Bearer. Payment-link tokens need slot_key.
curl -sS -X POST http://localhost:8081/v1/pay/$TOKEN/start \
  -H "Content-Type: application/json" \
  -d '{"name":"Ada","email":"ada@acme.test","slot_key":"slot-aaaa-1"}'
# 200 { redirect_url } or 409 full or 400 email. This is the cashier.

# 4. Learn paid.
curl -sS http://localhost:8081/v1/pay/$TOKEN?slot_key=slot-aaaa-1
# Poll status. No webhook arrives at the stranger's server.
```

Hub sample still:

```text
Authorization: Bearer ${key}   # sk_
events: payment.completed
```

That transcript is **false** against 8081. If 09-spec prints it under Pay, that is a marketing lie.

GET checkout-by-id is not a public learn-paid door: missing Bearer is 401; unknown id 404; other org 404. The **public** door is `/v1/pay/{token}`.

---

## 13.4 First-party dogfood transcript (what we *can* run)

This is the path that exists. It is not the integrator path.

1. One up on 8080, Zitadel 8085, login 5175. Hub compose **off**.
2. Pay Postgres 5435. `Pay:WrapKey` set. `task pay:dev`.
3. Merchant `:5178` with `VITE_ZITADEL_CLIENT_ID` registered on One. PKCE. `pickApiBearerToken` sends access_token.
4. Create workspace → One `POST /tenants`.
5. Processor page: paste Stripe `sk_test` + `whsec` or CHIP secret + PEM + brand id. Writer only. Member sees list, cannot PUT.
6. Pay links dialog: amount typed, capacity “one” means start-reservation, provider from host list (Test only if Dev). Copy tells TTL.
7. Open `:5179/c/{public_token}` in a clean profile. No login form. Name/email. Start. Test: paid immediately, Official Receipt. Stripe: redirect, Plane B, poll verifying.
8. Receipts table shows `RCPT-`. Journal two lines. Webhook replay `{duplicate:true}`.
9. Second human: **leave merchant**, use One invite. Come back with member role. See payments. Cannot paste keys.

Missing from that loop vs 013 sentence: step 9 UX, live `tenant.suspended` from One, production CORS origins, 014 on CHIP, SST honesty if they sell tax.

That loop **never** notifies another process. Aura does not unlock. That is Bar K.

---

## 13.5 Secret-key / inbound / outbound / clean API / M2M — restated without collapsing

**Secret key.** Two families must not share a name in a README:

| Object | Where | This SHA |
|--------|-------|----------|
| One `lzr_sk_` | Sibling One mint, tenant-bound, hashed | Pay does not mint, does not document scopes, does not test |
| Stripe `sk_test_` / CHIP secret | Pay vault, `SecretBox` | BYOK, never used as Pay Authorization |
| Hub homemade `sk_test_` / `sk_live_` | Museum `one.ApiCredentials` | Must not return |
| Process `Pay:OneWebhookSecret` | Pay env | One-shop HMAC fallback, not an API key |
| Process `Pay:TestWebhookSecret` | Testing/Dev | Signs Test Plane B |
| `Pay:WrapKey` | Pay env | AES-GCM, not a Bearer |

Calling any of those “the secret key” is how god-key happens.

**Inbound webhooks.** Plane A and Plane B are **live**. They are how Pay learns. They are not how an app learns.

**Outbound webhooks.** Plane C absent. Hub docs that list `payment.completed` are Hub docs.

**Clean `/v1`.** Bezos prefix is real (`/v1` not `/api/v1`). Snake_case JSON. Problem details. Writer vs member. Public pay vs staff. Spec closer to host than in 019. Still a cashier surface.

**M2M.** NP-SOON-007 / NP-API-004’s parenthetical `(One user JWT or lzr_sk_)`. The parenthetical is **false** for `lzr_sk_` as a Pay product. User JWT is true for the SPA. Do not “fix” M2M by stuffing the staff token in a GitHub Action.

---

## 13.6 Four processes before a second caller (refuse, unpacked)

A well-meaning launch checklist:

1. Boot One (API + Zitadel + OpenFGA + login) — identity.
2. Boot Pay 8081 + Postgres 5435 — money.
3. Boot merchant 5178 — staff.
4. Boot checkout 5179 — buyer.

That is **first-party cashier topology**. It is necessary. It is not sufficient for a second app. The second app is not process 5 as another Vite. It is:

5. A **machine identity** (One `lzr_sk_` with a Pay-documented check).
6. A **callback URL** Pay POSTs when fulfill commits.
7. A **sample** that is neither 5178 nor 5179.

Staffing 1–4 and cutting a “Pay is live” tweet is the four-process refuse. 006-sample already did 5–7 **against Hub 8080**. Repeating 006 against Hub does not move 020.

---

## 13.7 Predicted fights over “is occupancy still P0?”

If 07-money-remaining still prints 019’s paragraph “two slot_keys both get RCPT-,” they are citing a SHA that is not this SHA. Counter-cite:

- `PublicPayEndpoints.MintOrResume` transaction + `LockParentAsync`
- `PaymentLinkTests.Concurrent_start_on_one_person_link_admits_one_psp` (`Psp.SendCount == 1`)
- `PaymentLinkTests.Concurrent_test_start_on_one_person_link_mints_one_receipt` (documents == 1)
- `PostgresTxTests.Concurrent_starts_on_one_seat_leave_one_open`

If they say “InMemory ignores FOR UPDATE, therefore still P0,” the honest reply is: Production is Npgsql; the Postgres test exists and skips without Docker; rank the **skip** as CI ops, not as count-then-insert.

If they say “start still occupies before paid, therefore 004 is open,” the copy **changed**. Rank as product rule, not lie.

---

## 13.8 GET checkout Bearer order (062) — cited so 01 does not reopen it

```112:128:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
    static async Task<IResult> Get(
        string id,
        HttpRequest request,
        OneClient one,
        CheckoutStore store,
        CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out _))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        var session = await store.GetAsync(id, cancellationToken);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }
```

002/062 is closed as “404 before Bearer.” Cross-org still maps member-deny to 404 so the id is not an oracle **after** a token exists. 01 may still want 403 vs 404 law; that is P2, not an existence-before-auth hole.

---

## 14. Coordinates (repeat)

- **Date:** 2026-08-28  
- **HEAD:** `6d730d15` (`6d730d155c871465c35c192cf7730bfd270b47fa`)  
- **Branch:** `fix/002-pay-host-bugs`  
- **013 bar SHA (historical):** `6f866ff0`  
- **019 eval SHA (historical):** `9f04ad58`  
- **Sibling 01–09 / parent 00:** not on disk at write time  
- **Authority:** live files under `apps/lazuar-pay`, `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout`, `packages/pay-spec`, tests cited above  

This file is the ranked honesty paper. It does not implement. It does not flip 011/11. It does not retarget Hub compose. It does not add MediatR.

---

## 15. Closing

002 made the **hosted cashier honest about last-seat, Test, Stripe unpaid, unique receipts, and One HMAC packaging**. That was real work. It did not make Pay production-ready under 013. It did not open the Bezos door for a second caller. Occupancy is no longer the thing to say in a board sentence. The thing to say is:

**Pay takes hosted-link money for One shops on a laptop path, and cannot tell another app that the money arrived.**

Until that sentence is false, READMEs that sound like Stripe are lies. Until the 013 sentence is **run** (invite, real rail, live pause, WrapKey, CORS), READMEs that sound like production are also lies. Both lies are available on this SHA if we get lazy. This paper exists so we do not.
