# 09 — Spec, docs, SDK, second-app sample (integrator DX)

**Program:** 020-evals  
**Slice:** Contracts, docs, SDK, second-app sample — integrator DX.  
**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`.  
**Date:** 2026-08-28  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Branch:** `fix/002-pay-host-bugs`  
**HEAD:** `6d730d15` — `fix(pay): store per-org One webhook secrets`  
**Sibling:** `/Users/akmalfirdaus/Code/lazuar/lazuar-one`  
**Index:** [README.md](./README.md)

Authority on this SHA is live files. Historical papers ([012-one-to-pay](../012-one-to-pay/README.md), [013-prods](../013-prods/README.md), [006-sample](../006-sample/README.md), [011-new-lazuar-pay/08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md), [019-evals/08-contracts-spec-honesty.md](../019-evals/08-contracts-spec-honesty.md)) are named when they still describe the host, and named as stale when they do not. If they disagree with live files, live files win.

Standing law this report does not weaken:

- One Pay binary, one Pay database. Bezos is the **door** (`/v1`); Linux is the **room** (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path.
- Steal HTTP **judgment** from Hub; Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`).

---

## 0. Verdict (read this, then the evidence)

Pay on this SHA is a **hosted cashier** with a **path-honest TypeSpec** and a **CI scrape that currently exits 0**. It is **not** a kernel a stranger can swallow without cloning this repo.

A second app cannot:

1. mint a machine key **on Pay** (Pay does not mint keys; One mints `lzr_sk_`; Pay does not accept that prefix as a Pay `/v1` writer);
2. register a Pay→app outbound webhook for `payment.completed` (Pay has no Plane C dispatcher);
3. copy a Pay sample under `examples/` (there is none; the only sample is Hub museum);
4. read a Pay page in sibling One docs (Pay is not documented there as Consumer-0);
5. `npm i` a Pay client (there is no `@lazuar/pay-client`, no `@repo/pay-types-ts`, and waiting on unpublished `@lazuar/one-client` is refused).

What **does** exist:

- `packages/pay-spec/main.tsp` — ten interfaces, 22 `/v1` operations, namespace `LazuarPay`, server `http://localhost:8081`.
- Live honesty: `Pay OpenAPI honesty: 22 spec ops, 24 Map* (2 host-only probes).` exit 0, run on this SHA after the already-compiled `packages/pay-spec/dist/openapi.yaml`.
- `IMPL_ONLY = { GET /health, GET /ready }` in `scripts/check-pay-openapi-honesty.mjs`.
- Dist is gitignored (root `.gitignore` `dist/` **and** `packages/pay-spec/.gitignore` `dist/`). `git ls-files packages/pay-spec/dist` is empty. `git check-ignore -v packages/pay-spec/dist/openapi.yaml` → `packages/pay-spec/.gitignore:6:dist/`.
- First-party merchant (`:5178`) and checkout (`:5179`) talk to `/v1` with **plain `fetch`**. They do not import `@repo/api-types-ts`. IsolationTests and Vite honesty locks would go red if they did.
- Host README has one human-JWT curl for `POST /v1/checkouts`. That is the only mint recipe a stranger can find **in Pay product docs**. It is not a machine-key recipe. It is not a webhook recipe. The merchant SPA does **not** call `POST /v1/checkouts` at all — it mints `POST /v1/payment-links`.

Parent problem this slice owns: **what is missing so another app can integrate without cloning this repo — docs/sample, plus the honesty of the `/v1` contract they would call.** Machine keys and outbound webhooks are sibling slices (02, 03). This file names them as **blockers for DX**, not as work this slice would implement.

**How to solve (rank, sequence at the end of this file):** honesty already in CI — keep it; grow TypeSpec when kernel doors land, not before; one `examples/pay-node` that uses `lzr_sk_` + poll or webhook; one docs page “second app”. Do not wait on npm `@lazuar/one-client` (NP-XX-021). Plain fetch is the hatch.

---

## 1. Coordinates and files opened

| | |
|--|--|
| Focused host | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay` listen **8081** |
| TypeSpec | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp` |
| tspconfig | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/tspconfig.yaml` → `{project-root}/dist/openapi.yaml` via `@typespec/openapi3` |
| Local OpenAPI leftover | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/dist/openapi.yaml` (present on disk, **not tracked**) |
| Honesty scrape | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/check-pay-openapi-honesty.mjs` |
| Hub honesty (museum) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/scripts/check-openapi-minimal-honesty.mjs` scrapes `apps/lazuar-api` |
| CI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml` jobs `contracts` (Hub) and `pay` (focused host + tsp compile + Pay honesty) |
| Task | `Taskfile.yml` `pay:spec` = `pnpm exec tsp compile .` then `node ../../scripts/check-pay-openapi-honesty.mjs` |
| Isolation | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` |
| Hub types museum | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-types-ts` |
| Hub spec museum | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec` |
| Hub sample museum | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next` |
| Hub docs museum | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs` |
| One docs (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs` |
| One samples (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one/examples` |
| One client (unpublished) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/one-client` (`private: true`) |

Live counts on this SHA (path-level, methods distinct):

| Surface | Operations |
|---------|------------|
| Live `MapGet\|Post\|Put` under `apps/lazuar-pay/src` | **24** (22 under `/v1`, plus unversioned `GET /health` and `GET /ready`) |
| `packages/pay-spec/main.tsp` interfaces | **10** |
| `packages/pay-spec/main.tsp` operations | **22** (all `/v1`) |
| Compiled OpenAPI `paths` verbs | **22** |
| Honesty `IMPL_ONLY` | **2** (`GET /health`, `GET /ready`) |
| Honesty exit | **0** (`22 spec ops, 24 Map* (2 host-only probes)`) |
| Merchant SPA Pay fetches | whoami, gateways, gateway PUT, payment-links list/create, products POST, payments list, receipts list. **Zero** `POST /v1/checkouts`. |
| Checkout SPA Pay fetches | `GET /v1/pay/{token}?slot_key=`, `POST /v1/pay/{token}/start` |
| `@repo/pay-types-ts` | **does not exist** |
| Pay npm client | **does not exist** |
| Pay `examples/pay-*` | **does not exist** |
| `lzr_sk_` in Pay C# | **zero matches** under `apps/lazuar-pay` |
| `payment.completed` / Plane C in Pay C# | **zero matches** under `apps/lazuar-pay` |
| OpenAPI `securitySchemes` / `BearerAuth` / `'401'` / `'403'` | **all false** on compiled yaml |

019-evals/08 on HEAD `9f04ad58` counted **22 Map\*** vs **13 tsp** vs **11 stale dist**. 002 issues 067–074 closed the path/field subset that honesty now asserts. This SHA is a different world at the **path** layer. It is the same world at the **kernel DX** layer: no M2M, no outbound Pay events, no Pay sample, no Pay docs page.

---

## 2. `packages/pay-spec/main.tsp` — every interface and every route

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp` (431 lines).

Header:

```tsp
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
namespace LazuarPay;
```

Comment on line 7: “Focused Pay HTTP contract. Not packages/api-spec. Checkouts persist in Postgres; paid via verified PSP webhook.” That sentence is **true on this SHA**. 014/10 and 019/08 quoted the old “Checkout is a fixture” lie. 002 closed it. Do not revive that accusation.

Package: `@repo/pay-spec` `0.1.0` `private: true`. Scripts: `build` = `tsp compile .`. README: “Do not import One, LHDN, or `/public/commerce` routes here.” “Grow `main.tsp` when a Pay `/v1` door exists.” “Unversioned `/health` and `/ready` stay host-only.”

`tspconfig.yaml` emits OpenAPI 3 to `dist/openapi.yaml`. There is **no** TypeSpec `@auth` / `@useAuth`. There is **no** `security` block. There is **no** Problem Details model. Integrators who codegen from this yaml get 200/201 happy paths only.

### 2.1 Models (what the spec claims the wire is)

| Model | Role | Notes vs host |
|-------|------|----------------|
| `HealthResponse` | `{ status }` | Host anonymous `{ status = "ok" }` / `{ status = "ready" }` / 503 `{ status = "not_ready" }` |
| `WhoamiTenant` | id, slug?, name?, role?, status? | Projection of One `GET /me` tenants |
| `WhoamiResponse` | user_id, email?, **name?**, is_platform_admin, active_org_id?, tenants[] | `name` was 002-074; now in tsp. Host `WhoamiResponse.Name` + snake_case JSON. Mapper copies One `me.name`. |
| `OrgReadyResponse` | org_id, ready | Member ping + charges-not-paused + vault or Test |
| `CreateCheckoutRequest` | org_id, **provider**, product_id?, amount, currency?, success_url?, cancel_url?, idempotency_key? | 002-070: provider required. Host has no default; unknown → 400 |
| `CheckoutSession` | full session including slot_key, payment_link_id, payer_* | Matches `CheckoutSession.cs` property set under snake_case |
| `CheckoutListItem` | list row + optional label | Host list is **one-off mints only** (`PaymentLinkId == null`) — see §4.2 |
| `PublicPay` | buyer view including occupancy counters | Checkout SPA `PayView` is a **subset** (no remaining/max_payers/paid_count/taken_count in the TS type; host still sends them) |
| `StartPayRequest` | name?, email?, **slot_key?** | 002-071. Required on payment-link tokens at runtime; optional in tsp (runtime 400) |
| `GatewayView` / `GatewayList` / `PutGateway` | BYOK | PUT test → 400. key_id+key_secret concat. Five rails + Test outside Production |
| `PutOneWebhook` / `OneWebhookView` | per-org One `whsec_` | Process `Pay:OneWebhookSecret` is one-shop fallback (HEAD commit is this store) |
| `CreateProductRequest` / `CreatedProduct` / `Product` / `ProductPrice` | catalog | Currency other than MYR → 400 on create |
| `StartPayResponse` | `{ redirect_url }` | Checkout SPA treats missing redirect_url as error copy |
| `CreatePaymentLinkRequest` / `PaymentLink` | occupancy mint | No Idempotency-Key on this door (tsp comment true) |
| `Payment` / `Receipt` | merchant money reads | Receipt `number` can be `"PENDING"` on the wire when `Documents.Number` is null — tsp does not say so |
| `WebhookOk` / `WebhookDuplicate` / `WebhookIgnored` | Plane B 200 bodies | 002-073: unions, not a required `{ok:true}` on every 200 |
| `PspWebhookResult` / `OneWebhookResult` | unions | One inbound has no `ignored` arm |
| `OneWebhookEvent` | type?, id?, org_id?, tenant_id? | Plane A body; HMAC headers optional in tsp |

Honesty field checks (script, not tsp comments) currently assert:

- `CreateCheckoutRequest.provider`
- `StartPayRequest.slot_key`
- `WhoamiResponse.name`
- `CreateProductRequest` schema exists
- `WebhookDuplicate.duplicate`
- `WebhookIgnored.ignored`
- yaml contains `'201':` (mint status)

Those seven are **regression pins for 002-069–074**, not a full JSON contract.

### 2.2 Interfaces and routes — tsp vs Map* vs OpenAPI

Every tsp `@route` is under `@route("/v1")` on the interface. Compiled OpenAPI paths therefore start with `/v1`. Unversioned probes are **not** in tsp, by design.

| Interface | Tag | Method | Path | Map* file | In OpenAPI | In IMPL_ONLY |
|-----------|-----|--------|------|-----------|------------|--------------|
| `Health` | Health | GET | `/v1/health` | `Hosting/HealthEndpoints.cs` | yes | no |
| — | — | GET | `/health` | same | **no** | **yes** |
| — | — | GET | `/ready` | same | **no** | **yes** |
| `Session` | Session | GET | `/v1/whoami` | `Identity/WhoamiEndpoints.cs` | yes | no |
| `Orgs` | Orgs | GET | `/v1/orgs/{orgId}/ready` | `Identity/OrgReadyEndpoints.cs` | yes | no |
| `Checkouts` | Checkouts | POST | `/v1/checkouts` | `Checkouts/CheckoutEndpoints.cs` | yes | no |
| `Checkouts` | Checkouts | GET | `/v1/checkouts/{id}` | same | yes | no |
| `Checkouts` | Checkouts | GET | `/v1/orgs/{orgId}/checkouts` | same | yes | no |
| `PaymentLinks` | PaymentLinks | POST | `/v1/payment-links` | `PaymentLinks/PaymentLinkEndpoints.cs` | yes | no |
| `PaymentLinks` | PaymentLinks | GET | `/v1/orgs/{orgId}/payment-links` | same | yes | no |
| `PublicPayApi` | Pay | GET | `/v1/pay/{token}` | `PublicPay/PublicPayEndpoints.cs` | yes | no |
| `PublicPayApi` | Pay | POST | `/v1/pay/{token}/start` | same | yes | no |
| `Catalog` | Catalog | POST | `/v1/orgs/{orgId}/products` | `Catalog/CatalogEndpoints.cs` | yes | no |
| `Catalog` | Catalog | GET | `/v1/orgs/{orgId}/products` | same | yes | no |
| `Gateways` | Gateways | PUT | `/v1/orgs/{orgId}/gateway` | `Credentials/GatewayEndpoints.cs` | yes | no |
| `Gateways` | Gateways | GET | `/v1/orgs/{orgId}/gateway` | same | yes | no |
| `Gateways` | Gateways | GET | `/v1/orgs/{orgId}/gateways` | same | yes | no |
| `Money` | Money | GET | `/v1/orgs/{orgId}/payments` | `Money/Queries/PaymentQueryEndpoints.cs` | yes | no |
| `Money` | Money | GET | `/v1/orgs/{orgId}/receipts` | same | yes | no |
| `Money` | Money | GET | `/v1/orgs/{orgId}/receipts/{id}` | same | yes | no |
| `Webhooks` | Webhooks | POST | `/v1/webhooks/{provider}/{orgId}` | `Webhooks/WebhookEndpoints.cs` | yes | no |
| `Webhooks` | Webhooks | POST | `/v1/one/webhooks` | `Identity/OneWebhooks/OneWebhookEndpoints.cs` | yes | no |
| `Webhooks` | Webhooks | PUT | `/v1/orgs/{orgId}/one-webhook` | same | yes | no |
| `Webhooks` | Webhooks | GET | `/v1/orgs/{orgId}/one-webhook` | same | yes | no |

Grep of `\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]+)"` under `apps/lazuar-pay/src` returned **exactly those 24 string literals**. No `MapDelete`. No `MapPatch`. No `MapGroup` prefix that the scraper would miss. Program.cs composition root calls `MapHealth`, `MapWhoami`, `MapOrgReady`, `MapCheckouts`, `MapPaymentLinks`, `MapCatalog`, `MapPublicPay`, `MapGateways`, `MapWebhooks`, `MapPaymentQueries`, `MapOneWebhooks`. That is the whole door.

### 2.3 What tsp comments say the doors require (auth is comments, not OpenAPI security)

| Door | tsp comment | Live gate |
|------|-------------|-----------|
| POST `/v1/checkouts` | Bearer + writer. 201 on mint; 200 on idempotent replay | `MemberGate.RequireWriterAsync`. `Idempotency-Key` header or body `idempotency_key`. 409 on body conflict. |
| POST `/v1/payment-links` | Bearer + writer. No Idempotency-Key | Writer. Occupancy fields `max_payers` / `unlimited` |
| POST products | Bearer + writer. non-MYR → 400 | Writer |
| PUT gateway | Writer. PUT test → 400 | Writer. `PayProviders` |
| PUT/GET one-webhook | Writer stores shop’s One `whsec_` | Writer / member |
| GET whoami | Requires Bearer. Not Hub `/one/auth/me` | Bearer forwarded to One `GET /me` |
| GET org ready | Member ping | `RequireMemberAsync` then vault/pause/Test |
| GET checkouts/{id} | Member of that checkout’s org | Member. Other org → 404 (not 403) after 002-062 class of work |
| GET pay/{token} | public | No Bearer. `slot_key` query resumes a link child |
| POST pay/{token}/start | public. Link tokens require slot_key 8–128 or 400 | No Bearer |
| POST webhooks/{provider}/{orgId} | Plane B. 200 is `{ok}` / `{duplicate}` / `{ignored}` | HMAC / rail verify, not Bearer |
| POST /v1/one/webhooks | Plane A. `X-Lazuar-Signature` + `X-Lazuar-Timestamp` | HMAC. Per-org secret from PUT one-webhook |

**None of those Bearer rules appear as OpenAPI `security`.** A generated client will not attach `Authorization`. A stranger reading Scalar (if someone later hosted this yaml) would not see 401.

Plane B signature headers (Stripe `Stripe-Signature`, CHIP, Billplz `X-Signature`, Xendit callback token, Razorpay HMAC) are **not in tsp**. That is acceptable for a PSP-facing door — integrators do not call it — but it means the “spec is the host” claim is **path-level**, not header-level.

`X-Lazuar-Tenant-Id` is a live hint on merchant fetches (`payApi.ts` sets it when `orgHint` is passed). tsp does not declare it. One’s rule (011/02): hint only, never authorize by header alone. Pay `MemberGate` still calls `authz/check` with path `{orgId}`. Spec silence here is not a security hole; it is missing integrator documentation of a header the first-party SPA sends.

---

## 3. Honesty script rules, CI, compile dist gitignored

### 3.1 `scripts/check-pay-openapi-honesty.mjs`

Live file, 162 lines. Header:

```
 * Pay OpenAPI ↔ Minimal API path honesty (packages/pay-spec, not Hub api-spec).
 *
 * Asserts:
 *   1. OpenAPI paths ⊆ MapGet|Post|Put under apps/lazuar-pay/src
 *   2. Map* ⊆ OpenAPI ∪ host-only allowlist (unversioned /health /ready)
```

Constants:

```js
const OPENAPI_PATH = path.join(ROOT, "packages/pay-spec/dist/openapi.yaml");
const SCAN_ROOT = path.join(ROOT, "apps/lazuar-pay/src");
const IMPL_ONLY = new Set(["GET /health", "GET /ready"]);
```

Scrape regex:

```js
const mapRe = /\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]+)"/g;
```

It walks `*.cs` under `apps/lazuar-pay/src`, skips `bin`/`obj`. It does **not** scan tests. It does **not** scan Vite apps. It does **not** expand `MapGroup`. On this host that is fine: every door is a full-path `Map*` string.

OpenAPI parse is a **line-oriented yaml scrape**, not a YAML library:

- enter `paths:`
- take `  /foo:` as current path
- take `    get:` / `post:` / `put:` / `delete:` / `patch:` as verbs
- stop when a top-level key (`^[A-Za-z]`) appears (`components:` ends the walk)

That is enough for the emitted `@typespec/openapi3` shape on this SHA. It would miss `$ref` path items, YAML aliases, or quoted path keys. Do not treat the scraper as a general OpenAPI linter.

Fail modes:

1. `extraSpec` — OpenAPI path not mapped on the host.
2. `missingSpec` — Map* not in OpenAPI and not in `IMPL_ONLY`.
3. `allowlistedButMappedInSpec` — `GET /health` or `GET /ready` **also** in OpenAPI. Host-only probes must stay out of pay-spec.
4. Field checks listed in §2.1.

Missing `dist/openapi.yaml` exits 1 with “Run `task pay:spec` … first.” Dist being gitignored means **CI must compile before honesty**. That is exactly what CI does.

Live run on this SHA (working tree already had a compiled dist leftover):

```
Pay OpenAPI honesty: 22 spec ops, 24 Map* (2 host-only probes).
EXIT:0
```

Python recount of yaml verbs: 22 operations, listed in §2.2. `securitySchemes` / `BearerAuth` / `'401'` / `'403'` all absent.

### 3.2 Dist is gitignored. Honesty does not dirty-check yaml.

`packages/pay-spec/.gitignore`:

```
tsp-output/
dist/
```

Root `.gitignore` also has `dist/`. Double ignore. `git ls-files packages/pay-spec/` is:

- `.gitignore`
- `README.md`
- `main.tsp`
- `package.json`
- `tspconfig.yaml`

No yaml in git. 002-067’s “stale leftover on disk vs tsp vs host” is **structurally still possible** on a laptop that compiled an old tsp and never recompiled. CI does not have that leftover: it checks out git (no dist), compiles, then scrapes. That is the correct pairing.

What CI does **not** do:

- commit OpenAPI and `git diff --exit-code` it (Hub `contracts` job does that for `packages/api-types-ts/src` etc.; Pay is excluded on purpose);
- generate TypeScript/C# clients from pay-spec;
- compare SPA hand-written types to OpenAPI;
- compare error bodies to a Problem schema;
- compare comments in tsp to host filters.

019-evals/08 wanted “pick: gitignore dist **or** commit it and dirty-check.” This SHA picked **gitignore + compile-then-scrape**. That is honest. It is not a substitute for a generated client.

### 3.3 `task pay:spec` vs CI job `pay` vs Hub `contracts`

`Taskfile.yml` `pay:spec`:

```
dir: packages/pay-spec
cmds:
  - pnpm exec tsp compile .
  - node ../../scripts/check-pay-openapi-honesty.mjs
```

`.github/workflows/ci.yml` job `pay` (lines 96–120):

1. checkout, dotnet 10, node 22, pnpm 11.5.2
2. `pnpm install --frozen-lockfile`
3. `dotnet test apps/lazuar-pay/Lazuar.Pay.slnx`
4. `pnpm --filter lazuar-pay-merchant build` and checkout build
5. `pnpm --filter @repo/pay-spec exec tsp compile .`
6. `node scripts/check-pay-openapi-honesty.mjs`

Honesty is **already in CI**. 013-prods/10 once said `pay:spec` was optional / “when pay-spec grows beyond health.” Live files won. Do not re-open “should honesty run.” It runs.

Hub job `contracts` (same workflow, different job):

- `task gen --force`
- dirty-check `packages/api-types-ts/src`, `packages/api-types-dotnet/Generated`, LHDN SDKs
- `node scripts/check-openapi-minimal-honesty.mjs` against **`apps/lazuar-api`** and `packages/api-spec`

012/04 rule still holds: **do not** hook `packages/pay-spec` into `task gen`, Hub honesty allowlist, or the `contracts` dirty-check. Mixing hosts would force allowlist lies. Isolation of the two honesty scripts is a feature.

### 3.4 What the scraper cannot see (remaining honesty holes, not path drift)

These are **not** “OpenAPI paths not mapped.” They are the next class of lie after 002 path-closed.

1. **Comments vs code.** `Checkouts.list` tsp: “Member list. Mixes one-off mints and payment-link children.” Host `CheckoutEndpoints.List`: `.Where(x => x.OrgId == orgId && x.PaymentLinkId == null)`. Issue 002-031 was that mix. Host was fixed. Spec comment was **not**. Honesty does not read comments.
2. **Auth.** No OpenAPI security. Host 401s without Bearer on whoami and writer/member doors.
3. **Errors.** Host `PayErrors.Status` returns `{ status, title, detail }` JSON (`PayProblem`). tsp never names that shape. 400/401/403/404/409/429/503 are live. yaml has no `'401'`.
4. **Optional vs runtime-required.** `StartPayRequest.slot_key` is optional in tsp; payment-link start without it is 400. Field existence is pinned; cardinality is not.
5. **List vs merchant SPA.** Spec has `GET /v1/orgs/{orgId}/checkouts`. Merchant never calls it. Merchant lists payment-links. A kernel client that copies the merchant SPA will never discover the checkout list door.
6. **Receipt PENDING.** Host emits `number = d.Number ?? "PENDING"` and `status = string.IsNullOrWhiteSpace(d.Number) ? "pending" : "issued"`. tsp `Receipt.number: string` required, `status?` optional. The string `"PENDING"` is a protocol, not a missing field.
7. **Whoami `active_org_id`.** Mapper sets `ActiveOrgId = me.ActiveTenantId`. tsp uses Pay’s word `org`. Honest projection. Not in Hub types.
8. **No DELETE/PATCH.** Scraper would catch them if they appeared. They have not. Refunds, revoke, pause are **missing features**, not drifted paths.

Path-level remaining drift vs Map*: **none**, given IMPL_ONLY. Semantic remaining drift: **yes**, table in §4.

---

## 4. Remaining drift vs Map* (after honesty green)

### 4.1 Path set

| Map* | Spec | Notes |
|------|------|-------|
| GET `/health` | absent | IMPL_ONLY. Process liveness. Duplicate of `/v1/health` body `{status:ok}`. |
| GET `/ready` | absent | IMPL_ONLY. Postgres `CanConnect`. 503 `{status:not_ready}`. **Not** org ready. |
| GET `/v1/health` | Health.check | Duplicate liveness under `/v1` so load balancers that only scrape versioned trees still work. |
| 21 other `/v1` Map* | 21 tsp ops | 1:1 |

No extraSpec. No missingSpec. No allowlisted-but-in-spec. That is the 002-067 fix holding.

### 4.2 Semantic / comment / error / auth drift (honesty-green lies)

| # | Side that is wrong | Evidence |
|---|--------------------|----------|
| S1 | tsp comment | `Checkouts.list`: “Mixes one-off mints and payment-link children.” Host filters `PaymentLinkId == null` (`CheckoutEndpoints.cs` List). 002-031 host fix landed; comment did not. A stranger who trusts the comment double-counts occupancy children against `GET …/payment-links`. |
| S2 | tsp (missing) | No `security` / Bearer. Host `Bearer.TryGet` 401 “Missing bearer token”. |
| S3 | tsp (missing) | No `PayProblem` `{status,title,detail}`. Merchant `problemDetail()` and checkout `readDetail()` parse it. Generated clients will not. |
| S4 | tsp (missing) | No 401/403/404/409/429/503 responses. POST checkouts 409 “idempotency key reused with a different body” is a real money door. |
| S5 | tsp (weak) | `StartPayRequest.slot_key` optional. Host: payment-link tokens require 8–128. Honesty only checks the field **exists**. |
| S6 | tsp (missing) | `X-Lazuar-Tenant-Id` hint used by merchant `payApi.ts` and forwarded by `OneClient` to One. |
| S7 | tsp (missing) | Writer vs member. tsp says “writer” in some comments, not a role enum. Host `RequireWriterAsync` is One `/me` role overlay `owner|admin` (002-030 still names this as overlay, not `authz/check admin`). Spec does not mention `member` read-only. |
| S8 | tsp (missing) | `lzr_sk_` is not a Pay scheme. One documents it. Pay MemberGate forwards whatever Bearer it got. One `authz/check` **requires `user_id` when authenticating with an API key** (One recipe R2). Pay `OneAuthzCheckRequest` has **no `user_id`**. A stranger who pastes `lzr_sk_` into Pay `/v1` will not get a documented 400; they will get whatever One returns, mapped to Pay 400/401/403/503. |
| S9 | tsp (correct absence) | No outbound `payment.completed`. Host has no dispatcher. Growing tsp for a door that does not exist would be the 019 sin in reverse. |
| S10 | SPA vs spec | Merchant hand-written `Whoami`, `PayLink`, `Payment`, `Receipt`. Checkout `PayView` omits occupancy counters the host sends. Drift is one-way: SPA is a subset. Isolation-correct. DX-weak: the types a stranger would copy are in the SPA, not in a package. |
| S11 | README vs SPA | Host README curls `POST /v1/checkouts`. Merchant SPA curls `POST /v1/payment-links`. Both are live doors. The **product UI** does not demonstrate the Bezos mint the README advertises. |
| S12 | Root README | Root `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md` does not mention `lazuar-pay`, `:8081`, `:5178`, `:5179`, or `pay-spec`. Grep of those tokens in the root README: **no matches**. A stranger opening the repo lands in Hub CaaS. |

S9 is **not a bug**. It is a missing feature that honesty correctly refuses to invent. S1 is a **spec comment bug** (host is the product). S2–S8 are **missing contract surface**, not path drift. S10 is isolation. S11–S12 are docs.

### 4.3 002 issues 067–076 vs this SHA

| Issue | 019 claim | This SHA |
|-------|-----------|----------|
| 067 dist stale vs tsp | 11 yaml / 13 tsp / 22 Map* | yaml 22 / tsp 22 / Map* 24 with 2 IMPL_ONLY. CI compile-then-scrape. Dist untracked. **Path-closed.** Laptop leftover still possible. |
| 068 GET gateway without provider | list envelope | tsp: “Singular requires `provider`. Missing query is 400.” Honesty does not pin the 400. Host must still be the product (sibling slice). |
| 069 catalog create no body | tsp had no CreateProductRequest | Model exists; honesty pins schema presence. |
| 070 CreateCheckoutRequest omits provider | required now | honesty pins `provider:` in schema. |
| 071 slot_key | in StartPayRequest | honesty pins `slot_key:`. Cardinality still optional. |
| 072 spec 200 vs host 201 | tsp now 201\|200 on checkouts; 201 on payment-links and products | honesty pins `'201':` anywhere in yaml, not per-op. |
| 073 webhook `{ok}` | unions WebhookOk / Duplicate / Ignored | honesty pins `duplicate` and `ignored` fields. |
| 074 whoami name | in WhoamiResponse | honesty pins `name:`. |
| 075 GET receipts/{id} untested unused | still mapped; merchant SPA lists receipts, does not GET by id | spec includes it. Unused by first-party UI is not a path lie. |
| 076 unversioned /ready | IMPL_ONLY, host-only | as designed. |

002 README says 001–080 resolved on this branch. This slice does **not** re-litigate occupancy or HMAC. It records that **spec path honesty is a closed 002 bug** and **kernel DX is not**.

---

## 5. No `packages/api-types-ts` for Pay. Hub types are museum. Isolations.

### 5.1 Search: is there a Pay types package?

Workspace packages that look like generated API types:

| Package | Path | Generated from | Used by Pay host/SPAs? |
|---------|------|----------------|------------------------|
| `@repo/pay-spec` | `packages/pay-spec` | TypeSpec **source**, not a TS client | compile only |
| `@repo/api-types-ts` | `packages/api-types-ts` | Hub `packages/api-spec/dist/openapi.yaml` via `openapi-typescript` | **No** (IsolationTests + package.json locks) |
| `@repo/api-types-dotnet` | `packages/api-types-dotnet` | Hub spec (Kiota/NSwag via `task gen`) | **No** (csproj isolation) |
| `@repo/pay-types-ts` | — | — | **does not exist** (grep of package.json / cs / ts / yml: only `@repo/pay-spec`) |
| `@lazuar/pay-client` | — | — | **does not exist** |
| `@lazuar/one-client` | sibling `lazuar-one/packages/one-client` | One OpenAPI copy | **unpublished** `private: true`. Pay C# does not import it. Merchant Vite does not import it. |

`packages/api-types-ts/package.json`:

```json
"name": "@repo/api-types-ts",
"scripts": {
  "generate": "openapi-typescript ../api-spec/dist/openapi.yaml -o src/index.ts",
  "build": "pnpm generate"
}
```

`packages/api-types-ts/src/index.ts` header: “auto-generated by openapi-typescript. Do not make direct changes.” First paths:

- `/admin/billing/credits`
- `/admin/commerce/checkouts/{id}/mark-paid`
- `/integrations/payments/checkouts`
- `/lhdn/api-keys`
- …

That is the **modular monolith on :8080**, base `/api/v1` in Hub docs, **not** focused Pay `/v1` on :8081. `GET /integrations/payments/me` in that file is Hub homemade `sk_` introspect. New Pay must not grow that path. 012/08: new Pay does not mint `sk_test_` / `sk_live_` (prefix collision with Stripe).

Who still depends on Hub `@repo/api-types-ts` (package.json):

- `apps/lazuar-admin`
- `apps/lazuar-ops`
- `apps/lazuar-portal`

Those apps are **museum**. Standing law: steal HTTP judgment, not the types package.

Who does **not** depend on it:

- `apps/lazuar-pay-merchant/package.json` — oidc-client-ts, react-router, radix, **no** openapi-fetch, **no** api-types-ts
- `apps/lazuar-pay-checkout/package.json` — radix + react, **no** oidc, **no** api-types-ts
- `examples/hub-cashier-next/package.json` — next/react only (and that sample is Hub, not Pay)
- Pay `.csproj` — no ProjectReference to `api-types-dotnet`

012/04 timing rule: “Spin `@repo/pay-types-ts` only when a Pay client sets `baseUrl` to the Pay host.” **That condition is now true** for merchant and checkout: they call `/v1` for real. The package still does not exist. That is not a 012 contradiction if we keep the **other** 012 rule: do not generate until needed, and **do not** hijack `task gen`. The honest 020 reading: first-party SPAs chose **hand-written DTOs + fetch**. That is isolation-correct. It is also how DTO drift survives (014-evals/03 already said this for checkout). Generating `@repo/pay-types-ts` is **optional DX**, ranked **after** kernel doors and a sample that a stranger can run without the Vite apps.

### 5.2 IsolationTests — cathedral strings stay red

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

Banned on csproj text: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`.

Banned in every `src/**/*.cs`:

```
MediatR, Modules.One, BuildingBlocks, IPaymentGatewayAdapter, PaymentGatewayFactory,
IPaymentGatewayFactory, AddPaymentsModule, GatewayPaymentCompletedIntegrationEvent, Modules.Payments,
ApplicationFeeAmount, Razorpay.Api,
application_fee, TransferData, transfer_data,
ChipWebhookRegistrar, PublicDnsFallback,
Lhdn, MyInvois, UBL, XAdES, Irbm,
IEnumerable<IHostedRail>,
namespace Lazuar.Pay.Gateways,
namespace Lazuar.Pay.One;
```

Standing law names three strings. All three are in the test:

- `MediatR` — Banned + BannedSrc
- `IEnumerable<IHostedRail>` — BannedSrc
- Hub `@repo/api-types-ts` — test `Vite_apps_do_not_use_hub_types` reads merchant and checkout `package.json` and asserts they do not contain `@repo/api-types-ts`

Additional isolations:

- no `ToTable("organizations"|"users"|"members")` (Pay is not a second membership directory)
- no `apps/lazuar-api` in any Pay `*.csproj`
- no `Razorpay.Api` NuGet (the Hub package name collision)

Merchant `locks.test.ts`:

```
expect(pkg).not.toContain('@repo/api-types-ts')
expect(pkg).not.toContain('@repo/aura-ui')
expect(pkg).not.toContain('lazuar-ops')
```

Checkout `locks.test.ts`:

```
expect(pkg).not.toContain('oidc-client-ts')
expect(pkg).not.toContain('react-oidc-context')
expect(pkg).not.toContain('@repo/api-types-ts')
```

If someone “fixes DX” by `pnpm add @repo/api-types-ts` in merchant or checkout, **CI job `pay` goes red** (dotnet IsolationTests + Vite vitest in local `pnpm test`; CI currently **builds** merchant/checkout but the IsolationTests C# check of package.json still runs in `dotnet test`). That is the correct red.

pay-spec isolation is **social + README**, not a test that tsp does not import Hub namespaces. 012/04 noted IsolationTests do not assert TypeSpec isolation. Still true. `main.tsp` does not import `@repo/api-spec`. There is no tsp-level isolation test. Low risk: pay-spec is one file.

### 5.3 Hub types museum — what a stranger must not copy

Opening `packages/api-types-ts` and generating a Pay client from it would type-check:

- `POST /integrations/payments/checkouts` with `sk_test_`
- Hub `GET /one/auth/me`
- LHDN, dunning, credits, commerce subscribers

None of those exist on `:8081`. Pointing `VITE_API_URL` at 8081 was the 012/04 failure mode. Merchant README still says: “Do not depend on `@repo/api-types-ts` (Hub).” “Do not set `lazuar-ops` `VITE_API_URL` to 8081.”

`apps/lazuar-developers` README is still create-next-app (“localhost:3000”). Root README still lists it as “Scalar OpenAPI hub” for Hub. It is not a Pay docs site.

`apps/lazuar-docs` is Hub VitePress. Homepage hero: “Lazuar Hub”. Sample port **3020**. Keys `sk_test_` / `sk_live_`. That tree is **006-sample museum**, still in this git repo, still runnable, still the thing `pnpm example:cashier` starts. It is not Pay.

### 5.4 Hand-written clients that *are* Pay

Merchant `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/payApi.ts`:

- `payApi = import.meta.env.VITE_PAY_API_URL ?? 'http://localhost:8081'`
- `getWhoami` → `GET ${payApi}/v1/whoami` with Bearer access_token, optional `X-Lazuar-Tenant-Id`
- `payFetch` / `payJson` — generic, credentials omitted on purpose (“localhost cookies are not port-scoped”)
- Types `Whoami` / `WhoamiTenant` duplicated from tsp by hand

Merchant `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/src/lib/oneApi.ts`:

- `VITE_ONE_API_URL ?? 'http://localhost:8080/api/v1'`
- `POST /tenants` only
- **Out of pay-spec by design.** Workspace create is One’s door. 012/04: Pay TypeSpec must not copy `POST /tenants`.

Checkout `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/src/pay.ts`:

- origin from `VITE_PAY_API_URL`, DEV fallback localhost:8081, production **throws** if missing
- `payPath(token)` = `${payApi}/v1/pay/${token}?slot_key=…`
- slot_key persisted in localStorage/sessionStorage/memory

Checkout `App.tsx` `startPay`: `POST ${payApi}/v1/pay/${token}/start` JSON `{ name, email, slot_key }`. Parses 409 full, 503 rail, 400, missing `redirect_url`. Polls public GET while `?status=verifying`. **Never treats success_url as paid.** That judgment is steal-from-Hub and already in the pixel.

These two files are the closest thing Pay has to an SDK. They are not published. They import Vite env. A stranger cannot `npm i` them.

---

## 6. READMEs — can a stranger mint a checkout?

Question this section answers: if someone clones nothing but git, reads READMEs, and has One running, can they mint a Pay checkout and know when it is paid?

Short answer: **they can mint with a human JWT if they find the host README curl and already know how to get an access_token from One. They cannot do it as a second app. They will not find API keys, outbound webhooks, or a machine-key curl. The root README will send them to Hub.**

### 6.1 Root README — Hub museum, Pay invisible

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/README.md`

Title: “Lazuar Platform (Checkout-as-a-Service)”. Watermark table points **ADR 021/023** and `packages/api-spec` + `task gen`. Honest capability paragraph is Hub BYOK + ledger + dunning + LHDN.

Project structure lists:

```
apps/lazuar-api, lazuar-ops, lazuar-portal, lazuar-admin, lazuar-developers, lazuar-docs
examples/hub-cashier-next
packages/api-spec, api-types-dotnet, api-types-ts
```

**No** `apps/lazuar-pay`. **No** `apps/lazuar-pay-merchant`. **No** `apps/lazuar-pay-checkout`. **No** `packages/pay-spec`. Ports table: 8080, 3002, 3003, 3004, 3005, 3020, 9080. **No 8081 / 5178 / 5179 / 5435.**

Getting started: `task infra:up`, `task dev`, `task fe`. Dual-run Aura hop A on **:8090**. Demo accounts `admin@lazuar.com` / `founder@acme.test` against **Hub** admin/ops.

A stranger who reads the root README will:

1. start Hub on 8080;
2. collide with One on 8080 if they also follow One’s README;
3. never learn that focused Pay exists in this same tree.

That is a **docs bug** relative to 020’s question (second app / production Pay). It is also consistent with “Hub stays museum until Phase D” — except the museum is the **front door** of the repo that now contains the new product.

Missing from root README (required for stranger mint):

- API keys (`lzr_sk_` is One; Pay homemade `sk_` is refused)
- outbound Pay webhooks
- example curl with a machine key to `:8081/v1/checkouts`
- pointer to `apps/lazuar-pay/README.md`

### 6.2 `apps/lazuar-pay/README.md` — the only mint curl

This is the best Pay-facing document in the repo. It is written for a **laptop dogfood of the hosted cashier**, not a second app.

It tells you:

- listen **8081**, never 8080
- One `__BaseUrl=http://localhost:8080/api/v1`
- no MediatR, no `apps/lazuar-api` project reference, no `IEnumerable<IHostedRail>`
- `task pay:test` / `pay:dev` / `pay:merchant` / `pay:checkout`
- TypeSpec `packages/pay-spec` (`task pay:spec`), not `api-spec`
- compose: `docker-compose.pay.yml`, not root Hub compose
- live whoami: Hub `task dev` **off**; fingerprint `GET http://localhost:8080/api/v1/` names `lazuar-one-api`
- copy **access_token**, not `id_token`
- curl whoami
- curl **POST `/v1/checkouts`** with Bearer `$ACCESS_TOKEN` and JSON `{org_id, amount, currency, provider, success_url, cancel_url}`
- GET `/v1/checkouts/{id}` with same Bearer
- Postgres `lazuar_pay` on **5435**
- owner/admin paste keys per rail; mint pay link with explicit provider
- buyers `:5179/c/{token}`; no One account
- per-org webhook_secret for **Plane B** (PSP → Pay)
- process `Pay__StripeWebhookSecret` Testing-only fallback
- One pause HMAC per-org `PUT /v1/orgs/{orgId}/one-webhook`
- `POST /v1/checkouts` requires writer
- unversioned `GET /ready` is a host probe, not org ready

The mint curl is **human JWT**. It is not:

```bash
curl -H "Authorization: Bearer lzr_sk_…" \
  -H "Idempotency-Key: …" \
  -d '{"org_id":"…","provider":"test","amount":10,"currency":"MYR"}' \
  http://localhost:8081/v1/checkouts
```

That curl would be the second-app hatch. It is **absent**. On this SHA it would also **fail** MemberGate/One authz because `user_id` is omitted on `authz/check` (see S8). Documenting it today would be a lie.

Also absent from the host README:

- how to mint `lzr_sk_` (One recipe R2 lives in the sibling repo)
- how to receive `payment.completed` from Pay (does not exist)
- poll recipe for a second app that has no `:5179` (GET `/v1/checkouts/{id}` with Bearer is the honest poll; GET `/v1/pay/{token}` is public and is what checkout uses)
- that the merchant UI mints **payment-links**, not checkouts
- problem JSON shape
- 201 vs 200 idempotent replay
- 409 idempotency conflict

The whoami curl is enough for a Pay engineer who already has One. It is not enough for “another app without cloning this repo.”

### 6.3 Merchant README — first-party SPA, not integrator

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-merchant/README.md`

Origin `:5178`. API `VITE_PAY_API_URL`. Login One product login **`:5175`**. Callback `http://localhost:5178/callback`. Register SPA script `register-spa.sh` with `ACCESS_TOKEN` + `TENANT_ID`. One `REDIRECT_ALLOWLIST` and CORS. Fingerprint One. Demo user often `ada@acme.test`. Send **access_token**. sessionStorage. Must not: password form, Hub cookie, ops `:3003`, `@repo/api-types-ts`.

This is **how Ada the merchant uses Pay**. A second app is not a PKCE SPA on 5178. Missing: machine key, webhook to the app, curl. The README is adequate for first-party dogfood of the staff shell. It is not a second-app guide.

Merchant mint path in UI (`CheckoutsPage.tsx`): `POST /v1/orgs/{orgId}/products` then `POST /v1/payment-links` with `org_id, amount, currency, provider, product_id, max_payers, unlimited`. Copy buyer URL `{VITE_CHECKOUT_ORIGIN}/c/{public_token}`. **Grep of `/v1/checkouts` under `apps/**/*.ts(x)`: no matches.** The Bezos door the host README curls is unused by the first-party client.

### 6.4 Checkout README — buyer pixel

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay-checkout/README.md`

Origin `:5179`. `VITE_PAY_API_URL` is public, not a secret. Production build **fails** if missing. Buyers have **no** One account. Do not commit `dist/`. Adequate for running the pixel. A second app that hosts **its own** success page still needs a Pay mint door and a paid signal. This README does not provide either.

### 6.5 pay-spec README — engineer, not integrator

Compile, honesty, gitignored dist, grow tsp when a `/v1` door exists, unversioned probes host-only. Correct. Not a mint guide.

### 6.6 examples/README.md — honest about Hub, silent about Pay

“Integrator-facing sample apps… **not** product apps.” Table: one row, `@examples/hub-cashier-next`, port **3020**. “Copy-out friendly: samples intentionally avoid `@repo/*` packages and payment-gateway SDKs.”

That paragraph is true **for Hub**. There is no Pay row. A stranger reading `examples/README.md` will believe the second-app sample is the Hub cashier. Relative to 020, that is a **museum trap**, not an empty folder. See §7.

### 6.7 README scorecard for “stranger mints a checkout”

| Doc | Mentions 8081 `/v1`? | Human JWT mint curl? | Machine key curl? | Outbound webhook? | Poll-until-paid? | Enough alone? |
|-----|----------------------|----------------------|-------------------|-------------------|------------------|---------------|
| Root README | no | no | no (Hub `sk_` in other docs) | no (Hub in apps/lazuar-docs) | no | **no** — sends you to Hub |
| Pay host README | yes | **yes** POST checkouts | no | no | GET checkout by id implied | **partial** — needs One token + writer + vault |
| Merchant README | yes as env | no (UI) | no | no | no | first-party SPA only |
| Checkout README | yes as env | no (buyer) | no | no | pixel polls public GET | buyer only |
| pay-spec README | compile only | no | no | no | no | no |
| examples/README | Hub 3020 | Hub sample | Hub `sk_` | Hub `whsec_` | Hub webhook | **wrong product** |
| Hub apps/lazuar-docs | Hub `/api/v1/integrations/payments/checkouts` | no | Hub `sk_test_` | Hub `payment.completed` | yes | **wrong product** |
| One sibling docs | One `/api/v1` | R1 JWT | `lzr_sk_` for **One** | One control-plane events | n/a | **not Pay** |

Missing, named as the slice required: **API keys, outbound webhooks, example curl with machine key.** All three missing from Pay product READMEs. The host README’s human JWT curl is the only Pay mint recipe.

---

## 7. `examples/` in this repo vs Hub sample (006) vs One samples — honest empty?

### 7.1 This repo `examples/` is not empty. It is Hub.

Tracked under `examples/`:

- `README.md`
- `hub-cashier-next/` entire Next 16 App Router sample

`pnpm-workspace.yaml`: `examples/*`. Root scripts exclude `@examples/*` from product turbo, and add `example:cashier` to start the Hub sample.

`examples/hub-cashier-next/README.md` title: **Hub Cashier Sample**. Proves:

- Server-side Hub M2M `POST …/integrations/payments/checkouts`
- Redirect to Hub hosted checkout
- Signed webhook unlock (`payment.completed`)
- Envelope + `data` payload honesty
- No gateway SDKs — plain `fetch`
- Does **not** use `@repo/api-types-ts`

Keys: `sk_test_…` + `whsec_…`. Env `LAZUAR_HUB_BASE_URL` default `http://localhost:8080/api/v1`. Provision via Hub `POST /one/integrations/workspaces/provision` with `X-Lazuar-Provision-Key`. Port **3020**. Fulfillment rule: mark paid **only** after verified Hub webhook. Never unlock on `success_url`.

`lib/hub.ts` `createCheckout` posts to `hubUrl("/integrations/payments/checkouts")` with `Authorization: Bearer ${key}` and `Idempotency-Key`. That path **does not exist** on focused Pay.

006-sample README (done 2026-08-10): “Make Hub integrator story demoable… standalone Next.js sample under `examples/` that provisions (or accepts keys), creates M2M checkouts, verifies signed webhooks, and fulfills a toy domain object.” Runtime anchors: `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs`, Hub `OutboundWebhookSignature.cs`. **Museum.** Steal **judgment** (plain fetch, no `@repo/*`, webhook not success_url, snake_case, idempotency). Do **not** steal paths, `sk_` prefix, provision secret, or the Next app as-is.

### 7.2 Is `examples/` “honest empty” for Pay?

No. Empty would be honest: “Pay has no second-app sample yet.” What we have is **a working Hub sample in the Pay repo**, advertised at the root README and `pnpm example:cashier`, with VitePress runbooks under `apps/lazuar-docs` that look like product docs.

A stranger following the easiest sample path will integrate **Hub**, not Pay. That is worse than empty. Ranked fix in §11: either (a) move/mark Hub sample as museum in every README that currently presents it as *the* sample, or (b) add `examples/pay-node` and make it the only `example:*` script that 020 cares about. Do not rewrite hub-cashier-next onto 8081 by changing the base URL. The JSON, auth, and webhook dialect are different products.

### 7.3 Sibling One `examples/` — the right *identity* sample, still not Pay

`/Users/akmalfirdaus/Code/lazuar/lazuar-one/examples/README.md`

| Path | What it shows |
|------|----------------|
| `node-api-key/` | `GET /me` as `lzr_sk_…` |
| `node-webhook-verify/` | Local receiver verifying `X-Lazuar-Signature` (`whsec_…`, 300s skew) |
| `oidc-spa-notes/` | pointers |
| `vite-spa/` | `:5177` login → `/me` → workspace → key (`@lazuar/one-client` workspace) |
| `postman/` | One collection |

`node-api-key/index.mjs` is ~60 lines of plain `fetch` to `${apiBase}/me` with `Authorization: Bearer ${apiKey}`. Warns if the secret does not start with `lzr_sk_`. Docs: One recipe R2.

012/08 “Runnable sample alignment”: Pay should **not rewrite** this; document mint via R2 then run One’s sample against One. That is still the correct identity dogfood. It does **not** mint a Pay checkout. One webhooks are `member.invited` / `tenant.suspended`, not `payment.completed`.

Pay Consumer-0 (011/02) is supposed to be One’s first external product. One docs still do not name Pay as that consumer (see §8). One samples do not call `:8081`.

### 7.4 What a Pay sample would have to prove (006 judgment, new paths)

Steal from 006 G3/G4/G5:

- second backend (or tiny node script) talks **only HTTP**
- no `@repo/*`, no Stripe/CHIP SDK
- snake_case JSON
- real signature algorithm **once Pay has Plane C**; until then **poll** `GET /v1/checkouts/{id}` or public `GET /v1/pay/{token}` and say so
- turbo exclude so sample failures never block product CI
- no Dockerfile / GHCR

Do **not** steal:

- `sk_test_` prefix
- Hub `/api/v1/integrations/payments/checkouts`
- Hub provision secret
- Hub `X-Lazuar-Signature: t=<unix>,v1=<hex>` dialect until Pay’s outbound signer is specified by slice 03
- Next.js App Router requirement — a 80-line `pay-node` `.mjs` is closer to One’s `node-api-key` and is enough for “second app”

On this SHA the sample **cannot honestly claim webhook unlock**. Host has no Plane C. An honest v0 sample is:

1. mint `lzr_sk_` in One (R2) — **blocked** until Pay MemberGate can use it (slice 02);
2. `POST /v1/checkouts` or `POST /v1/payment-links` with that Bearer — **blocked** on the same gate;
3. print buyer URL `${CHECKOUT_ORIGIN}/c/${public_token}`;
4. poll GET until `status=paid` **or** (later) verify Pay outbound HMAC.

Until (1)(2) land, the only honest runnable path is the **human JWT curl in the host README** plus the first-party SPAs. Calling that a second-app sample would be a lie.

---

## 8. Sibling One docs — Pay is not documented as Consumer-0

### 8.1 What One docs actually are

`/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs`

Integrations:

- `integrations/index.md` — OIDC, API callers, authz, webhook receivers. Base `/api/v1`. Recipes R1–R6. API keys `lzr_sk_`. Webhooks CRUD. **lazuar-app** Settings. Samples table points at One `examples/`. Planned: published npm SDK. **Zero mentions of Pay / 8081 / checkouts / payment.completed.**
- `integrations/api-keys.md` — Available. `lzr_sk_`. Scopes catalog. curl create/use/list/revoke against **One**. Example name `"pay-worker"` with scopes `authz:check`, `tenant:read` is a **string in an example JSON**, not a Pay integration guide. “Not in scope: webhooks for key lifecycle events (**planned**)” — 012/08 already called that sentence stale vs One’s producer; this 020 file does not re-audit One.
- `integrations/webhooks.md` — One control-plane events (`tenant.suspended`, `member.*`, `api_key.*`, `oidc_app.*`). HMAC `v1=` over `{timestamp}.{raw_body}`. **Not** payment events.
- Recipes: `service-api-key.md` (R2), `webhook-verify.md` (R5), `user-oidc-spa.md` (R1).
- `reference/sdk-publish-policy.md` — `@lazuar/one-client` **private, not published**. Integrators use OpenAPI + `examples/` today. First publish gate includes “at least one external dogfood consumer.” Pay-as-Consumer-0 would be that consumer **if it existed in the docs**. It does not.

Grep of `lazuar-pay|8081|/v1/checkouts` under `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs`: **no matches**.

Grep of Pay as Consumer-0 in the sibling repo hits **plan papers** (`plans/015-dimension/04-authz-product-to-a-plus.md` still talks about not scheduling Consumer-1; 011/02 in **this** repo declares Pay is Consumer-0). Product docs do not.

### 8.2 Hub docs in *this* repo look like the missing Pay page, and are the wrong product

`/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/`

| Page | Product it describes |
|------|----------------------|
| `api-keys.md` | Hub `sk_test_` / `sk_live_`. Ops → Developer → API Keys. `GET /api/v1/integrations/payments/me`. Scopes `payments.checkouts:write`. **Prefix decision = B — not `lpk_`.** Stripe collision warning. |
| `create-checkout.md` | `POST /api/v1/integrations/payments/checkouts` Bearer `sk_test_`. Scope `payments.checkouts:write`. |
| `webhooks.md` | Hub hop 1 PSP→Hub, hop 2 Hub→app `payment.completed` / `payment.failed`. `t=<unix>,v1=<hex>`. |
| `second-app-checklist.md` | Provision non-aura, store `sk_` + `whsec_`, POST checkouts, signed webhook, unlock own domain. |
| `run-sample-app.md` | `examples/hub-cashier-next` :3020 |
| `index.md` (docs home) | “Lazuar Hub” hero |

This is **exactly the DX page Pay does not have**, written for the process 020 is **not** shipping as the kernel. Copy-editing Hub `sk_` onto Pay `lzr_sk_` without changing paths and dialects would ship a lying quickstart.

### 8.3 Consumer-0 gap, stated plainly

011/02: “Pay is Consumer-0 (One `plans/017-evals/08-dogfood-then-serve.md` §6).”

Consumer-0 in One’s honesty language means: a **real product** uses One’s public HTTP (OIDC SPA, `lzr_sk_`, authz, webhooks) so One is not only dogfooding `lazuar-app`.

Live on this SHA:

- Pay merchant **is** an OIDC SPA of One (`:5178` → `:5175`). That is Consumer-0 for **login**.
- Pay **does** call One `GET /me` and `POST …/authz/check` from C#. That is Consumer-0 for **Mode U**.
- Pay **does** receive One HMAC at `POST /v1/one/webhooks` (Plane A). That is Consumer-0 for **tenant.suspended / pause**.
- Pay is **not** documented on One’s VitePress as that consumer.
- Pay does **not** use `lzr_sk_` as a Pay `/v1` caller (no C# matches).
- Pay does **not** appear in One recipes as “then POST Pay `/v1/checkouts`.”

The docs gap is therefore two-sided:

1. One docs do not say “Pay is how you take money; here is the stitch.”
2. Pay docs do not say “mint the key in One (link R2); then call Pay `/v1` (curl that does not exist yet).”

020’s “one docs page second app” should not be a third VitePress product invented from nothing. Ranked placement in §11.

---

## 9. Client libraries: none for Pay. Refuse waiting on npm `@lazuar/one-client`. Plain fetch is the hatch.

### 9.1 NP-XX-021 as live refuse, not a historical vibe

011/11 and 012/08:

| ID | Feature | Status |
|----|---------|--------|
| NP-XX-021 | Block Pay on npm publish of `@lazuar/one-client` | **refuse**. Workspace import / raw HTTP is enough. |

012/08 binding: “Do not block on npm. `examples/node-api-key` and recipe R2 are the mint/use proof.” “Pay builds against workspace `@lazuar/one-client` or raw fetch. CI does not `npm view @lazuar/one-client`.”

Live One package: `"name": "@lazuar/one-client", "private": true, "license": "UNLICENSED"`. SDK policy: no registry entry, no CI publish. Integrators who clone One may import the workspace pack. External integrators use OpenAPI until the publish gate.

Live Pay:

- C# `OneClient` is a handwritten `HttpClient` to One `me` and `tenants/{id}/authz/check`. It does not reference the TS pack (it cannot).
- Merchant does not import `@lazuar/one-client` (would be a cross-repo workspace that this pnpm workspace does not include). Merchant uses `oidc-client-ts` + `oneApi.ts` fetch to `POST /tenants`.
- Checkout has no One client at all (buyers are not One humans).

Waiting on npm to start a Pay sample, a Pay types package, or a Pay mint curl is **refuse**. 013-prods/01 production bar already listed this under “do not block.” This SHA has not weakened that.

### 9.2 There is no Pay SDK to wait on either

No `@lazuar/pay-client`. No Kiota from pay-spec. No openapi-typescript output committed. Hub `docs/architecture-decision-log/011-sdk-publishing-runbook.md` is Hub museum. `packages/lhdn-sdk-ts` / `lhdn-sdk-dotnet` are Hub/LHDN museum; IsolationTests ban `Lhdn` in Pay src.

openapi-fetch is used by Hub ops/portal/admin. Not by Pay SPAs.

### 9.3 Plain fetch is the hatch — already proven in-tree

| Caller | Hatch |
|--------|-------|
| Merchant | `fetch(`${payApi}/v1/…`)` |
| Checkout | `fetch(payPath(token))` and `fetch(…/start)` |
| Host README | `curl -H "Authorization: Bearer $ACCESS_TOKEN"` |
| Hub sample (museum, judgment only) | `fetch(hubUrl("/integrations/payments/checkouts"))` |
| One node-api-key | `fetch(url, { headers: { Authorization: Bearer lzr_sk_ }})` |

A Pay second-app sample that `npm i @lazuar/one-client` from github packages would **violate NP-XX-021 and isolation**. A sample that `npm i @repo/api-types-ts` would go red. The hatch is `fetch` + snake_case JSON + Bearer + problem `{detail}`.

When `@repo/pay-types-ts` exists, it should be **optional types**, generated from **pay-spec**, not a runtime SDK, and not imported by the sample if the sample is copy-out friendly (006 G5). First-party merchant/checkout may take it later to kill hand-written DTO drift. Ranked after doors.

### 9.4 What “SDK” would even wrap today

If we generated types this afternoon from current OpenAPI:

- no auth
- no errors
- no `lzr_sk_`
- no outbound webhook verify
- checkout list comment wrong
- public pay and merchant mint mixed in one spec (correct for one host; a stranger still needs a narrative of which doors they call)

Shipping that as `@lazuar/pay-client` would freeze a **cashier contract** as if it were a kernel contract. Refuse until Plane C + M2M exist **or** generate types with a README that says “hosted cashier, Mode U JWT only.” Prefer the latter only if first-party SPA drift becomes a bug. It is not the second-app blocker.

---

## 10. Classification — bug vs missing feature vs refuse

020 parent asks the ten reports to classify. This slice:

### 10.1 Bugs (lies in files that exist)

| ID | Lie | Fix later |
|----|-----|-----------|
| B1 | Root README is Hub-only; Pay apps invisible | Point at `apps/lazuar-pay/README.md`, ports 8081/5178/5179/5435, `task pay:*`. Keep Hub as museum **below** a watermark. |
| B2 | tsp `Checkouts.list` comment still says mix of one-off and link children | Host filters `PaymentLinkId == null`. Change the comment. Do not change the host back. |
| B3 | `examples/` + `pnpm example:cashier` + Hub VitePress present Hub as the integrator sample of **this** repo | Museum banner on `examples/README.md`, root README, `apps/lazuar-docs` homepage. Do not delete Hub sample in this program unless 10-honesty says so; **do** stop advertising it as Pay. |
| B4 | Host README mint curl is `POST /v1/checkouts`; first-party UI mints payment-links | Document both doors. “UI creates pay links. Kernel mint is POST /v1/checkouts.” |
| B5 | OpenAPI has no security and no problem schema while host 401s with `{status,title,detail}` | Grow tsp **errors + Bearer** when writing the second-app page, without inventing M2M. Path honesty stays green either way. |

B2/B5 are spec. B1/B3/B4 are docs. None require kernel doors. They are still **not** sufficient for a stranger.

002-067 class (stale dist vs 13 tsp vs 22 Map*) is **closed at path layer**. Do not reopen it as a P1 on this SHA. Laptop leftover dist remains a footgun; CI compile-then-scrape is the mitigation; honesty does not need to start committing yaml.

### 10.2 Missing features (doors / artifacts that do not exist)

| ID | Missing | Blocked on | Slice owner |
|----|---------|------------|-------------|
| M1 | Pay accepts `lzr_sk_` on `/v1` writer/member doors with an explicit `user_id` policy | One authz key semantics | 02-machine-keys |
| M2 | Pay outbound `payment.completed` (Plane C) signed to the app | M1 useful but poll can substitute | 03-outbound-webhooks |
| M3 | `examples/pay-node` using `lzr_sk_` + poll or webhook | M1; M2 optional if poll is honest | **this slice’s how-to-solve** |
| M4 | Docs page “second app” (Pay) | M1 for a true M2M page; a Mode U curl page can ship with B1/B4/B5 | this slice |
| M5 | `@repo/pay-types-ts` from pay-spec, not `task gen` | optional; after M3 or when SPA drift bites | this slice, later |
| M6 | Pay Scalar / developers site for pay-spec | refuse-as-first; OpenAPI file + curl is enough | — |
| M7 | Homemade Pay `sk_test_` mint | **refuse** (012/08 prefix collision) | refuse |
| M8 | npm `@lazuar/one-client` or `@lazuar/pay-client` | **refuse to wait** | NP-XX-021 |

M3 without M1 would be a sample that uses Ada’s user JWT from env — a **worse** secret than `lzr_sk_` (long-lived user token in a worker). Do not ship that as the second-app sample. Ship it only as a documented **laptop** curl, which the host README already almost is.

### 10.3 Refuse

| ID | Temptation | Why refuse |
|----|------------|------------|
| R1 | Block Pay DX on `npm publish @lazuar/one-client` | NP-XX-021. Plain fetch. |
| R2 | `pnpm add @repo/api-types-ts` in merchant/checkout/sample | IsolationTests / locks go red. Wrong host. |
| R3 | Hook pay-spec into Hub `task gen` / `contracts` dirty-check | 012/04. Two hosts. |
| R4 | Generate `@repo/pay-types-ts` from Hub api-spec | Same sin. |
| R5 | Point hub-cashier-next at 8081 | Paths, keys, webhooks are Hub. |
| R6 | Copy One tenant/invite routes into pay-spec | 012/04. Pay is not One. |
| R7 | Grow tsp for `payment.completed` outbound before the host maps it | Reverse of 019 path-lie. Honesty would go red **or** you’d have extraSpec. |
| R8 | Mint Pay homemade `sk_*` | 012/08. Stripe prefix. One already mints `lzr_sk_`. |
| R9 | Wait for One staging PASSED / public SKU before writing `examples/pay-node` | 013-prods. Develop against live One HTTP. |
| R10 | Treat first-party Vite apps as the second-app sample | They are the hosted cashier. Strangers should not clone `:5178`. |
| R11 | Publish pay-spec OpenAPI as “stable v1 kernel” | version `0.1.0`. No auth in yaml. No Plane C. |

---

## 11. How to solve — ranked sequence

Honesty is already in CI. Do not start with a types package. Do not start with npm. Grow tsp when kernel doors land. One sample. One docs page.

### Rank 0 — keep (already done on this SHA)

1. CI job `pay`: tsp compile then `check-pay-openapi-honesty.mjs`.
2. `IMPL_ONLY` for unversioned `/health` `/ready`.
3. Dist gitignored; do not commit yaml unless a later paper wants dirty-check **instead of** gitignore (do not do both).
4. IsolationTests + Vite locks against Hub `@repo/api-types-ts`, MediatR, `IEnumerable<IHostedRail>`.
5. Merchant/checkout plain fetch.
6. Host README human JWT whoami + POST checkouts curl (keep; fix surrounding docs).

### Rank 1 — docs honesty that does not wait on kernel doors (bugs B1–B4)

These can land without M2M. They stop the museum trap.

1. **Root README watermark** above the Hub CaaS essay: focused Pay is `apps/lazuar-pay` :8081, merchant :5178, checkout :5179, spec `packages/pay-spec`, `task pay:dev`. Hub tree is museum. Dual-run 8080 is One, not Hub, when dogfooding Pay.
2. **`examples/README.md`**: first sentence: this folder currently contains the **Hub** cashier sample (006). Pay second-app sample is not here. `pnpm example:cashier` talks to Hub `:8080/api/v1`, not Pay `:8081/v1`.
3. **Host README**: two mint doors. UI = payment-links. Kernel = POST `/v1/checkouts`. 201/200/409. Problem JSON. Poll GET `/v1/checkouts/{id}` (Bearer) or public GET `/v1/pay/{token}` (no unlock-on-success). Explicit: **no machine key on Pay yet; no Pay→app webhook yet.**
4. **tsp comment S1**: list checkouts does not mix children.

This rank does **not** claim production-ready second-app. It stops lying.

### Rank 2 — kernel doors (other 020 slices; this slice is blocked on them for a real sample)

1. **M2M (slice 02):** Pay MemberGate branches JWT vs `lzr_sk_`. Do not send key id as `authz/check` `user_id`. Introspect One `GET /me` for bound tenant. Scopes: Pay should **not** invent `payments.checkouts:write` on One’s catalog without One’s consent; either map existing One scopes + writer role overlay, or a written Pay-side “key is bound tenant + One membership of a human owner” policy. This paper does not pick the scope string. It names the **user_id 400** as the live blocker (OneAuthzCheckRequest has no user_id).
2. **Outbound (slice 03):** `payment.completed` (and failed) HMAC to a URL the **app** registered. Until then, poll is the hatch. Do not generate tsp operations for Plane C before `MapPost`.
3. **Do not** mint Pay `sk_`. Point merchants at One Settings → API keys (R2).

### Rank 3 — grow tsp when those doors land (not before)

1. Add Bearer security and `PayProblem` for existing doors (can be Rank 1.5; does not invent doors).
2. When M2M is real: document in comments which doors accept `lzr_sk_`. Still no second mint API in Pay.
3. When Plane C is real: new interface `AppWebhooks` or similar — **only after Map\***. Honesty extraSpec would catch a premature tsp.
4. Field pins in the honesty script for new required JSON, same style as provider/slot_key/name.
5. Still do not merge with Hub `contracts`.

### Rank 4 — `examples/pay-node` (the actual second-app artifact)

Shape, stolen from One `examples/node-api-key` not from Hub Next:

```
examples/pay-node/
  README.md
  .env.example          # PAY_BASE=http://localhost:8081  ONE_BASE=…  LZR_SK=lzr_sk_…  ORG_ID=
  index.mjs             # POST /v1/checkouts (or payment-links) + print buyer URL + poll GET
  webhook.mjs           # later: verify Pay outbound; until Plane C, file says "poll only"
  package.json          # no @repo/*, no next, type module
```

Rules:

- plain `fetch`
- refuse `@repo/api-types-ts`, `@lazuar/one-client` from npm, Stripe SDK
- turbo exclude `@examples/*` already exists — add the package name under `examples/*` workspace
- do **not** start it from `pnpm example:cashier`
- README: mint the key in One (link sibling R2), paste vault in Pay merchant or curl PUT gateway, then run the script
- fulfillment: poll until `paid`; if Plane C landed, verify HMAC and **do not** unlock on buyer return
- copy-out: a stranger can copy the folder without the monorepo (env + fetch)

Sequence vs Rank 2: **do not merge pay-node that claims M2M until MemberGate accepts `lzr_sk_`**. A stub README in Rank 1 that says “blocked on machine keys” is allowed. A green demo that uses a user JWT in `.env` as if it were a machine key is a lie.

### Rank 5 — one docs page “second app”

Placement options (pick one; do not write three):

| Option | Where | Pros | Cons |
|--------|-------|------|------|
| A | Sibling One VitePress `integrations/pay.md` | Consumer-0 lives where One said it would | Cross-repo PR; One docs currently have no Pay |
| B | New Pay VitePress (this repo, not Hub `apps/lazuar-docs`) | Product-owned | Another site to host; Rank 5 cost |
| C | `apps/lazuar-pay/README.md` section “Second app” + `examples/pay-node/README.md` | Zero new site; files already the front door for Pay engineers | Not the pretty integrator portal; root README still Hub |
| D | Rewrite Hub `apps/lazuar-docs/integrations/*` in place | Looks like 006 | **Refuse.** Wrong keys, wrong paths, wrong hop 2 |

**Recommend C then A.** C unblocks engineers in this repo without standing up VitePress. A is the Consumer-0 sentence One is missing: “Pay is a product that calls us; here is the stitch (link to Pay README + pay-node).” Do not do D. Do not do B until A/C are boring.

Page contents (when Rank 2 exists):

1. Mint `lzr_sk_` in One (R2 curl).
2. PUT Pay gateway (or Test in Dev).
3. `POST http://localhost:8081/v1/checkouts` Bearer `lzr_sk_`, Idempotency-Key, `{org_id, provider, amount, currency, success_url, cancel_url}`.
4. Redirect buyer to `{CHECKOUT_ORIGIN}/c/{public_token}` **or** start public pay yourself.
5. Poll GET checkout **or** verify Plane C.
6. Never unlock on success_url.
7. Replay: 200 duplicate / 409 conflict as applicable.

Until Rank 2, the page is a **checklist of gaps**, not a runbook that pretends to work.

### Rank 6 — optional types package

`packages/pay-types-ts` generated with `openapi-typescript packages/pay-spec/dist/openapi.yaml`. Script on `@repo/pay-spec` or a tiny package. **Not** `task gen`. First-party merchant/checkout may switch later. Sample stays fetch-only. Do this when B5 (errors+auth in tsp) exists, otherwise generated types teach a 200-only world.

### Rank 7 — never

npm wait; Hub types; Hub sample retarget; Pay `sk_`; tsp for unmapped Plane C; merging honesty jobs.

### Sequence (one line)

Keep CI honesty → Rank 1 README/museum/comment fixes → slice 02 M2M + slice 03 Plane C (parallel after MemberGate design) → grow tsp to those Map* → `examples/pay-node` + README curl with `lzr_sk_` → One docs Consumer-0 link → optional pay-types-ts.

If only one engineering week exists after Rank 1: **slice 02, not the types package.** A sample cannot lie its way around a 400 from One.

---

## 12. Bezos door vs Linux room — what this slice is allowed to demand

011/08: public `/v1` from day one; own UI is a client of `/v1`; no back door into tables; One is another team over HTTP.

Live:

- Merchant and checkout **are** HTTP clients of `/v1`. That is Bezos for first-party UI.
- There is no second process inside Pay. That is Linux in the room.
- A **stranger** still cannot use the door without a human JWT obtained by cloning the merchant SPA or scraping tokens from `:5175`. That is a **missing key on the door**, not a missing room split.

NP-API-004 (011/11): “Merchant ops is a client of `/v1` (One user JWT or `lzr_sk_`)” status was `todo` in the tracker. Live: merchant **is** a client of `/v1` with **user JWT**. `lzr_sk_` half is still todo. NP-SOON-007: “M2M checkout for a second of *your* apps (same `/v1`)” still `todo`. This slice agrees with those cells without flipping them.

Do not demand a Pay SDK to satisfy Bezos. The door is HTTP. The missing piece is **who may turn the handle** (machine key) and **how the app hears the click** (outbound event or documented poll).

---

## 13. First-party SPA vs integrator — 08-headless sibling pointer

This file does not steal slice 08’s job. One sentence of evidence: merchant is **not** API-only; it is a PKCE staff shell. Checkout is **not** an SDK; it is a hosted pixel. Integrator DX cannot be “clone these two Vite apps.” 006 already knew that: the sample was a **third** origin (3020) on purpose. Pay needs its own third origin or a node script. The Vite apps remaining as the only clients is why kernel doors feel optional until a stranger shows up.

Merchant also calls **One** `POST /tenants` (`oneApi.ts`). That is correct Consumer-0 behavior. pay-spec must not absorb it. The second-app page must link One workspace create (R3 / lazuar-app) separately from Pay mint.

---

## 14. Field-level leftover (honesty pins vs live JSON)

Honesty pins seven tokens. Live JSON that a sample would parse and that tsp **has** but honesty **does not pin**:

| Field | tsp | Host |
|-------|-----|------|
| CheckoutSession.public_token | optional string | always set on mint (hex concat) |
| PaymentLink.public_token | required | required |
| PaymentLink.unlimited / paid_count / taken_count | required | occupancy |
| OrgReady.ready | required | not dummy `true` anymore if 002-078 resolved — this slice did not re-prove org-ready semantics |
| Receipt.number | required string | `"PENDING"` placeholder |
| PutGateway.webhook_secret | required | Plane B |
| CreateCheckoutRequest.idempotency_key | optional body | also header `Idempotency-Key`; header wins if both |
| Whoami.active_org_id | optional | mapped from One `active_tenant_id` |

A generated client would see these. A stranger reading tsp comments would miss header-vs-body idempotency. Host README curl does not send `Idempotency-Key`. Replay 200 is untested in the README.

PublicPay occupancy fields exist in tsp and host; checkout `PayView` type omits them. Pixel still functions. Sample poll should use `status`, not occupancy, for “paid.”

---

## 15. CI matrix — what is proven vs what a stranger needs

| Gate | Proves | Does not prove |
|------|--------|----------------|
| job `pay` dotnet test | host + IsolationTests | a second app can mint |
| job `pay` Vite build | merchant/checkout typecheck/bundle | they are copy-out samples |
| job `pay` tsp compile | main.tsp is valid TypeSpec | OpenAPI is documented for humans |
| job `pay` honesty | 22 yaml ops ↔ Map* ∪ IMPL_ONLY + 7 field pins | auth, errors, comments, M2M, Plane C |
| job `contracts` | Hub types dirty + Hub path honesty | **nothing about Pay** |
| `pnpm example:cashier` | Hub sample starts | **wrong product** |
| IsolationTests Vite_apps | no Hub types in Pay UIs | no Pay types package exists (that is fine) |

Production-ready **hosted cashier** can be true while production-ready **kernel** is false. 019 already said that. 002 closed cashier bugs. 020 still has no machine key and no outbound `payment.completed`. This slice’s job is to say the **docs/sample/SDK** layer is also false, and that honesty-green is not a counterexample.

---

## 16. What “enough for a stranger to mint a checkout” would look like (acceptance, not work)

A stranger **without** this git clone, given a docs URL and two secrets, can:

1. Create a One workspace (or be invited).
2. Mint `lzr_sk_` with documented scopes (R2).
3. Configure one Pay rail (or Test in non-prod) without pasting Stripe secret as Bearer.
4. `POST /v1/checkouts` or `POST /v1/payment-links` with that key and get **201** + `public_token`.
5. Send a buyer to a URL that is not One login.
6. Learn paid via poll **or** signed Pay webhook, with a copy-paste verifier.
7. Replay safely (idempotency / duplicate).
8. Never import Hub types, never wait on npm, never `SELECT` Pay.

On this SHA: step 1 works in One. Step 2 works in One **for One’s API**. Steps 3–4 work with a **human JWT** if you clone Pay and read the host README / use `:5178`. Step 5 works (`:5179/c/…`). Step 6 is **pixel poll only** (no app webhook). Step 7 is host-real, undocumented in a sample. Step 8 is true for first-party (isolation) and **false** if they follow `examples/hub-cashier-next` (they will use Hub `sk_`).

That is the gap. It is not a stale OpenAPI file. It is not an unpublished One client. It is missing kernel handles plus a museum sample in the doorway.

---

## 17. Evidence appendix — live inventories

### 17.1 Honesty stdout (this SHA)

```
Pay OpenAPI honesty: 22 spec ops, 24 Map* (2 host-only probes).
EXIT:0
```

### 17.2 OpenAPI operations (compiled leftover, matches tsp)

```
POST /v1/checkouts
GET /v1/checkouts/{id}
GET /v1/health
POST /v1/one/webhooks
GET /v1/orgs/{orgId}/checkouts
PUT /v1/orgs/{orgId}/gateway
GET /v1/orgs/{orgId}/gateway
GET /v1/orgs/{orgId}/gateways
PUT /v1/orgs/{orgId}/one-webhook
GET /v1/orgs/{orgId}/one-webhook
GET /v1/orgs/{orgId}/payment-links
GET /v1/orgs/{orgId}/payments
POST /v1/orgs/{orgId}/products
GET /v1/orgs/{orgId}/products
GET /v1/orgs/{orgId}/ready
GET /v1/orgs/{orgId}/receipts
GET /v1/orgs/{orgId}/receipts/{id}
GET /v1/pay/{token}
POST /v1/pay/{token}/start
POST /v1/payment-links
POST /v1/webhooks/{provider}/{orgId}
GET /v1/whoami
```

Count 22. `securitySchemes` false. `BearerAuth` false. `401` false. `403` false.

### 17.3 Map* files (grep)

```
Credentials/GatewayEndpoints.cs          PUT/GET /v1/orgs/{orgId}/gateway, GET …/gateways
Webhooks/WebhookEndpoints.cs             POST /v1/webhooks/{provider}/{orgId}
Hosting/HealthEndpoints.cs               GET /health, GET /v1/health, GET /ready
Catalog/CatalogEndpoints.cs              POST/GET /v1/orgs/{orgId}/products
Money/Queries/PaymentQueryEndpoints.cs   GET payments, receipts, receipts/{id}
PaymentLinks/PaymentLinkEndpoints.cs     POST /v1/payment-links, GET …/payment-links
Checkouts/CheckoutEndpoints.cs           POST /v1/checkouts, GET /v1/checkouts/{id}, GET …/checkouts
Identity/OrgReadyEndpoints.cs            GET /v1/orgs/{orgId}/ready
Identity/WhoamiEndpoints.cs              GET /v1/whoami
PublicPay/PublicPayEndpoints.cs          GET /v1/pay/{token}, POST /v1/pay/{token}/start
Identity/OneWebhooks/OneWebhookEndpoints.cs  POST /v1/one/webhooks, PUT/GET …/one-webhook
```

### 17.4 gitignore / tracked spec

```
HEAD 6d730d155c871465c35c192cf7730bfd270b47fa
packages/pay-spec/.gitignore:6:dist/  → packages/pay-spec/dist/openapi.yaml
git ls-files packages/pay-spec/dist   → empty
```

### 17.5 Isolation cathedral pins

`IsolationTests.cs` BannedSrc includes `MediatR`, `IEnumerable<IHostedRail>`. `Vite_apps_do_not_use_hub_types` asserts merchant and checkout package.json do not contain `@repo/api-types-ts`. Merchant `locks.test.ts` and checkout `locks.test.ts` repeat the Hub-types ban.

### 17.6 `lzr_sk_` / Plane C in Pay C#

Grep `lzr_sk_` under `apps/lazuar-pay/**/*.cs`: **no matches**.  
Grep `payment.completed` / `OutboundWebhook` / `Plane C` under `apps/lazuar-pay`: **no matches**.

OneClient `CheckMemberAsync` body: `{ relation: "member", object: { type: "tenant", id: orgId } }` — **no `user_id`**. One R2: omit user_id or send key id → **400** for API-key callers.

### 17.7 First-party fetch map

Merchant: whoami, gateways, gateway PUT, payment-links GET/POST, products POST, payments GET, receipts GET. One: POST tenants.  
Checkout: GET pay + POST start.  
Neither: POST `/v1/checkouts`, GET `/v1/checkouts/{id}`, GET `/v1/orgs/{id}/checkouts`, GET `/v1/orgs/{id}/ready`, GET products, GET receipts/{id}, one-webhook, Plane A/B.

### 17.8 Sibling docs files named in the slice brief

Opened and read:

- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/integrations/api-keys.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/integrations/webhooks.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/integrations/index.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/recipes/service-api-key.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-docs/docs/reference/sdk-publish-policy.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/examples/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/examples/node-api-key/index.mjs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-one/packages/one-client/package.json`

Pay is not Consumer-0 in those pages.

### 17.9 Hub museum docs/sample opened

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/index.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/api-keys.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/create-checkout.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/webhooks.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-docs/docs/integrations/second-app-checklist.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/examples/hub-cashier-next/lib/hub.ts`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/006-sample/README.md`

---

## 18. Cross-links to other 020 slices (pointers, not substitutes)

| File | Why this slice names it |
|------|-------------------------|
| 01-public-http-api | `/v1` error/idempotency/versioning. This file: yaml has no 401 and version is `0.1.0`. |
| 02-machine-keys-m2m | `lzr_sk_` vs JWT. This file: zero C# matches; One authz user_id blocker; refuse Pay `sk_`. |
| 03-outbound-webhooks | Plane C. This file: zero host matches; sample cannot honestly webhook-unlock. |
| 04-inbound-webhooks | Plane A/B **are** in tsp and Map*. Integrators do not call them; PSP and One do. Spec omits PSP headers. |
| 08-headless-vs-spa | Merchant/checkout as clients vs API-only integrator. This file: UI ≠ kernel mint door. |
| 10-honesty-production-bar | Ranked bugs for the parent. This file’s B1–B5 / M1–M5 / R1–R11 are the DX subset. |

Do not treat this file as the production-ready bar. Treat it as: **the contract scrape is green; the integrator story is not.**

---

## 19. Closing

On `6d730d15` / 2026-08-28, Pay’s TypeSpec is a **path-complete** description of the hosted cashier `/v1` plus two host-only probes behind `IMPL_ONLY`. Dist is gitignored. Honesty is in CI and exits 0. Remaining drift is comments, auth, errors, and doors that **should not** be in tsp until they exist.

There is **no** Pay `api-types-ts`. Hub `@repo/api-types-ts` is museum and IsolationTests stay red if Pay UIs import it. First-party clients use plain fetch. There is **no** Pay SDK. Waiting on npm `@lazuar/one-client` is refuse (NP-XX-021).

READMEs are not enough for a stranger to mint a checkout as a second app. The host README’s human JWT curl is the only Pay mint recipe. Root README hides Pay. Merchant UI mints payment-links, not checkouts. API keys, outbound webhooks, and machine-key curl are missing.

`examples/` is **not** honestly empty: it is a Hub second-app sample (006, port 3020, `sk_`, Hub webhooks). One sibling docs document One keys and One webhooks, not Pay as Consumer-0.

How to solve: keep honesty; Rank 1 stop museum lies; kernel M2M then Plane C; grow tsp when Map* lands; one `examples/pay-node` on `lzr_sk_` + poll or webhook; one “second app” page (Pay README, then One Consumer-0 link). Rank types last. Never npm-wait. Never Hub types. Never retarget hub-cashier-next.

That is the integrator DX of focused Pay on this SHA: **an honest cashier contract with no second-app handle.**
