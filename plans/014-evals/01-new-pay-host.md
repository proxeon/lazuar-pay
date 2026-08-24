# 01 — Current state of the focused Pay host (`apps/lazuar-pay`)

**Date:** 24 August 2026  
**Repo:** `lazuar-pay` (`/Users/akmalfirdaus/Code/lazuar/lazuar-pay`)  
**Branch:** `main`  
**HEAD:** `ee2db8e5758305089a38298456c456d6bf0e97ca` (`ee2db8e5`) — `feat(pay): Bar B receipts, webhook secret, merchant money UI`  
**Type:** Uncondensed analysis of the **new** focused C# host. **Not** an implementation. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) cells. **Not** a Stripe-vs-Hub adapter port. **Not** the Vite shells.

Recorded from `.git/HEAD` (`ref: refs/heads/main`), `.git/refs/heads/main` (full SHA above), and `.git/logs/HEAD` (tip subject matches 014’s index). The working copy is on `main` at the Bar B receipts commit, not at the 013 analysis SHA `6f866ff0`.

Parent index: [README.md](./README.md). Binding: [011](../011-new-lazuar-pay/README.md), [012](../012-one-to-pay/README.md), [013](../013-prods/README.md). Tracker compared, not edited: [011/11](../011-new-lazuar-pay/11-checklist.md). Historical host paper that this file **overrules on facts**: [013/03](../013-prods/03-host-production-seams.md) at `6f866ff0`.

---

## 0. Verdict (read this before the inventory)

**The focused host is no longer an in-memory whoami fixture.** On `ee2db8e5` it is a single `net10.0` process that listens on **8081**, talks to One over HTTP REST, persists into **one** Postgres database (`lazuar_pay` on host **5435**) through **one** `PayDbContext`, encrypts a Stripe secret with AES-GCM, creates a Stripe Checkout Session in `mode=payment`, verifies a Stripe webhook, and — in the same HTTP request, in a second EF transaction — marks the checkout paid, writes a two-line journal, and mints `RCPT-{MYT year}-#####` titled **Official Receipt**.

That is a different animal from the process [013/03](../013-prods/03-host-production-seams.md) inventoried at `6f866ff0`. That paper said, in its own verdict:

> **The focused host is a working laptop fixture, not a production process.** It listens on 8081 when launchSettings applies, forwards the caller’s Bearer to One, maps a snake_case `/v1`, stores checkouts in a process-local `ConcurrentDictionary`, and hard-codes CORS to the two new Vite origins.

Live code on this SHA disagrees on the store, the packages, the maps, and the money path. It still agrees on several **process** seams that 013 named and that have **not** been closed: CORS is still four localhost literals; there is still no Dockerfile; listen is still launchSettings-only; `OneOptions` still has no `ValidateOnStart`; logging is still MEL console; there is still no rate limit; `appsettings.json` still has no `ConnectionStrings` section (the fallback lives in `Program.cs`); the host README still claims checkout is in-memory.

**Bar B on the host is a code path plus hermetic tests, not a lived dogfood sentence.** `WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` drives a signed `checkout.session.completed` JSON through `POST /v1/webhooks/stripe/{orgId}` against EF InMemory and asserts one `RCPT-` and a balanced journal. That is not “a buyer paid on Stripe’s hosted page.” [013/B99](../013-prods/checklists/b99-bar-b-done.md) is still all open checkboxes, including “Checkouts survive process restart (D17)” — the production store is Postgres, the **tests** never open port 5435.

**Stay Consumer-0.** The host still has no `Modules/One`, no MediatR, no BuildingBlocks, no `ProjectReference` into `apps/lazuar-api`. IsolationTests still ban those strings. One membership is still HTTP. Pay still must not become an IdP.

---

## 1. Method / SHAs / files actually opened

### 1.1 Binding coordinates

| Field | Value |
|-------|--------|
| Title | Current state of the focused Pay host |
| Date | 24 August 2026 |
| Pay repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Pay SHA | `ee2db8e5758305089a38298456c456d6bf0e97ca` (`ee2db8e5`) |
| Pay branch | `main` |
| Pay subject | `feat(pay): Bar B receipts, webhook secret, merchant money UI` |
| Host TFM | `net10.0` (`apps/lazuar-pay/global.json` pins SDK `10.0.100`, `rollForward: latestFeature`) |
| Listen (when launchSettings applies) | `http://localhost:8081` |
| Type | Uncondensed analysis. **Not** an implementation. **Not** a flip of 011/11 |

Historical SHAs this paper names only to **disagree** with:

| Paper | SHA it froze | Claim this SHA overrules |
|-------|----------------|---------------------------|
| [008-evals](../008-evals/README.md) | `4624070` on `feat/007-waves-1-4-implement` | Evaluates the **old** modular monolith after Waves 0–4. It is not an inventory of `apps/lazuar-pay`. Do not quote 008 as “new Pay has no DB / no Stripe.” |
| [013/03](../013-prods/03-host-production-seams.md) | `6f866ff0` on `feat/012-connect-one` | “`CheckoutStore` is an in-memory fixture”; zero `PackageReference`; no Postgres; six HTTP maps; no `/ready`. |
| [013 README](../013-prods/README.md) | same | “Whoami, org ready, in-memory checkout fixture.” |
| [013/01](../013-prods/01-production-ready-bar.md) §2 | same | “**in-memory** checkout fixture”; “`CheckoutStore` is a `ConcurrentDictionary`”; “No” on receipt / journal / webhook retry. |
| Host `README.md` (live file, stale sentence) | this SHA | “Checkout is an in-memory fixture (`status: open`).” The store is Postgres. Status still starts `open`. |

[014 README](./README.md) already records the correction at analysis start: focused host on 8081, “One façade, Postgres on 5435, Stripe hosted rail, PSP webhook, same-handler fulfillment.” This file is the evidence for that row.

### 1.2 What was actually opened

**Law / tracker (quoted, not flipped):**

- `plans/014-evals/README.md`
- `plans/011-new-lazuar-pay/01-product.md`, `02-one-integration.md`, `03-first-slice.md`, `11-checklist.md`, `12-first-slice-tracker.md`
- `plans/013-prods/README.md`, `01-production-ready-bar.md` (historical), `03-host-production-seams.md` (historical), `checklists/README.md`, `checklists/b99-bar-b-done.md`, `checklists/decisions.md`
- `plans/008-evals/README.md` (scope check only)

**Host process:**

- `apps/lazuar-pay/README.md`
- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/docker-compose.pay.yml`
- `apps/lazuar-pay/global.json`, `package.json`, `Lazuar.Pay.slnx`
- Every `.cs` file under `apps/lazuar-pay/src/Lazuar.Pay/` except `bin/` / `obj/`:
  - `One/*` (13 files, including `OneWebhookEndpoints.cs` whose **namespace** is `Lazuar.Pay.Webhooks`)
  - `Checkouts/*` (4)
  - `Catalog/CatalogEndpoints.cs`
  - `Data/PayDbContext.cs`, `Data/Rows.cs`, `Data/Migrations/20260821152601_Initial.cs`, Designer, Snapshot
  - `Gateways/*` (3)
  - `Money/*` (2)
  - `PublicPay/PublicPayEndpoints.cs`
  - `Secrets/SecretBox.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/*.cs` (all of them) and the test csproj
- `packages/pay-spec/main.tsp`, `packages/pay-spec/dist/openapi.yaml`, `packages/pay-spec/README.md`
- `Taskfile.yml` `pay:*` block
- `.github/workflows/ci.yml` `pay` job

**Not opened as an implementation source (out of this slice):** `apps/lazuar-pay-merchant`, `apps/lazuar-pay-checkout`, `apps/lazuar-api/Modules/Payments/**`, Hub `StripeGatewayAdapter`. Those belong to the other 014 reports.

### 1.3 Method

1. Inventory the focused host as it compiles on `ee2db8e5` (folders, DI, maps, tables, tests, spec).
2. Quote live files with path and line numbers. Do not inventory a folder from memory.
3. Compare 011/11 **Status** cells in NP-ONE / NP-CAT / NP-CHK / NP-GW / NP-FUL / NP-API to live host behaviour. List IDs. Do not edit 011.
4. Name every disagreement with 013-prods papers at `6f866ff0` and with the host README’s in-memory sentence.
5. Do not implement. Do not flip tracker cells. Do not design the five-adapter port (report 09) or the Stripe-vs-Hub diff (report 05).

---

## 2. Standing law

From [014](./README.md) and the 011/012/013 binding this host already lives under:

1. **New Pay is Consumer-0 of lazuar-one.** Do not rebuild `Modules/One`. Merchants are One humans. One tenant id **is** Pay `org_id`. Buyers are not Zitadel humans.
2. **IsolationTests ban** MediatR, BuildingBlocks, `Modules.`, `lazuar-api` project refs, and `organizations` / `users` / `members` tables.
3. **Steal HTTP judgment from Hub; do not copy the cathedral.** One process, one schema, handlers as functions. No MediatR event bus between webhook and journal.
4. **Live code is authority.** `plans/008-evals` and `013-prods` papers at HEAD `6f866ff0` are **STALE** wherever they say this host is an in-memory fixture with no DB and no Stripe.
5. **Checkout is no longer only an in-memory fixture.** Re-check `CheckoutStore`, `PayDbContext`, `Program.cs` on this SHA — done below.
6. **Listen 8081, never 8080.** Dial One on 8080 locally. CORS allows `:5178` / `:5179` only. Ops `:3003` stays denied.
7. **Receipt ≠ tax invoice.** `RCPT-…`, title Official Receipt, never VALID. Setup / amount≤0 is not paid.
8. **Wrap-rails.** Bar B first rail is Stripe hosted Checkout `mode=payment` ([013 decisions](../013-prods/checklists/decisions.md)). CHIP is the next Malaysian rail, not this host today. Billplz-class never silent debit.

011/03 pass/fail locks still bind the host even when the tracker cells have not been flipped:

```26:35:plans/011-new-lazuar-pay/03-first-slice.md
**Fail (do not paper over):**

- Pay password form or second org table.
- Buyer created as a Zitadel human.
- Setup session counted as paid.
- Receipt titled Tax Invoice or numbered with a UUID.
- Webhook retry double-journals.
- Merchant sent to `lazuar-admin`.
```

B99’s sentence is the **lived** bar, not the unit-test bar:

```11:19:plans/013-prods/checklists/b99-bar-b-done.md
- [ ] Merchant signs in through One `:5175` on origin `:5178`
- [ ] Merchant pastes **the B00 rail** keys (encrypted)
- [ ] Merchant creates a MYR product + shareable pay link
- [ ] Buyer opens `:5179/c/{token}` **without** a One account
- [ ] Buyer pays on the PSP hosted page
- [ ] Pay shows one `RCPT-` and a **balanced** journal
- [ ] Webhook retry no-ops
- [ ] Invited One `member` can see the payment; `member` cannot paste keys
- [ ] Fail locks still true (password, second org table, Zitadel buyer, setup-as-paid, Tax Invoice/UUID, double-journal, merchant sent to admin)
```

On this SHA the **host** can do several of those clauses in process (keys, MYR product, public token, webhook → receipt, replay no-op, `member` 403 on product create). The **sentence** is still unchecked. This paper does not tick B99.

---

## 3. Stale papers vs live code (name the disagreement)

### 3.1 `008-evals` is the wrong tree

[008 README](../008-evals/README.md) is dated 16 August 2026, branch `feat/007-waves-1-4-implement` (`4624070`), product line “Compliance CaaS / headless checkout.” Its ten reports inventory `apps/lazuar-api` after Waves 0–4. It does not describe `apps/lazuar-pay`. Treating 008 as “new Pay has Hub’s five adapters / Hub’s ledger / Hub’s LHDN” is a category error. Treating 008 as “new Pay has no DB and no Stripe” is also a category error — 008 never looked at this host.

### 3.2 `013-prods` host papers at `6f866ff0`

[013/03](../013-prods/03-host-production-seams.md) locked this table row:

> `| \`CheckoutStore\` is an in-memory fixture | Say so. Persistence can be sketched. Do **not** invent a 9-schema cathedral |`

and this composition-root claim:

> 37 lines of statements plus `public partial class Program;`  
> `builder.Services.AddSingleton<CheckoutStore>();`  
> maps: `/health`, `/v1/health`, whoami, org ready, checkouts.

[013/01](../013-prods/01-production-ready-bar.md) §2:

> Focused host | `apps/lazuar-pay` | **8081** | Health, whoami, dummy org-ready, **in-memory** checkout fixture

> `CheckoutStore` is a `ConcurrentDictionary`. Comment on the type: “In-memory fixture store. Not a ledger. Replace when money is real.”

> | “We write a receipt” | **No** |  
> | “The journal balances” | **No** |  
> | “Webhook retry no-ops” | **No** |

**Live disagreement on `ee2db8e5`:**

| 013-at-`6f866ff0` | Live `ee2db8e5` |
|-------------------|-----------------|
| Zero host `PackageReference` | EF Design 10.0.0, Npgsql.EF 10.0.0, Stripe.net 48.0.0 |
| `AddSingleton<CheckoutStore>()` + `ConcurrentDictionary` | `AddScoped<CheckoutStore>()` + `PayDbContext` |
| No connection string, no migrator, no `/ready` | Fallback `Host=localhost;Port=5435;Database=lazuar_pay;…`; `task pay:db:migrate`; `GET /ready` is Postgres `CanConnectAsync` |
| Six application maps | Health ×2, `/ready`, whoami, org-ready, checkouts ×2, catalog ×2, public pay ×2, gateway ×2, PSP webhook, One webhook, payments, receipts ×2 |
| No Stripe | `StripeHosted.CreateHostedUrlAsync`, `EventUtility.ValidateSignature` |
| No journal / `RCPT-` | `Fulfillment.FulfillPaidAsync` writes charges, optional subscription, journal + two lines, document sequence, audit |
| CI does not run Pay | `.github/workflows/ci.yml` job `pay` runs `dotnet test apps/lazuar-pay/Lazuar.Pay.slnx` |
| Dockerfile absent (named as a gap) | **Still absent** — 013 was right about the image and is still right |
| CORS hardcoded 5178/5179 | **Still hardcoded** — 013 was right and is still right |

The 013 **law** (one schema, no MediatR, never 8080, ready never calls One, CORS never 3003) still binds. The 013 **inventory** of this host does not.

### 3.3 Host README is stale on the same sentence

```48:50:apps/lazuar-pay/README.md
Checkout is an in-memory fixture (`status: open`). Not a real charge. Buyer has no One account.

Pay never holds a Zitadel PAT. Staff **VIEWER** is not a One tenant role (`owner` / `admin` / `member` only); `/v1/orgs/{orgId}/ready` checks `member`, not “cannot charge”.
```

Buyer-has-no-One-account is still true (public `GET /v1/pay/{token}` does not call One). `status: open` is still the **create** status. “In-memory fixture” and “not a real charge” are false: the row is in Postgres, and `POST /v1/pay/{token}/start` calls Stripe Checkout Sessions API with the org’s unwrapped key.

`pay-spec` repeats the same stale fixture line (see §11).

---

## 4. What the host is (folders, DI, routes)

### 4.1 Tree (source of truth: `apps/lazuar-pay/`, excluding `bin/` / `obj/`)

```
apps/lazuar-pay/
  .env.example                         One + ConnectionStrings__Pay + commented wrap/webhook secrets
  docker-compose.pay.yml               postgres:16-alpine, host 5435, db lazuar_pay
  global.json                          SDK 10.0.100
  Lazuar.Pay.slnx                      two projects (host + tests)
  package.json                         pnpm name lazuar-pay; scripts call dotnet
  README.md                            8081; stale in-memory sentence
  src/Lazuar.Pay/
    Lazuar.Pay.csproj                  Sdk.Web; EF + Npgsql + Stripe.net
    Program.cs                         composition root (73 lines)
    appsettings.json                   Logging, AllowedHosts, One — no ConnectionStrings
    appsettings.Development.json       Logging only
    Properties/launchSettings.json     http://localhost:8081
    Catalog/                           POST/GET products
    Checkouts/                         Postgres-backed store + merchant POST/GET
    Data/                              PayDbContext, Rows, one Initial migration
    Gateways/                          PUT/GET keys, StripeHosted, PSP webhook
    Money/                             Fulfillment + merchant payment/receipt queries
    One/                               HTTP façade + One HMAC receiver (namespace Webhooks)
    PublicPay/                         GET /v1/pay/{token}, POST …/start
    Secrets/                           AES-GCM SecretBox
  tests/Lazuar.Pay.Tests/              NUnit + WAF + EF InMemory
```

**Still absent (process-relevant, 013 named these and they remain true):**

| Absent | On `ee2db8e5` |
|--------|----------------|
| `Dockerfile` / `.dockerignore` | none under `apps/lazuar-pay/` |
| `appsettings.Production.json` | none |
| `UserSecretsId` | none |
| `Directory.Build.props` under Pay | none (Hub’s props still do not apply sideways) |
| Serilog / OpenTelemetry packages | none |
| `AddAuthentication` / JwtBearer | none (correct: forward Bearer to One) |
| `AddRateLimiter` | none |
| `UseHttpsRedirection` / HSTS / forwarded headers | none |
| Second class library / MediatR | none |
| `ProjectReference` except tests → host | none |

`packages/pay-spec/` remains a sibling package, not a C# project reference. Server URL `http://localhost:8081`.

### 4.2 `Program.cs` — the composition root, entire file

```1:73:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
using System.Text.Json;
using Lazuar.Pay.Catalog;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Gateways;
using Lazuar.Pay.Money;
using Lazuar.Pay.One;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<Fulfillment>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay")
        ?? "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", async (PayDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.CanConnectAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.Json(new { status = "not_ready" }, statusCode: 503);
    }
});
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();

app.Run();

public partial class Program;
```

Facts that must stay true:

- **JSON for `/v1` is snake_case globally** via `ConfigureHttpJsonOptions`. Health’s `{ status = "ok" }` still serializes as `{"status":"ok"}`.
- **`Testing` skips Npgsql.** `PayApiFactory` sets `UseEnvironment("Testing")` and registers EF InMemory in `ConfigureTestServices`. Laptop / CI unit tests do **not** require Docker 5435. Production and `task pay:dev` do.
- **Connection string fallback is in source**, not in `appsettings.json`. Missing env → `localhost:5435` / `postgres`/`postgres` / `lazuar_pay`. That is convenient for laptop dogfood and a footgun if a Production container is started without `ConnectionStrings__Pay`.
- **`AddDataProtection()` is registered and unused.** `SecretBox` is hand-rolled AES-GCM (see §8). Data Protection is gravity, not a vault.
- **Pipeline after `Build`:** `UseCors()` only. No `UseAuthentication`. Whoami remains an **endpoint**. Member checks are **function calls** (`MemberGate.RequireMemberAsync` / `RequireWriterAsync`).
- **`app.Run()` has no URL argument.** Listen URL is still launchSettings `http://localhost:8081`. There is still no `ASPNETCORE_URLS` under `apps/lazuar-pay`.
- **`public partial class Program;` stays.** WAF and `InternalsVisibleTo` depend on it.

### 4.3 Host csproj (entire)

```1:20:apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Lazuar.Pay.Tests" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageReference Include="Stripe.net" Version="48.0.0" />
  </ItemGroup>

</Project>
```

013’s “zero PackageReference / restore graph is empty” is **false** on this SHA. The host now depends on EF Design (migrator), Npgsql EF (one context), and Stripe.net (hosted session + webhook Event types). It still has **no** MediatR, **no** Hub `ProjectReference`, **no** BuildingBlocks. IsolationTests still fail the csproj if those substrings appear.

### 4.4 Every HTTP map

There is no `MapControllers`. Sold door is `/v1` on **8081**, not Hub’s `/api/v1` on 8080.

| Method | Path | Auth | Calls One? | Persistence | Notes |
|--------|------|------|------------|-------------|-------|
| `GET` | `/health` | none | **never** | none | `{status:ok}` |
| `GET` | `/v1/health` | none | **never** | none | same |
| `GET` | `/ready` | none | **never** | `CanConnectAsync` | 200 `{status:ready}` / 503 `{status:not_ready}` |
| `GET` | `/v1/whoami` | Bearer required; Pay does **not** validate JWT | `GET {One}/me` | none | projection `active_org_id` |
| `GET` | `/v1/orgs/{orgId}/ready` | Bearer + `MemberGate` | `POST …/authz/check` `member`/`tenant` | none | dummy admin; membership ≠ charge |
| `POST` | `/v1/checkouts` | Bearer + `MemberGate` on `body.org_id` | authz | `checkouts` + `idempotency_keys` + lazy `org_settings` | 201; `status: open`; default MYR |
| `GET` | `/v1/checkouts/{id}` | 404 if missing **before** One; else member of **session.org_id** | authz after hit | `checkouts` | other org 403 |
| `POST` | `/v1/orgs/{orgId}/products` | Bearer + **writer** (`owner`/`admin` via `/me`) | authz + `/me` | `products` + `prices` | MYR only; 201 |
| `GET` | `/v1/orgs/{orgId}/products` | Bearer + member | authz | read | nested prices |
| `PUT` | `/v1/orgs/{orgId}/gateway` | Bearer + writer | authz + `/me` | `gateway_credentials` ciphertext | provider must be `stripe` |
| `GET` | `/v1/orgs/{orgId}/gateway` | Bearer + member | authz | metadata only | `last4`, `configured`, `capability: hosted_link` |
| `GET` | `/v1/pay/{token}` | **none** | **never** | `checkouts` by `PublicToken` | 404 unknown; no org internals |
| `POST` | `/v1/pay/{token}/start` | **none** | **never** | payer fields + `PspRedirectUrl` | Stripe hosted URL; 409 if paid/expired; 403 if charges paused |
| `POST` | `/v1/webhooks/{provider}/{orgId}` | Stripe-Signature + `Pay:StripeWebhookSecret` | **never** | `psp_webhook_events` then `Fulfillment` | empty body 400; unknown provider 400 |
| `POST` | `/v1/one/webhooks` | `X-Lazuar-Signature` HMAC vs `Pay:OneWebhookSecret` | **never** | `one_webhook_events`; may set `ChargesPaused` | missing secret 503 |
| `GET` | `/v1/orgs/{orgId}/payments` | Bearer + member | authz | `charges` | |
| `GET` | `/v1/orgs/{orgId}/receipts` | Bearer + member | authz | `documents` | missing number → `"PENDING"` |
| `GET` | `/v1/orgs/{orgId}/receipts/{id}` | Bearer + member | authz | `documents` | 404 if wrong org |

CORS: `UseCors()` default policy on every map. Origins are the four literals in `Program.cs`. `AllowAnyHeader` + `AllowAnyMethod`, **no** `AllowCredentials`. `CorsTests` lock 5178/5179 allow and **3003 / 3004 deny**.

### 4.5 DI inventory

| Registration | Lifetime | Notes |
|--------------|----------|--------|
| `IOptions<OneOptions>` | options | `BindConfiguration("One")`. Defaults `BaseUrl = http://localhost:8080/api/v1`, `TimeoutSeconds = 5`. **No** `ValidateOnStart` |
| `OneClient` | typed HttpClient | Constructor sets `BaseAddress` + Timeout. **No** `DefaultRequestHeaders.Authorization` from config |
| `IDataProtectionProvider` | via `AddDataProtection()` | **unused** by application code |
| `SecretBox` | singleton | AES-GCM; key from `Pay:WrapKey` or hardcoded SHA256 of a dev string |
| `CheckoutStore` | **scoped** | holds `PayDbContext`; not a dictionary |
| `StripeHosted` | scoped | Stripe.net `SessionService` |
| `Fulfillment` | scoped | same DbContext as the webhook request |
| `PayDbContext` | scoped (EF) | Npgsql unless `Testing` |
| CORS default policy | singleton policy | four localhost origins |

**Not registered:** MediatR, `IEventBus`, `IJwtService`, `ISecretVault`, hosted services, authentication schemes, exception handler, rate limiter, `ICheckoutRepository`.

### 4.6 Config as it sits on disk

`appsettings.json` (entire):

```1:13:apps/lazuar-pay/src/Lazuar.Pay/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "One": {
    "BaseUrl": "http://localhost:8080/api/v1",
    "TimeoutSeconds": 5
  }
}
```

`appsettings.Development.json` is logging only. `launchSettings.json` is one profile `http`, `applicationUrl: http://localhost:8081`, `ASPNETCORE_ENVIRONMENT=Development`.

`.env.example` (entire) is documentation; `Program.cs` still does **not** load a `.env` file:

```1:15:apps/lazuar-pay/.env.example
# One HTTP façade (no PAT, no OpenFGA admin).
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5

# Greenfield Pay DB on host 5435. Not One lazuar, not Hub lazuar_mvp.
ConnectionStrings__Pay=Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres

# 32-byte base64 wrap key for BYOK. Dev has a fallback; production must set this.
# Pay__WrapKey=

# Stripe webhook signing secret (whsec_…). Checkout secret key is BYOK per org.
# Pay__StripeWebhookSecret=

# HMAC secret for POST /v1/one/webhooks
# Pay__OneWebhookSecret=
```

Env names that **code** actually reads: `One:BaseUrl`, `One:TimeoutSeconds`, `ConnectionStrings:Pay`, `Pay:WrapKey`, `Pay:StripeWebhookSecret`, `Pay:OneWebhookSecret`. There is still no `Jwt:Secret`, no `Kms:MasterKey`, no Zitadel PAT.

### 4.7 Tasks, compose, CI, listen

`Taskfile.yml` focused-Pay block:

```90:141:Taskfile.yml
  pay:restore:
    desc: Restore the focused Pay solution
    dir: apps/lazuar-pay
    cmds:
      - dotnet restore Lazuar.Pay.slnx

  pay:build:
    desc: Build the focused Pay solution
    deps: [pay:restore]
    dir: apps/lazuar-pay
    cmds:
      - dotnet build Lazuar.Pay.slnx --nologo --verbosity minimal

  pay:test:
    desc: Test the focused Pay host (health + isolation)
    dir: apps/lazuar-pay
    cmds:
      - dotnet test Lazuar.Pay.slnx --nologo --verbosity minimal

  pay:dev:
    desc: Run focused Pay on http://localhost:8081 (old API stays on 8080)
    dir: apps/lazuar-pay
    cmds:
      - dotnet watch run --project src/Lazuar.Pay/Lazuar.Pay.csproj

  pay:db:up:
    desc: Start greenfield Pay Postgres on localhost:5435
    dir: apps/lazuar-pay
    cmds:
      - docker compose -f docker-compose.pay.yml up -d

  pay:db:migrate:
    desc: Apply PayDbContext migrations (one context)
    dir: apps/lazuar-pay
    cmds:
      - dotnet ef database update --project src/Lazuar.Pay/Lazuar.Pay.csproj --context PayDbContext

  pay:spec:
    desc: Compile focused Pay TypeSpec to OpenAPI (packages/pay-spec, not api-spec)
    ...
  pay:merchant: ... :5178 ...
  pay:checkout: ... :5179 ...
```

`pay:test` description is still “health + isolation”; behaviour is the whole test project (whoami, CORS, org ready, checkout, catalog, public pay, webhooks). Cosmetic drift.

`docker-compose.pay.yml` (entire):

```1:16:apps/lazuar-pay/docker-compose.pay.yml
# Greenfield Pay Postgres only. Not Hub lazuar_mvp. Not One lazuar.
# Publish 5435 so it does not fight One's default 5432.
services:
  pay-db:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: lazuar_pay
    ports:
      - "5435:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d lazuar_pay"]
      interval: 5s
      timeout: 5s
      retries: 10
```

Root Hub `docker-compose.yml` is still the cathedral on 8080. Focused Pay is **not** a service there. README still says “Compose still points at `apps/lazuar-api`. Swap later.”

CI **does** run this host (013’s “CI can go green while Lazuar.Pay.slnx is red” is stale):

```96:118:.github/workflows/ci.yml
  pay:
    runs-on: ubuntu-latest
    steps:
      ...
      - name: Test focused Pay host
        run: dotnet test apps/lazuar-pay/Lazuar.Pay.slnx --nologo --verbosity minimal
      - name: Build merchant and checkout
        run: |
          pnpm --filter lazuar-pay-merchant build
          pnpm --filter lazuar-pay-checkout build
      - name: Compile pay-spec
        run: pnpm --filter @repo/pay-spec exec tsp compile .
```

That job is hermetic InMemory + FakeOne. It does not start `docker-compose.pay.yml`. It does not hit Stripe. That matches B99.2 “`task pay:test` green without Zitadel/CHIP network” as a **test** bar, not as a lived charge.

---

## 5. Persistence: `PayDbContext`, tables, migrations, connection string, `/ready`

### 5.1 One context, public schema

```1:28:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Data;

/// <summary>One context, public schema. Not nine module DbContexts.</summary>
public sealed class PayDbContext(DbContextOptions<PayDbContext> options) : DbContext(options)
{
    public DbSet<OrgSettingsRow> OrgSettings => Set<OrgSettingsRow>();
    public DbSet<CheckoutRow> Checkouts => Set<CheckoutRow>();
    public DbSet<IdempotencyKeyRow> IdempotencyKeys => Set<IdempotencyKeyRow>();
    public DbSet<ProductRow> Products => Set<ProductRow>();
    public DbSet<PriceRow> Prices => Set<PriceRow>();
    public DbSet<GatewayCredentialRow> GatewayCredentials => Set<GatewayCredentialRow>();
    public DbSet<PspWebhookEventRow> PspWebhookEvents => Set<PspWebhookEventRow>();
    public DbSet<ChargeRow> Charges => Set<ChargeRow>();
    public DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();
    public DbSet<JournalEntryRow> JournalEntries => Set<JournalEntryRow>();
    public DbSet<JournalLineRow> JournalLines => Set<JournalLineRow>();
    public DbSet<DocumentRow> Documents => Set<DocumentRow>();
    public DbSet<DocumentSequenceRow> DocumentSequences => Set<DocumentSequenceRow>();
    public DbSet<PayerRow> Payers => Set<PayerRow>();
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();
    public DbSet<MailOutboxRow> MailOutbox => Set<MailOutboxRow>();
    public DbSet<OneWebhookEventRow> OneWebhookEvents => Set<OneWebhookEventRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("public");
```

Table names and keys (from the same file’s `OnModelCreating`):

| Table | Key | Extra |
|-------|-----|--------|
| `org_settings` | `OrgId` | `Currency`, `ChargesPaused`, `SstRegistered?` |
| `checkouts` | `Id` | unique `PublicToken`; index `OrgId`; amount `numeric(18,2)` |
| `idempotency_keys` | `(OrgId, Key)` | `CheckoutId` |
| `products` | `Id` | index `OrgId` |
| `prices` | `Id` | amount precision |
| `gateway_credentials` | `(OrgId, Provider)` | `Ciphertext`, `Last4` |
| `psp_webhook_events` | `(OrgId, Provider, EventId)` | |
| `charges` | `Id` | |
| `subscriptions` | `Id` | |
| `journal_entries` | `Id` | |
| `journal_lines` | `Id` | |
| `documents` | `Id` | `Number` nullable; `Title` |
| `document_sequences` | `(OrgId, Series, YearMyt)` | `LastN` |
| `payers` | `Id` | email/name; **not** a Zitadel user |
| `audit_events` | `Id` | |
| `mail_outbox` | `Id` | **table only; no writer in host code** |
| `one_webhook_events` | `Id` | unique `DeliveryId` |

IsolationTests lock the refuse list:

```36:45:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    public void Source_does_not_create_org_or_user_tables()
    {
        var src = Path.Combine(FindPayRoot(), "src");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain("ToTable(\"organizations\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"users\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"members\")"), file);
        }
    }
```

There is **no** `HasDefaultSchema("commerce")` / `"billing"` / `"payments"`. 013’s candidate table names landed as **one** context. That is the persistence shape 013/03 §4 asked for, now compiled.

### 5.2 Row types that matter for honesty

`OrgSettingsRow` is Pay-only settings keyed by One tenant id, not a membership table:

```3:10:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class OrgSettingsRow
{
    public required string OrgId { get; set; }
    public string Currency { get; set; } = "MYR";
    public bool ChargesPaused { get; set; }
    /// <summary>null = unknown (fail closed for SST). true/false when merchant set it.</summary>
    public bool? SstRegistered { get; set; }
}
```

`CheckoutRow` is the durable session 013 said did not exist:

```12:28:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class CheckoutRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public string Interval { get; set; } = "one_off";
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? PspRedirectUrl { get; set; }
    public string? PayerName { get; set; }
    public string? PayerEmail { get; set; }
    public string? ProductId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

`ProductId` exists on the row and is **never written** by `CheckoutEndpoints` or catalog. Catalog and checkout are adjacent tables, not a join at create time.

### 5.3 One migrator

`Data/Migrations/20260821152601_Initial.cs` is the only migration. It `EnsureSchema("public")` and `CreateTable`s every table listed above. `task pay:db:migrate` runs `dotnet ef database update --context PayDbContext`. There is **no** boot-time `MigrateAsync` in `Program.cs`. A process started against an empty 5435 without that task will 500 on first write; `/ready` will still 200 if Postgres accepts connections. That is honest liveness vs a schema version probe (the latter does not exist).

Designer/snapshot pin `ProductVersion` 10.0.0 and `HasDefaultSchema("public")`.

### 5.4 `/ready` vs `/health`

Liveness is local JSON and is tested not to call One (`HealthTests.Health_does_not_call_one`). Readiness is **Postgres only**:

```48:59:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
app.MapGet("/ready", async (PayDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Database.CanConnectAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.Json(new { status = "not_ready" }, statusCode: 503);
    }
});
```

There is **no** `Ready_does_not_call_one` test. There is **no** test that `/ready` returns 503 when Postgres is down. `HealthTests` and `CorsTests` use raw `WebApplicationFactory<Program>()` (environment Development), which **does** register Npgsql against the fallback connection string. They only hit `/health`, so they stay green without 5435. Hitting `/ready` in that factory without Postgres would be 503 — untested.

Path is `/ready`, not Hub’s `/health/ready`. TypeSpec does not document it. Unversioned `/health` is also absent from TypeSpec (same as at `6f866ff0`).

### 5.5 Tests do not lock durability

`PayApiFactory`:

```21:47:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PayApiFactory.cs
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Pay:StripeWebhookSecret", StripeWebhookSecret);
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(s => s.ServiceType == typeof(OneClient)).ToList())
            {
                services.Remove(d);
            }

            services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddTransient(_ =>
            {
                var http = new HttpClient(One, disposeHandler: false)
                {
                    BaseAddress = new Uri("http://one.test/api/v1/"),
                    Timeout = TimeSpan.FromSeconds(2)
                };
                return new OneClient(http, Options.Create(new OneOptions
                {
                    BaseUrl = "http://one.test/api/v1",
                    TimeoutSeconds = 2
                }));
            });
        });
    }
```

013 decisions locked “Tests may use EF InMemory; prod is Npgsql on **5435**.” That is what shipped. Consequences:

- Idempotency-across-restart is **not** proven. The unique `(OrgId, Key)` exists in the migration; tests only prove in-process InMemory.
- `Fulfillment`’s `BeginTransactionAsync` is **ignored** by InMemory (`TransactionIgnoredWarning`). Same-TX journal+receipt is not proven on Postgres.
- `psp_webhook_events` insert then `Fulfillment` is two `SaveChanges` even on Postgres (see §9). InMemory hides that further.

B99.2 “Checkouts survive process restart (D17)” remains an **unchecked** operational claim. The table exists; the test job does not recycle a process against 5435.

---

## 6. One façade: whoami, org ready, `OneClient`, `MemberGate`, One webhooks

### 6.1 Trust model (unchanged from 012, still correct)

Pay does not mint tokens. `Bearer.TryGet` requires scheme `Bearer ` and a non-whitespace remainder. Missing/empty/non-Bearer → 401 **without** calling One.

```1:21:apps/lazuar-pay/src/Lazuar.Pay/One/Bearer.cs
internal static class Bearer
{
    public static bool TryGet(HttpRequest request, out string authorization)
    {
        authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return authorization.Length > prefix.Length && !string.IsNullOrWhiteSpace(authorization[prefix.Length..]);
    }
}
```

`OneClient` forwards that header verbatim (`TryAddWithoutValidation`) to `me` and `tenants/{orgId}/authz/check`. Optional `X-Lazuar-Tenant-Id` is a **hint**, never the object id of `authz/check`. Timeouts and `HttpRequestException` become `TimedOut` / `TransportFailed`. There is still no Polly, no retry, no `ILogger<OneClient>`, no `lzr_sk_` on `OneOptions`.

Authz body is still `{ relation: "member", object: { type: "tenant", id: orgId } }`. OrgReadyTests lock: path org, not header org; body does not contain `user_id`.

Whoami mapping still drops One `active_role` and `name`; copies `active_tenant_id` → `active_org_id`. Status map still collapses One 500 / garbage / missing `user_id` to **503**. Tests still lock that (`Whoami_maps_one_500_to_503`, timeout 503). 012-03 wanted 502 vs 503; implementation did not split; do not flip casually.

### 6.2 `MemberGate` grew a writer

At `6f866ff0` there was only `RequireMemberAsync`. Live code adds `RequireWriterAsync`: member first, then `/me`, then tenant `role` must be `owner` or `admin`.

```42:69:apps/lazuar-pay/src/Lazuar.Pay/One/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        Bearer.TryGet(request, out var authorization);
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var who = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        if (who.Value is null)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider failed");
        }

        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }

        return null;
    }
```

Used by: catalog **create**, gateway **PUT**. Not used by: checkout create (any **member** may open a checkout), payments GET, receipts GET, gateway GET, org ready. That is the 013 VIEWER honesty applied as One `member` vs `owner`/`admin`: dummy `/ready` is still `check(member)`; money **writes** that exist (keys, products) require writer. Refunds do not exist, so NP-ONE-021 “cannot refund” has nothing to hang on.

`RequireWriterAsync` does **not** call `authz/check` with relation `admin`. It trusts `/me`’s `tenants[].role`. 011 said “Roles from `/me` + `authz/check`, not Zitadel project-role claims.” The writer path is `/me` role after a `member` check. That is Pay-enforced using One’s projection, not a second claim parse. It is also a second `/me` call on every product/key write (JIT-join amplifier 012 warned about). There is no cache.

### 6.3 Org ready is still dummy admin

```1:23:apps/lazuar-pay/src/Lazuar.Pay/One/OrgReadyEndpoints.cs
internal static class OrgReadyEndpoints
{
    public static void MapOrgReady(this WebApplication app)
    {
        app.MapGet("/v1/orgs/{orgId}/ready", Handle);
    }

    static async Task<IResult> Handle(
        string orgId,
        HttpRequest request,
        OneClient one,
        CancellationToken cancellationToken)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = true }, OneClient.Json);
    }
}
```

`ready: true` means “One said this Bearer is a member of this tenant.” It does not mean rail configured, SST known, or charges unpaused. 013/01 §2.4 still applies: flipping NP-ONE-021 because this returned 200 would be a lie. The host did **not** do that; 011 still has 021 `todo`.

### 6.4 One HMAC receiver (Pay half of NP-ONE-017 / 018)

File lives under `One/OneWebhookEndpoints.cs`; namespace is `Lazuar.Pay.Webhooks` so `Program.cs` `using Lazuar.Pay.Webhooks` compiles.

```12:79:apps/lazuar-pay/src/Lazuar.Pay/One/OneWebhookEndpoints.cs
    public static void MapOneWebhooks(this WebApplication app)
    {
        app.MapPost("/v1/one/webhooks", Handle);
    }
    // ...
        var secret = config["Pay:OneWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PayErrors.Status(503, "Service Unavailable", "One webhook secret missing");
        }

        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(json)));
        // FixedTimeEquals on UTF-8 bytes of hex strings (length must match)
        ...
        if (await db.OneWebhookEvents.AnyAsync(x => x.DeliveryId == delivery, ct))
        {
            return Results.Ok(new { duplicate = true });
        }
        ...
        if (type == "tenant.suspended" && !string.IsNullOrWhiteSpace(orgId))
        {
            // upsert org_settings.ChargesPaused = true
        }
        if (type == "tenant.reactivated" && !string.IsNullOrWhiteSpace(orgId))
        {
            // ChargesPaused = false if row exists
        }
```

Honesty:

- **Route exists.** Different path from PSP (`/v1/one/webhooks` vs `/v1/webhooks/{provider}/{orgId}`). Different table (`one_webhook_events` vs `psp_webhook_events`). That matches O14/O17.
- **Pay does not subscribe.** NP-ONE-017’s “Subscribe to `member.*` and `tenant.suspended`” is an One-side registration job. This host only receives. There is no `lzr_sk_` worker that calls One’s webhook register API.
- **HMAC format is homemade.** `Convert.ToHexString` is **uppercase** hex of HMAC-SHA256(secret, raw body). No `sha256=` prefix, no timestamp, no `t=,v1=` Stripe-style header. If One signs with lowercase hex or a prefixed scheme, every delivery 401s. **There is no test** for this route at all.
- **Empty body is not 400.** Empty/whitespace JSON is parsed as `{}` after HMAC. PSP empty body **is** 400. Asymmetry.
- **`tenant.suspended` stops new charges** if `org_id` is present on the JSON: checkout create and public start both 403 `Org charges are paused`. Money already captured is not reversed (correct). Staff access is **not** stopped (whoami/org-ready/payments GET still work). NP-ONE-018 “and staff access” is only half-done on the host.
- **`member.*` is stored as an event type and otherwise ignored.** No roster cache (there is no roster table; correct).
- **`tenant.created` does not provision catalog rows.** NP-ONE-019 still a lazy upsert on first checkout (`OrgSettings` created in `CheckoutEndpoints`).

Checkout create is where paused charges and SST default meet:

```29:40:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var settings = orgId is null ? null : await db.OrgSettings.FindAsync([orgId], cancellationToken);
        if (settings is null && orgId is not null)
        {
            settings = new OrgSettingsRow { OrgId = orgId, SstRegistered = false };
            db.OrgSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (settings?.ChargesPaused == true)
        {
            return PayErrors.Status(403, "Forbidden", "Org charges are paused");
        }
```

Lazy upsert sets `SstRegistered = false`, **not** `null`. The row comment says `null = unknown (fail closed)`. First checkout therefore marks the merchant as “not SST-registered” without anyone answering the question. Fulfillment’s fail-closed path (throw if `SstRegistered is null`) never fires on this create path. That is a live disagreement with NP-MON-004 / F18, owned as a host money seam even though this slice is not the SST paper.

### 6.5 What the host does **not** implement (One product)

These 011 rows are One’s HTTP or the merchant SPA. The host has **no** routes for them:

| ID | Host evidence |
|----|----------------|
| NP-ONE-001/002/004/005 | No OIDC. No `client_id` in Pay appsettings. Login is `:5175` (Vite/One). |
| NP-ONE-009 | No `POST /v1/tenants`. Workspace create is One `POST /tenants` from the SPA. |
| NP-ONE-010 | No Pay tenant profile PATCH. |
| NP-ONE-011/012/013 | No invite/roster routes. IsolationTests forbid `members` table. |
| NP-ONE-014 | No mint `lzr_sk_`. Pay **will forward** a Bearer that looks like one if One accepts it on `/me` and `authz/check`. Untested. |
| NP-ONE-016 | No `authz/batch-check`. |
| NP-ONE-022 | Invited member **seeing ops** is a SPA chrome job. Host: member can GET products/payments/receipts/gateway metadata; cannot POST products or PUT keys. |

NP-ONE-003/006/007/008/015 remain **done** in 011 and remain true in code.

---

## 7. Checkouts + catalog + public pay

### 7.1 `CheckoutStore` is Postgres (the 013 lock is dead)

```1:50:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Checkouts;

/// <summary>Postgres-backed checkouts. Not a ledger.</summary>
public sealed class CheckoutStore(PayDbContext db)
{
    public async Task<CheckoutSession> CreateAsync(CheckoutSession session, string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingKey = await db.IdempotencyKeys.FindAsync([session.OrgId, idempotencyKey], ct);
            if (existingKey is not null)
            {
                var existing = await db.Checkouts.FindAsync([existingKey.CheckoutId], ct);
                if (existing is not null)
                {
                    return Map(existing);
                }
            }
        }
        // INSERT checkout + optional idempotency_keys row
        await db.SaveChangesAsync(ct);
        return Map(row);
    }
```

Comment on the type is no longer “In-memory fixture store.” There is **no** `ConcurrentDictionary` in `apps/lazuar-pay/src`. Idempotency is `(org_id, key)` → `checkout_id` as 013 sketched. `GetByPublicTokenAsync` is the buyer door.

Gaps inside an otherwise real store:

- No `ON CONFLICT` — a racing double-POST with the same key can unique-constraint-fail instead of returning the first row.
- Create always sets `Interval = "one_off"` in the **endpoint**, ignoring catalog price interval.
- `ProductId` is never set.
- Status is `"open"` at insert. `"paid"` is written only by `Fulfillment`. `"expired"` is **never written** anywhere in `src/`. Public start 409s if status is `"expired"`, a branch that cannot be reached without a raw SQL/update.

### 7.2 Merchant checkout HTTP

Create: member of `body.org_id`, amount > 0, currency default MYR uppercased, `Idempotency-Key` header or body, 201 snake_case session, `PublicToken` = two concatenated hex GUIDs.

```54:69:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var session = new CheckoutSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = orgId!,
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = body.Amount.Value,
            Currency = currency,
            Status = "open",
            Interval = "one_off",
            SuccessUrl = body.SuccessUrl,
            CancelUrl = body.CancelUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session = await store.CreateAsync(session, idempotency, cancellationToken);
        return Results.Json(session, OneClient.Json, statusCode: 201);
```

GET: unknown id 404 **without** One (existence oracle, same as 013). Known id then `MemberGate` on **session.org_id**. CheckoutTests lock create/get, other-org 403, idempotent key, default MYR, amount 0 → 400, no-bearer 401.

NP-CHK-001/002/003 are `done` in 011 and remain true, except 003 is no longer “in-memory only” — the **code** persists the key; the **tests** still use InMemory.

Shareable pay identifier is `public_token`, not checkout `id`. Merchant GET stays member-gated. That matches 013 decisions (“Public pay identifier = `token` on `/v1/pay/{token}`”).

### 7.3 Catalog

```9:62:apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs
    public static void MapCatalog(this WebApplication app)
    {
        app.MapPost("/v1/orgs/{orgId}/products", Create);
        app.MapGet("/v1/orgs/{orgId}/products", List);
    }
    // Create: RequireWriterAsync; name required; currency default MYR and **must** be MYR;
    // amount > 0; insert ProductRow + PriceRow (interval default one_off); 201
```

List is member-gated and nests prices. There is **no** PATCH/DELETE product, **no** seats/quantity column, **no** “create checkout from product id” route. A merchant who creates a product still has to `POST /v1/checkouts` with a raw amount; nothing copies `PriceRow.Amount` onto the session. Dogfood “product + pay link” is two unjoined writes plus the SPA.

`CatalogTests`: owner 201; `member` 403 on create. No list test, no non-MYR 400 test, no 401 test.

### 7.4 Public pay (buyer plane on the host)

```12:80:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
```

GET returns `{ token, amount, currency, status, payer_name, payer_email }`. No `org_id`. Unknown token 404, SendCount 0. Repeat GET does not call One (`PublicPayTests`). That is NP-CHK-007 on the **host**: this route cannot log the buyer into Zitadel because it never looks at Authorization.

START: 404 missing; 409 if `paid` or `expired`; 403 if `ChargesPaused`; writes optional payer name/email onto the checkout row; `StripeHosted.CreateHostedUrlAsync`; stores `PspRedirectUrl`; returns `{ redirect_url }`. `InvalidOperationException` (rail not configured) and `StripeException` → 503.

There is **no** test that `/start` returns a URL. Doing so would need Stripe or a test double of `StripeHosted`. `StripeHosted` is a concrete class registered in `Program.cs`; tests do not replace it. PublicPayTests’ third method is `Empty_webhook_is_400` — a webhook assertion living in the wrong fixture.

Payer fields: written on start if the SPA sends them; **not** required. NP-BUY-001 is “on the checkout session” — columns exist; the merchant create path does not set them.

K11 asked for “merchant display” on the public DTO. Live GET has no merchant name. Org internals are correctly omitted.

---

## 8. Gateways + webhooks + secrets (existence and honesty, not a Stripe port)

This section records **what the host has**. Diff vs Hub `StripeGatewayAdapter` is report 05. Port architecture is report 09.

### 8.1 BYOK keys

`PUT /v1/orgs/{orgId}/gateway` is writer-gated. Provider must be `stripe`. Secret required. Last4 stored. Ciphertext via `SecretBox.Protect`. GET returns metadata, never ciphertext, `capability = "hosted_link"`.

```28:60:apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs
        if (provider != StripeHosted.Provider)
        {
            return PayErrors.Status(400, "Bad Request", "Bar B first rail is stripe");
        }
        ...
        var wrapped = box.Protect(secret);
        ...
        return Results.Json(new { org_id = orgId, provider, last4, capability = "hosted_link" }, OneClient.Json);
```

Honesty of wrap-rails **label**: `hosted_link` is the capability string. There is no silent off-session charge path in this host. `StripeHosted` uses Checkout Session `Mode = "payment"` only. NP-GW-007’s “honest matrix” for Billplz-class is vacuous until a second rail exists; the Stripe path is labelled hosted, which is the truth.

NP-GW-003 (CHIP or Billplz) is **not** present. `400 Bar B first rail is stripe` is the lock.

Audit on key change (NP-AUD-003): **not written**. `Fulfillment` writes `checkout.paid` audit; gateway PUT does not insert `AuditEventRow`.

No host test that `member` cannot PUT keys. CatalogTests cover member-cannot-create-product; G14 is implied by the same `RequireWriterAsync` but not locked on this route.

### 8.2 `SecretBox`

```1:52:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
/// <summary>AES-GCM wrap for BYOK. Key from Pay:WrapKey (32-byte base64). Never log plaintext.</summary>
public sealed class SecretBox(IConfiguration config)
{
    public string Protect(string plaintext) { ... AesGcm ... }
    public string Unprotect(string wrapped) { ... }
    byte[] LoadKey()
    {
        var b64 = config["Pay:WrapKey"];
        if (string.IsNullOrWhiteSpace(b64))
        {
            // Dev/test only. Production must set Pay:WrapKey.
            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
        }
        ...
    }
}
```

Production-shaped **if** `Pay:WrapKey` is set. If not, every replica that uses the fallback can decrypt every ciphertext, and the fallback is a string in source. `AddDataProtection()` does not participate. Envelope via cloud KMS is not here. 013’s “never `Kms:MasterKey` copied from Hub” holds — the name is `Pay:WrapKey`.

### 8.3 Stripe hosted session (existence)

```9:47:apps/lazuar-pay/src/Lazuar.Pay/Gateways/StripeHosted.cs
public sealed class StripeHosted(PayDbContext db, SecretBox box)
{
    public const string Provider = "stripe";

    public async Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == Provider, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
        var cents = (long)Math.Round(checkout.Amount * 100m, MidpointRounding.AwayFromZero);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = checkout.Id,
            SuccessUrl = checkout.SuccessUrl ?? "http://localhost:5179/c/" + checkout.PublicToken + "?status=verifying",
            CancelUrl = checkout.CancelUrl ?? "http://localhost:5179/c/" + checkout.PublicToken,
            Metadata = new Dictionary<string, string> { ["checkout_id"] = checkout.Id, ["org_id"] = checkout.OrgId },
            LineItems = [ /* Quantity = 1, Currency = checkout.Currency, UnitAmount = cents, Name = "Pay" */ ]
        }, cancellationToken: ct);
        return session.Url ?? throw new InvalidOperationException("Stripe returned no URL");
    }
}
```

Facts for later Stripe-port paper, recorded so this host paper does not pretend the adapter is Hub’s:

- Uses **the org’s secret key** as `StripeClient(secret)` — true BYOK for the **create session** call.
- `Mode = "payment"` — not `setup`, not `subscription`. NP-GW-008’s setup-not-paid is also enforced on the webhook (below).
- Default success URL is the checkout SPA with `?status=verifying`. Host does **not** treat success_url as paid (K19). Public GET is the status poll.
- Line item name is the literal `"Pay"`, not the catalog product name (`ProductId` was never set).
- Quantity is always 1. No seats.
- Amount is major units × 100. MYR has no subunit in the real world in the same way; this is Stripe’s smallest-unit convention. Fine for dogfood; not SST math.

### 8.4 PSP webhook

```12:103:apps/lazuar-pay/src/Lazuar.Pay/Gateways/WebhookEndpoints.cs
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
        // provider must be stripe
        // empty body → 400
        // rail must be configured for that orgId
        // Pay:StripeWebhookSecret missing → 503
        // EventUtility.ValidateSignature then ConstructEvent (throwOnApiVersionMismatch: false)
        // existing (orgId, stripe, event.Id) → { duplicate: true }
        // insert psp_webhook_events; SaveChanges
        // if type checkout.session.completed:
        //   ignore mode==setup or amount_total null/0
        //   else FulfillPaidAsync(client_reference_id or metadata.checkout_id)
```

Honesty:

- **Signature is a process secret**, `Pay:StripeWebhookSecret`, **not** stored per org next to the BYOK sk. Merchant pastes `sk_test_…`; webhook `whsec_…` is Pay’s env. True multi-tenant BYOK (each merchant’s Stripe account has its own endpoint secret) is **not** implemented. Dogfood with **one** Stripe account and one `whsec` can work. That is a different product than “paste your keys and we verify **your** webhook secret.”
- **Empty body 400** is locked (`PublicPayTests.Empty_webhook_is_400` and the handler). NP-GW-005.
- **Idempotency** is unique `(org_id, provider, event_id)` inserted **before** fulfill, in a **separate** `SaveChanges`. Replay returns `{ duplicate: true }` without calling fulfill. `WebhookTests` asserts one document after replay.
- **Split transaction risk:** if `FulfillPaidAsync` throws after the event row is committed, retry no-ops **and** the checkout stays `open`. InMemory tests never see this. On Postgres this is a real poison-pill: a permanent SST throw (`SstRegistered is null`) would swallow every replay. The create path currently sets `SstRegistered = false`, so the throw does not fire on the happy path.
- **Setup not paid:** `session.Mode == "setup" || AmountTotal is null or 0` returns `{ ignored: "setup_or_zero" }`. **No dedicated test.** NP-GW-008 is implemented, not locked.
- **No `customer.subscription.*` listener.** Only `checkout.session.completed`. NP-XX-012 / G23 hold.
- **Path `orgId` is not checked against session metadata `org_id`.** A webhook posted to `/v1/webhooks/stripe/wrong-org` with a valid signature and a `client_reference_id` of another org’s checkout will still fulfill that checkout if the event id is new for `wrong-org`. Signature is platform-wide, not per-org, so this is forgeable only with the platform `whsec` — still a tenant-isolation hole (NP-API-005).
- Unknown provider 400. CHIP/Billplz URLs do not exist.

---

## 9. Fulfillment + journal + receipts + queries

### 9.1 Same handler, not MediatR

Webhook HTTP calls `fulfillment.FulfillPaidAsync(...)` in-process. There is no `IEventBus`, no `CheckoutPaidIntegrationEvent`, no outbox. F10’s “same handler” is true as **process**. It is **not** true as **one database transaction with the webhook event row** (event `SaveChanges` already returned).

`Fulfillment` itself opens a transaction for the money write:

```7:128:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
public sealed class Fulfillment(PayDbContext db)
{
    public async Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null) { return; }
        if (checkout.Amount <= 0) { return; }
        if (checkout.Status != "open")
        {
            await tx.CommitAsync(ct);
            return;
        }

        var settings = await db.OrgSettings.FindAsync([checkout.OrgId], ct);
        if (settings?.SstRegistered is null)
        {
            throw new InvalidOperationException("SST registration unknown; fail closed");
        }

        checkout.Status = "paid";
        db.Charges.Add(... Status = "paid" ...);
        // optional PayerRow if name/email present
        // SubscriptionRow only if checkout.Interval is "mo" or "yr"
        // JournalEntry + cash D + revenue C for checkout.Amount
        // DocumentSequences RCPT / MalaysiaTime.Year ; Number = $"RCPT-{year}-{seq.LastN:00000}"
        // Document Title = "Official Receipt"
        // AuditEvent Action = "checkout.paid"
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

`MalaysiaTime.Year` uses `Asia/Kuala_Lumpur` / Windows `Singapore Standard Time`. Number format matches F14’s `RCPT-{MYT year}-#####` with five-digit `LastN`. Missing number on **read** is `"PENDING"` (query layer). Fulfill always writes a number; it never leaves `Number` null on the happy path.

### 9.2 What fulfillment actually does vs 011

| 011 / 013 ask | Live |
|---------------|------|
| Same handler paid + ledger | Yes, in `FulfillPaidAsync`. Webhook event is **outside** this TX |
| CAS open → paid | `if (checkout.Status != "open") return`. No rowversion / `UPDATE … WHERE status='open'` |
| Subscription **or** one-off | Subscription only if interval `mo`/`yr`. Merchant checkout **always** inserts `one_off`. Catalog may store `mo`/`yr` on the price and it is **not copied**. Seat path is dead on the sold create route |
| Buyer access = Pay row | Paid checkout + optional subscription. No One grant. NP-FUL-002 holds vacuously for one-off |
| Balanced journal cash/revenue/tax/fee | **cash D + revenue C** for the full amount. No tax line. No fee line. `unknown ≠ 0` is not modelled; fee is omitted (closer to “unknown” than to “0”, but there is no `unknown` flag) |
| SST exclusive on unit × seats; fail closed if unknown | Fail-closed throw if `SstRegistered is null`. Create path sets `false`. Qty=1 always. No SST math stolen from Hub |
| Amount≤0 does not mint RCPT | Early return before charge/document. Zero-amount is also rejected at checkout create |
| Official Receipt; never Tax Invoice; never UUID number | Title is the literal `"Official Receipt"`. Number is `RCPT-{year}-{n}`. Checkout **id** is still a dashless GUID; it is not printed as the document number |
| Audit same TX as the write | `checkout.paid` is in the fulfillment TX. Gateway-key audit is missing. Mail outbox is never inserted |
| Merchant list payments + open receipt | `GET …/payments`, `GET …/receipts`, `GET …/receipts/{id}` exist. **No tests.** No subscribers list (NP-FUL-003 “payments + subscribers”) |

`WebhookTests.Completed_session_writes_receipt_and_replay_is_noop` is the only lock for this kernel:

```81:116:apps/lazuar-pay/tests/Lazuar.Pay.Tests/WebhookTests.cs
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());
        ...
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        var debit = db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount);
        var credit = db.JournalLines.Where(l => l.Dc == "C").Sum(l => l.Amount);
        Assert.That(debit, Is.EqualTo(credit));
        ...
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
```

That test seeds rail via `PUT …/gateway` with `sk_test_dummy` (never talks to Stripe) and posts a **hand-built** Stripe event JSON with a local HMAC. It proves verify + persist + fulfill + replay against InMemory. It does not prove Stripe API, Postgres TX, or a buyer redirect.

### 9.3 Query door

```9:14:apps/lazuar-pay/src/Lazuar.Pay/Money/PaymentQueryEndpoints.cs
        app.MapGet("/v1/orgs/{orgId}/payments", List);
        app.MapGet("/v1/orgs/{orgId}/receipts", ListReceipts);
        app.MapGet("/v1/orgs/{orgId}/receipts/{id}", Receipt);
```

All member-gated. Receipt JSON uses `number = d.Number ?? "PENDING"` and returns `title`. Cross-org id 404s (`d.Id == id && d.OrgId == orgId`). Missing from pay-spec (see §11). SPA that shows `RCPT-` is the merchant Vite (other report); the **host** door is here.

---

## 10. Tests: what they actually lock

Test project: NUnit, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11, EF InMemory 10.0.0, `ProjectReference` to the host only. IsolationTests also scan the test csproj for cathedral substrings.

| File | What it locks | What it does not |
|------|----------------|------------------|
| `IsolationTests` | host+test csproj banned tokens; src has no MediatR / Modules.One / BuildingBlocks; no `organizations`/`users`/`members` tables; Vite `package.json` has no `@repo/api-types-ts`; no csproj path to `apps/lazuar-api` | Does not scan test `.cs` for `using MediatR`. Does not scan `Lazuar.Pay.slnx` for `lazuar-api` (slnx is two Pay projects; fine today) |
| `HealthTests` | `/health` and `/v1/health` 200 `ok`; health does not call One even if One throws | `/ready`; Postgres |
| `CorsTests` | 5178/5179 ACAO on `/health`; **3003 and 3004** no ACAO | OPTIONS preflight; public `/v1/pay` origin (same default policy, untested on that path) |
| `WhoamiTests` | maps `/me` → `active_org_id`; empty tenants; 401 skips One; One 401; timeout 503; One 500 → 503 | hint header; `lzr_sk_`; 403 map |
| `OrgReadyTests` | allowed true; allowed false 403; One 403; One 500 → 503; 401 skips One; **path org not header** | writer vs member (ready is member) |
| `CheckoutTests` | 401; create+get open MYR; unknown 404 no One; other org create 403; get other org 403; idempotency key; default MYR; amount 0 → 400; health still skips One | public token; paused charges; restart; Postgres unique conflict |
| `CatalogTests` | owner 201; member 403 | list; MYR reject; 401; interval |
| `PublicPayTests` | public GET no extra One calls; missing 404 no One; empty PSP body 400 | `/start`; payer fields; paused; 409 paid |
| `WebhookTests` | missing `Pay:StripeWebhookSecret` → 503 once rail configured; bad sig 400; completed event writes one `RCPT-` + balanced lines; replay `duplicate` and still one document | setup ignored; zero amount; SST null throw; org path mismatch; One HMAC route; Postgres TX |
| `FakeOneHandler` / `PayApiFactory` | replace typed `OneClient`; Testing env; InMemory per factory; default `whsec_test_local` | Does not fake Stripe |

`task pay:test` / CI `pay` job run this set. They do **not** require Zitadel, CHIP, Stripe network, or port 5435. B99.2’s hermetic clause is true for this SHA.

---

## 11. `pay-spec` honesty vs live HTTP

`packages/pay-spec/main.tsp` still opens with:

```7:11:packages/pay-spec/main.tsp
/** Focused Pay HTTP contract. Not packages/api-spec. Checkout is a fixture (open session), not a charge. */
@service(#{ title: "Lazuar Pay" })
@info(#{ version: "0.1.0" })
@server("http://localhost:8081", "Local focused Pay host")
```

`packages/pay-spec/README.md` still says “Grow `main.tsp` when `POST /v1/checkouts` exists” — the spec already has checkouts; that sentence is leftover from 012.

Compiled `dist/openapi.yaml` `paths:` (complete list):

- `POST /v1/checkouts`
- `GET /v1/checkouts/{id}`
- `GET /v1/health`
- `POST /v1/one/webhooks`
- `POST|GET /v1/orgs/{orgId}/products`
- `GET /v1/orgs/{orgId}/ready`
- `GET /v1/pay/{token}`
- `POST /v1/pay/{token}/start`
- `POST /v1/webhooks/{provider}/{orgId}`
- `GET /v1/whoami`

**Live but missing from spec:**

| Live | Spec |
|------|------|
| `GET /health` (unversioned) | omitted (same as 012) |
| `GET /ready` | omitted |
| `PUT/GET /v1/orgs/{orgId}/gateway` | omitted |
| `GET /v1/orgs/{orgId}/payments` | omitted |
| `GET /v1/orgs/{orgId}/receipts` | omitted |
| `GET /v1/orgs/{orgId}/receipts/{id}` | omitted |

**Spec vs live shape drift:**

| Topic | Spec | Live |
|-------|------|------|
| Checkout create status | OpenAPI `200` | **201** |
| Product create status | OpenAPI `200` | **201** |
| Checkout comment | “fixture … not a charge” | durable row + Stripe start + webhook fulfill |
| `CheckoutSession` | no `payer_*`, no `interval` | JSON policy will emit `public_token`, `interval`, `payer_name`, `payer_email` from the C# type |
| `PublicPay` | token, amount, currency, status | also `payer_name`, `payer_email` |
| `Product` | id, org_id, name | create returns `price_id`, `amount`, `currency`, `interval`; list nests `prices[]` |
| `start` body | none in TypeSpec | `StartPayRequest` `{ name, email }` accepted |
| Gateway / receipts / payments | absent | sold merchant doors |

Spec **does** include PSP + One webhook ops (G26 / O14 as TypeSpec rows) and public pay (K20). It does **not** include fulfillment query ops (F23 as pay-spec payments/receipts — 013 F23 intent vs live spec: **not done**).

---

## 12. Tracker cells vs live host (do not edit 011)

011/11 counts on this SHA (file not flipped): S0 5 done / 17 todo; S1 5 done / 37 todo; total 10 done / 81 todo / 24 refuse. The five S0 dones and five S1 dones are still the C99 set. Live Bar B **code** has outrun those Status cells. This section is a recommendation to a later human flip, not a flip.

Legend: **done-in-code (host)** = a later flip of 011 would not be a lie **about this process**. **partial** = a door exists but the 011 sentence is wider than the code. **still todo** = host does not have it (or it is One/SPA and this host correctly does not). **falsely todo** = 011 Status is `todo` while the host already does the job the row names.

### 12.1 Already `done` in 011 — still true

| ID | Live |
|----|------|
| NP-ONE-003 | Whoami forwards Bearer; 401 skips One |
| NP-ONE-006 | `GET /v1/whoami` → `/me` once per request; not middleware; not on `/health` |
| NP-ONE-007 | Path/body org is authz SoT; header is hint (`OrgReadyTests`) |
| NP-ONE-008 | Projection copies One `role`; no Zitadel claim parse |
| NP-ONE-015 | Dummy ready + checkout still `check(member)` |
| NP-CHK-001 | Session amount/currency/tenant; org_id is One tenant |
| NP-CHK-002 | success/cancel stored |
| NP-CHK-003 | `Idempotency-Key` header or body, per org, now in `idempotency_keys` |
| NP-API-001 | `POST /v1/checkouts` |
| NP-API-003 | `GET /v1/checkouts/{id}`; other org 403 |

Do not un-flip these.

### 12.2 Falsely `todo` on the **host** (code exists; 011 still `todo`)

These are the rows this slice is allowed to call out. SPA/OIDC rows are **not** falsely todo just because the host is ready for them.

| ID | Why the cell is behind the code |
|----|----------------------------------|
| **NP-CAT-001** | `POST /v1/orgs/{orgId}/products` writes `name` |
| **NP-CAT-003** | Create rejects non-MYR (`Bar B currency is MYR`); checkout defaults MYR |
| **NP-CHK-006** | `PublicToken` on create; `GET /v1/pay/{token}` is the shareable resource. The URL prefix `:5179/c/` is the Vite app (other report) |
| **NP-CHK-007** (host half) | Public GET/START do not require Bearer and do not call One. Fail lock “checkout requires Zitadel login” is **false on 8081**. (5179 UI is the other half.) |
| **NP-GW-001** | Encrypted BYOK in `gateway_credentials` via `SecretBox` |
| **NP-GW-002** | Stripe Checkout Session `mode=payment` from org key |
| **NP-GW-004** | `EventUtility.ValidateSignature` |
| **NP-GW-005** | Empty body 400, tested |
| **NP-GW-006** | Unique `(org, provider, event_id)`; replay `{duplicate:true}`; test asserts one `RCPT-` |
| **NP-GW-008** | Setup / amount_total 0 ignored in webhook; create amount≤0 400 |
| **NP-FUL-001** | Webhook HTTP → `FulfillPaidAsync` in-process (TX caveats in §9) |
| **NP-API-002** | `POST /v1/webhooks/{provider}/{orgId}` |

Money/docs (not in the user slice letters, but the host implements them and 011 still says `todo`):

| ID | Why |
|----|-----|
| **NP-MON-001** | Two-line balanced journal on first pay (cash/revenue only; no tax/fee lines) |
| **NP-DOC-001** | `RCPT-{year}-{n}` |
| **NP-DOC-002** | Number is not a UUID; query emits `PENDING` if null |
| **NP-DOC-003** | Title `Official Receipt` |
| **NP-FUL-003** (payments half) | `GET /v1/orgs/{orgId}/payments` + receipts. Subscribers list **missing** |
| **NP-DOC-005** (door half) | `GET …/receipts/{id}`. Merchant **UI** is the Vite report |
| **NP-ONE-018** (new charges) | `ChargesPaused` on `tenant.suspended`; create + start 403 |
| **NP-ONE-021** (keys + products) | `RequireWriterAsync`. Member cannot create product (tested). Member cannot PUT keys (code; **untested**). Refund does not exist |

A responsible flip of 011 would still wait for: (a) a human dogfood of B99.1, and/or (b) Postgres TX tests, before calling NP-FUL-001 / NP-GW-006 **production-done**. The **code** is not `todo`. The **lived sentence** is.

### 12.3 Still `todo` honestly (host)

| ID | Why |
|----|-----|
| NP-ONE-001, 002, 004, 005, 009–014, 016, 019, 022 | One product + merchant SPA. Host correctly has no password, no org table, no invite routes |
| NP-ONE-017 | Receiver exists; **subscribe** does not. HMAC untested. `member.*` ignored |
| NP-ONE-020 | `.env.example` is honest about what Pay **may** hold; wrap-key fallback in source is a prod hole; no PAT in tree |
| NP-CAT-002 | Price `Interval` exists; monthly/yearly not required or copied onto checkout |
| NP-CAT-004 | No seats/quantity |
| NP-CAT-005 | Host list/create yes; **edit** no; merchant ops UI is Vite |
| NP-CHK-004 | `open` → `paid` yes; **`expired` never written** |
| NP-CHK-005 | Hosted page is `:5179` (other report). Host has public GET/start |
| NP-GW-003 | No CHIP/Billplz |
| NP-GW-007 | Only Stripe; label `hosted_link` is honest for that rail |
| NP-GW-009 | PUT keys on host; paste UI is Vite; VIEWER/member lock untested on this route |
| NP-FUL-002 | One-off paid row yes; subscription insert unreachable from `POST /v1/checkouts` |
| NP-FUL-004, 005 | Bar C / V1 |
| NP-API-004 | Host is the `/v1` door; “merchant ops is a client” is the SPA |
| NP-API-005 | Member routes isolate by org; webhook `orgId` vs checkout org **not** checked; GET checkout 404 oracle remains |
| NP-API-006 | Checkout idempotency + PSP event id. Not a generic money-POST middleware |

Refuse rows NP-XX-001–024: still `refuse`. Host does not implement password, second org table, Tax Invoice title, Stripe Billing SoT, Zitadel-per-buyer, MediatR, or `lazuar-admin`. Keep them.

### 12.4 011/12 first-slice steps

Steps 1–12 are all `todo` in `12-first-slice-tracker.md`. C99 forbade flipping them for whoami alone. Bar B **code** covers Pay-side steps 8–11’s **host** clauses (keys, product, public token, webhook verify, replay, `RCPT-`, journal) and the writer/member split of step 12’s API. Steps 1–7 are One/SPA. This paper does not mark 011/12 `done`. B99.4 still says flip those steps only if the sentence **ran**.

---

## 13. Gaps / risks / what this slice is NOT

### 13.1 Process seams 013 named that are **still** open

| Gap | Live |
|-----|------|
| Dockerfile / `ASPNETCORE_URLS=http://+:8081` | absent |
| CORS from config; prod fail-boot if empty | still four literals |
| `ValidateOnStart` on `One:BaseUrl` | still bind-only |
| Connection string in appsettings | fallback in `Program.cs` only |
| Structured logs; redact Authorization | MEL console; `OneClient` still logs nothing |
| Rate limit on whoami / public start / webhooks | none |
| Exception handler → `PayErrors` JSON | none |
| `/ready` tests | none |
| Image / compose **service** for the host (not just `pay-db`) | none |
| `pay:test` description | still “health + isolation” |
| Host README in-memory sentence | stale |

### 13.2 Money honesty holes (host)

1. **Webhook event committed before fulfill.** Replay can no-op a failed fulfill.
2. **SST fail-closed is bypassed** by lazy `org_settings` with `SstRegistered = false`.
3. **Journal has no tax/fee lines.** Balanced at 2, not at 4. NP-MON-001’s full sentence is wider than the code.
4. **`Pay:StripeWebhookSecret` is not BYOK.** SK is per org; `whsec` is process-wide.
5. **`Pay:WrapKey` fallback is in source.**
6. **Checkout always `one_off`.** Catalog intervals and `Fulfillment`’s subscription branch do not meet.
7. **No `expired` writer.** NP-CHK-004 is half.
8. **Webhook `orgId` vs checkout org** not checked.
9. **InMemory tests ignore transactions.** Durability and same-TX are not proven on 5435.
10. **`mail_outbox` is a table with no producer.** NP-MAIL-001 still todo.
11. **Gateway PUT writes no audit row.**
12. **One HMAC untested**; hex case / header scheme likely to disagree with One’s real signer until proven.
13. **`AddDataProtection()` unused.** Noise in the composition root.

### 13.3 What this slice is NOT

- **Not the merchant Vite (`:5178`).** OIDC, workspace picker, product UI, keys UI, receipt UI — report 02. This host exposes `/v1` those screens must call.
- **Not the checkout Vite (`:5179`).** `/c/{token}` page states, no-OIDC lock on the SPA — report 03. This host exposes `GET/POST /v1/pay/{token}`.
- **Not a Stripe-vs-old-adapter port.** `StripeHosted` vs Hub `StripeGatewayAdapter` — report 05. This paper only records that a hosted `mode=payment` call exists.
- **Not the five-adapter cathedral.** There is one rail. CHIP/Billplz/Xendit/Razorpay are absent. Adding them without MediatR/factory-of-five is report 09.
- **Not Hub cutover.** Listen is 8081. Hub still binds 8080. P60 (ops/portal on 8081) remains refused.
- **Not Bar C.** Renew, refund-once, magic-link portal, SST × seats, second rail: parked.
- **Not a lived B99.** Hermetic tests green ≠ Ada paid on Stripe.

### 13.4 Isolation that still holds (do not weaken)

The host on this SHA is still Consumer-0:

```4:31:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
    ...
            Assert.That(text, Does.Not.Contain("MediatR"), file);
            Assert.That(text, Does.Not.Contain("Modules.One"), file);
            Assert.That(text, Does.Not.Contain("BuildingBlocks"), file);
```

`Lazuar.Pay.slnx` is two projects. Hub `Lazuar.slnx` is not this host’s solution. Keep it that way when someone “productionizes” logging or a second rail.

---

## 14. Map of live doors (one screen)

```
8081 Lazuar.Pay
 ├─ GET  /health              local liveness
 ├─ GET  /v1/health           local liveness
 ├─ GET  /ready               Postgres CanConnect (never One)
 ├─ GET  /v1/whoami           Bearer → One /me
 ├─ GET  /v1/orgs/{id}/ready  Bearer → One authz/check member
 ├─ POST /v1/checkouts        member; PG checkouts + idempotency
 ├─ GET  /v1/checkouts/{id}   member of session org
 ├─ POST /v1/orgs/{id}/products   writer; products+prices MYR
 ├─ GET  /v1/orgs/{id}/products   member
 ├─ PUT  /v1/orgs/{id}/gateway    writer; AES-GCM stripe sk
 ├─ GET  /v1/orgs/{id}/gateway    member; last4 only
 ├─ GET  /v1/pay/{token}          public; no One
 ├─ POST /v1/pay/{token}/start    public; Stripe Checkout URL
 ├─ POST /v1/webhooks/{provider}/{orgId}  Stripe sig → Fulfillment
 ├─ POST /v1/one/webhooks         HMAC → ChargesPaused
 ├─ GET  /v1/orgs/{id}/payments   member
 └─ GET  /v1/orgs/{id}/receipts[/id]  member; RCPT- / PENDING
```

Postgres `lazuar_pay` on **5435**. One API on **8080**. Merchant SPA **5178**. Checkout SPA **5179**. This process is the money kernel in the middle. It is no longer a dictionary. It is not yet a production image. It is not Hub.
