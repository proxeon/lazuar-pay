# 03 — Making apps/lazuar-pay a production process (seams the fixture host still lacks)

**Date:** 21 August 2026  
**Repo:** `lazuar-pay` (this tree)  
**SHA:** `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` (`6f866ff0`)  
**Branch at writing:** `feat/012-connect-one`  
**Commit subject at SHA:** `feat(pay): scaffold merchant and checkout Vite apps`  
**One sibling SHA:** `0f79fe4f6503847881286ead2e7e57b7c7dc1808` (`0f79fe4`) at `/Users/akmalfirdaus/Code/lazuar/lazuar-one` (branch `main`, subject `WIP: Thu Aug 20 21:24:22 +08 2026`)  
**Host at SHA:** focused C# process at `apps/lazuar-pay`. Whoami, org ready, in-memory checkout fixture. Not a production process.  
**Type:** Seam analysis for making **this host** something you can run as a production process. **Not an implementation.** No code in this paper is to be applied as a patch from this file.  
**Scope of this paper:** the C# host only (`apps/lazuar-pay`). Configuration, database, secrets, CORS, health/ready, logging, deploy, isolation from the cathedral, JSON, auth forwarding, `CheckoutStore` currently in-memory. **Not** `apps/lazuar-api`. **Not** `Lazuar.slnx`. **Not** the Vite shells (papers 04–05). **Not** money-domain rails, BYOK adapters, journal, or `RCPT-` (papers 06–07). **Not** One’s production IdP/HMAC product (paper 08). **Not** Hub data migration (paper 09). **Not** the CI kill-switch mega-plan (paper 10) except where this host’s missing CI job is itself a process seam.

Parent product law: `plans/011-new-lazuar-pay/` — especially [04-linux-shape.md](../011-new-lazuar-pay/04-linux-shape.md) (one tree, one linker, call the function *inside* Pay), [05-language.md](../011-new-lazuar-pay/05-language.md) (C# gravity: write this like a 2008 ASP.NET app), [07-separate-vs-one-binary.md](../011-new-lazuar-pay/07-separate-vs-one-binary.md) and [13-monolith-vs-services.md](../011-new-lazuar-pay/13-monolith-vs-services.md) (one Pay process, one Pay DB), [08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md) (sold `/v1`, HTTP *to* One), [02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) (secrets Pay may hold; secrets Pay must never hold).

Parent host-insertion law: `plans/012-one-to-pay/03-pay-host-seams.md` — that paper inserted One HTTP trust into a nine-line health-only host. This paper is the sequel for **process**: the host now has whoami, org ready, CORS, and an in-memory checkout fixture, and still is not a production process.

Index for this program: [README.md](./README.md). Production-ready bar is paper 01. Cutover is paper 02. This file does not flip checklist cells.

Locked for this slice (do not “clarify” them away):

| Lock | Meaning here |
|------|----------------|
| Host `apps/lazuar-pay` only | Stay off `apps/lazuar-api`. Stay off `Lazuar.slnx`. No MediatR, no `Modules.*`, no BuildingBlocks, no `ProjectReference` into the cathedral |
| **8081 never 8080** | Listen 8081. Dial One on 8080 (local) / One’s HTTPS origin (remote). Never bind 8080 |
| One:BaseUrl is **HTTP** | REST `HttpClient` to One’s `/api/v1`. Not gRPC, not a project reference, not a copied `Modules/One` |
| Caller Bearer forwarded | Pay does not mint a second token for whoami / member checks. No `DefaultRequestHeaders.Authorization` from config on `OneClient` |
| whoami is an **endpoint**, not middleware | `GET /v1/whoami` is the only route that calls One `GET /me`. Health never calls One |
| CORS today allows only `:5178` and `:5179` (and `127.0.0.1`) | Ops `:3003` must stay denied. `CorsTests` is the lock |
| `CheckoutStore` is an in-memory fixture | Say so. Persistence can be sketched. Do **not** invent a 9-schema cathedral |
| One Pay process, one Pay DB when a DB appears | No per-module DbContexts |
| Secrets Pay may hold later | OIDC `client_id` (public), `lzr_sk_` for jobs, One-webhook HMAC, BYOK gateway keys encrypted. **Never** Zitadel PAT / FGA admin / masterkey |

This paper does **not** reopen Go-vs-C#. The focused host that exists is C# `net10.0` + minimal APIs. Production-shape it *here*. Fight the ecosystem for this folder the way 011-05 said you must if you stay on C#.

---

## 0. Verdict (read this before the inventory)

**The focused host is a working laptop fixture, not a production process.** It listens on 8081 when launchSettings applies, forwards the caller’s Bearer to One, maps a snake_case `/v1`, stores checkouts in a process-local `ConcurrentDictionary`, and hard-codes CORS to the two new Vite origins. That is enough for Consumer-0 dogfood on one machine. It is not enough to take a buyer’s money, survive a restart, run two replicas, terminate TLS, or be the thing Caddy health-gates.

**Stay on one host project.** When a database appears it is **one** Postgres database, **one** schema (or `public`), **few tables**, **one** migrator, **one** connection string. Not nine `*DbContext` types. Not `MigrateAllModuleDatabasesAsync`. Not `one` / `commerce` / `billing` / `payments` / `lhdn` / `crm` / `ops` / `messaging` / `communications` copied under new names.

**Keep the test seams that already tell the truth.** `FakeOneHandler` + `PayApiFactory` (replace the typed `OneClient`, do not invent `IOneClient`). `WebApplicationFactory<Program>` for health and CORS so those routes cannot grow a hidden One call. `IsolationTests` string bans. `CorsTests` denying `:3003`. Do not “upgrade” these to NSubstitute, WireMock, Testcontainers-of-Zitadel, or NetArchTest.

**Do not copy the cathedral’s production kit to look finished.** Hub production is Serilog + Azure Key Vault + nine EF migrations at boot + `Jwt:Secret` + `Kms:MasterKey` + `AddLazuarMediatR` + `AddAllModules` + `/health/ready` that asks a metrics collector about outbox lag across schemas. That kit is how a two-month product grew a museum. Pay’s production kit is: listen 8081 behind a gateway, env for `One__BaseUrl` and later one connection string, console (or Serilog-console) logs that never print Bearer, a ready probe that is **Postgres only** (never One), CORS from config that still refuses ops/portal/admin, durable checkout when money is real.

**Dockerfile absence is no longer “correct for whoami.”** 012-03 said no image because the slice was HTTP trust on a laptop. 013’s bar is a process you can run. An image that listens **8081** (never 8080) is in scope to *design* here. Do not publish it as `lazuar-hub-api`. Do not add this host to `docker-bake.hcl`’s Hub matrix as a fifth Hub image.

**C# gravity is still the defect to optimize against.** The failure mode of *this* paper is: “we need production, therefore MediatR + BuildingBlocks.Infrastructure.Observability + nine schemas + JwtBearer + `ICheckoutRepository` + `IUnitOfWork`.” Production is a connection string, a listen URL, a ready probe, and not losing the checkout dictionary on restart. It is not a second cathedral.

---

## 1. Method / SHAs

### 1.1 Binding coordinates

| Field | Value |
|-------|--------|
| Title | Making `apps/lazuar-pay` a production process (seams the fixture host still lacks) |
| Date | 21 August 2026 |
| Pay repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Pay SHA | `6f866ff0489a4de77d2fc1b1bbcfa87fbe72b80f` (`6f866ff0`) |
| Pay branch | `feat/012-connect-one` |
| Pay subject at SHA | `feat(pay): scaffold merchant and checkout Vite apps` |
| One repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` |
| One SHA | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` (`0f79fe4`) |
| One branch | `main` |
| One subject at SHA | `WIP: Thu Aug 20 21:24:22 +08 2026` |
| Host TFM | `net10.0` (`apps/lazuar-pay/global.json` pins SDK `10.0.100`, `rollForward: latestFeature`) |
| Listen (when launchSettings applies) | `http://localhost:8081` |
| Type | Uncondensed seam analysis. **Not** an implementation. **Not** a flip of 011/11 cells |

If this file is read after later commits, treat `6f866ff0` as the **analysis baseline**. Re-inventory `apps/lazuar-pay` before implementing. Do not assume `Program.cs` is still 37 lines.

### 1.2 What was actually opened (this write-up)

**Focused host (source of truth for “what the process is”):**

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` (entire)
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` (entire)
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/One/**` (12 files)
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/**` (4 files)
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/**` (Isolation, Health, Cors, Whoami, OrgReady, Checkout, FakeOneHandler, PayApiFactory, csproj)
- `apps/lazuar-pay/README.md`, `.env.example`, `package.json`, `global.json`, `Lazuar.Pay.slnx`
- `packages/pay-spec/main.tsp`

**Process / deploy contrast (cathedral, to refuse copying):**

- `apps/lazuar-api/src/Lazuar.Api/Program.cs` (Serilog, Key Vault, `MigrateAllModuleDatabasesAsync`, MediatR, modules)
- `apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj` (nine `Modules.*.Infrastructure` ProjectReferences)
- `apps/lazuar-api/src/Lazuar.Api/Composition/DatabaseMigrationExtensions.cs` (nine DbContexts)
- `apps/lazuar-api/src/Lazuar.Api/Composition/HealthEndpointExtensions.cs` (`/health`, `/health/ready`, `/health/metrics`)
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` (`App:CorsOrigins`, `AllowCredentials`)
- `apps/lazuar-api/src/Lazuar.Api/appsettings.json`, `deploy/prod/env.example`
- `apps/lazuar-api/Dockerfile` (`EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`)
- `docker-compose.yml`, `docker-bake.hcl`, `deploy/prod/docker-compose.yml`, `deploy/prod/Caddyfile`, `deploy/dev/Caddyfile`
- `.github/workflows/ci.yml`, `.github/workflows/ghcr.yml`
- `Taskfile.yml` `pay:*` vs `api:*`
- `mprocs-dev.yaml` (Hub frontends only; focused Pay absent)

**Law:**

- `plans/012-one-to-pay/03-pay-host-seams.md` (style parent; whoami insertion; 8081; IsolationTests; no Dockerfile *then*)
- `plans/011-new-lazuar-pay/04-linux-shape.md`, `05-language.md`, `07-separate-vs-one-binary.md`, `08-bezos-door.md`, `13-monolith-vs-services.md`, `02-one-integration.md` (secrets table)
- `plans/012-one-to-pay/08-machine-keys.md`, `09-webhooks-events.md` (what Pay may hold later)
- `plans/013-prods/README.md` (this program’s slice table)

**Not opened as an implementation source:** `Modules/**` handlers, Hub checkout session EF model, Hub JwtService. Those are the museum. This paper names them only to forbid copying.

### 1.3 Method

1. Inventory the focused host as it actually compiles at `6f866ff0` (files, DI, maps, tests, config).
2. Contrast each production seam with Hub’s *existing* production kit in this same monorepo (compose, GHCR, Caddy, nine EF contexts, Serilog) — not with a generic “12-factor” slide.
3. Recommend the **smallest** production shape that 011 already locked: one process, one DB when a DB appears, HTTP to One, 8081, no cathedral types.
4. Leave money-domain table *semantics* (SST, wrap-rails, `RCPT-` format) to papers 06–07. This paper may **name** candidate tables so the host has somewhere to put a connection string. It may not design nine modules.
5. Do not implement. Names in sketches can change; responsibilities should not.

### 1.4 What this paper is allowed to decide vs defer

| Decide here (process) | Defer |
|------------------------|--------|
| One Pay DB, not nine schemas | Exact ledger line columns (paper 07) |
| Candidate table *names* for checkout durability | Stripe vs CHIP adapter internals (paper 06) |
| Env names for One, CORS, connection string | Merchant Vite OIDC (paper 04) |
| Listen 8081 behind a gateway; TLS at Caddy | Checkout SPA flow (paper 05) |
| Health never calls One; ready is local resources | One HMAC catalog completeness (paper 08) |
| Keep FakeOneHandler / WAF / IsolationTests / CorsTests | What Hub rows to migrate (paper 09) |
| Refuse MediatR / BuildingBlocks / JwtBearer-as-SoT | GHCR kill of hub-api (paper 10) |

---

## 2. What the host actually is today (endpoints, DI, JSON snake_case, singleton store)

### 2.1 Tree (source of truth: `apps/lazuar-pay/`, excluding `bin/` / `obj/`)

```
apps/lazuar-pay/
  .env.example                         One__BaseUrl, One__TimeoutSeconds only
  global.json                          SDK 10.0.100
  Lazuar.Pay.slnx                      two projects
  package.json                         pnpm name lazuar-pay
  README.md                            8081, do not copy Modules/One, checkout is in-memory
  src/Lazuar.Pay/
    Lazuar.Pay.csproj                  Sdk.Web, zero PackageReference
    Program.cs                         composition root
    appsettings.json                   Logging, AllowedHosts, One
    appsettings.Development.json       Logging only
    Properties/launchSettings.json     http://localhost:8081
    One/                               typed HttpClient + doors that call One
    Checkouts/                         in-memory fixture
  tests/Lazuar.Pay.Tests/
    Lazuar.Pay.Tests.csproj            NUnit + Mvc.Testing
    IsolationTests.cs
    HealthTests.cs
    CorsTests.cs
    WhoamiTests.cs
    OrgReadyTests.cs
    CheckoutTests.cs
    FakeOneHandler.cs
    PayApiFactory.cs
```

**Still absent (process-relevant):**

| Absent | At `6f866ff0` |
|--------|----------------|
| `Dockerfile` / `.dockerignore` | none under `apps/lazuar-pay/` |
| `appsettings.Production.json` | none |
| `UserSecretsId` | none (Hub has `lazuar-api-dev-secrets`) |
| `Directory.Build.props` / `Directory.Packages.props` under Pay | none. MSBuild walks *up*; Hub’s props live in `apps/lazuar-api/` and do **not** apply sideways. This is still true and still required |
| Connection string / Npgsql / EF | none |
| `/ready` or `/health/ready` | none |
| Serilog / OpenTelemetry packages | none on the focused host |
| `AddAuthentication` / JwtBearer | none |
| `AddRateLimiter` | none |
| `UseHttpsRedirection` / HSTS / forwarded headers | none |
| `UserSecrets` / Azure Key Vault | none |
| Second class library | none |
| `ProjectReference` to anything except the test → host link | none |

`packages/pay-spec/` is a sibling package, not inside the host. Server URL `http://localhost:8081`. Documents health, whoami, org ready, checkout fixture. Not a C# project reference.

### 2.2 `Program.cs` (entire host composition root)

37 lines of statements plus `public partial class Program;`. Quoted in full because this file *is* the process:

```csharp
using System.Text.Json;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.One;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
// Test seam: ConfigureTestServices re-registers OneClient with a fake HttpMessageHandler.
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddSingleton<CheckoutStore>();
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
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();

app.Run();

public partial class Program;
```

Read this as the production composition root it will remain. Everything this paper adds later (connection string, ready probe, CORS from config, a real store) hangs **here**, in the open, not inside `AddPayModules()`.

Implications that are already true and must stay true:

- **JSON for `/v1` is snake_case globally** via `ConfigureHttpJsonOptions`. Health’s anonymous `{ status = "ok" }` still serializes as `{"status":"ok"}` because the name is already lowercase. Whoami, org ready, checkout, and `PayErrors` (`status`, `title`, `detail`) ride the same policy. `OneClient.Json` duplicates the same policy for upstream One DTOs and for `Results.Json(..., OneClient.Json)`. Two option objects, one convention. Do not introduce camelCase for “C# property honesty.”
- **DI before `Build`:** `OneOptions` bind (no `ValidateOnStart`, no DataAnnotations), typed `AddHttpClient<OneClient>()` with **no** factory `BaseAddress` (the constructor writes `HttpClient.BaseAddress` from options), singleton `CheckoutStore`, CORS policy.
- **Pipeline after `Build`:** `UseCors()` only. No `UseAuthentication`. No `UseAuthorization`. No `UseExceptionHandler`. No `UseHttpsRedirection`. No `UseRateLimiter`. No custom One-calling middleware. Whoami remains an **endpoint**. Member checks are **function calls** from checkout/org-ready handlers (`MemberGate.RequireMemberAsync`). That is the Linux shape (011-04) applied to identity: call the function, do not install a hot loop.
- **`app.Run()` has no URL argument.** Listen URL is not in `Program.cs`. See §2.8 / §6.
- **`public partial class Program;` stays.** `WebApplicationFactory<Program>` and `InternalsVisibleTo` depend on it.

The comment on `AddHttpClient<OneClient>()` is the documented test seam. `PayApiFactory` honours it by **removing** the typed client and re-registering with `new HttpClient(FakeOneHandler)`. Keep that comment if the registration changes.

### 2.3 Host csproj (entire)

```xml
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

</Project>
```

Facts:

- **Zero `PackageReference`.** Restore graph (`src/Lazuar.Pay/obj/project.assets.json`) is `"targets": { "net10.0": {} }`, `"libraries": {}`, `"projectFileDependencyGroups": { "net10.0": [] }`. Framework reference is `Microsoft.AspNetCore.App`. `IHttpClientFactory`, `IOptions<T>`, `System.Text.Json`, CORS, logging — production bits this host already uses — are in the shared framework. **A production process does not start by adding MediatR.**
- **`TreatWarningsAsErrors` is on.** Unused usings, nullable holes, and dead parameters fail the build. Do not leave `#pragma warning disable` as a substitute for deleting the line.
- **`InternalsVisibleTo` names `Lazuar.Pay.Tests` only.** Keep it. Do not add the Hub test assemblies.
- **No `ProjectReference`.** IsolationTests fails the csproj if `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, or `Lazuar.Api` appear as substrings.
- **No `UserSecretsId`.** Hub’s csproj has `lazuar-api-dev-secrets` and a comment about `Jwt:Secret`. Do not copy that id. Do not add Key Vault “for later.”
- **No `Directory.Build.props` under Pay.** Hub’s `apps/lazuar-api/Directory.Build.props` sets `ManagePackageVersionsCentrally` and TFM. Directory.Build.* walks **up**, never into a sibling. Adding a `Directory.Build.props` at repo root that both hosts inherit would be a contamination event. Do not.

Hub’s host csproj, for contrast (do not copy): nine `ProjectReference`s to `Modules.{One,Messaging,CRM,Payments,Ops,Billing,Lhdn,Commerce,Communications}.Infrastructure`, plus Azure Key Vault, Serilog, EF Design. That is the cathedral’s composition root expressed as MSBuild. Pay’s composition root is the 37-line `Program.cs` and an empty item group.

### 2.4 Every HTTP endpoint the process maps

There are **six** application maps plus CORS preflight that `UseCors()` will answer. There is no `MapControllers`. There is no `/api` prefix (Hub serves `/api/v1/...`; Pay’s sold door is `/v1/...` on 8081). TypeSpec documents five of the six (it omits unversioned `/health`).

| Method | Path | Auth | Calls One? | Body / result | Persistence |
|--------|------|------|------------|---------------|-------------|
| `GET` | `/health` | none | **never** | `{"status":"ok"}` 200 | none |
| `GET` | `/v1/health` | none | **never** | `{"status":"ok"}` 200 | none |
| `GET` | `/v1/whoami` | Bearer required (401 if missing/empty/non-Bearer); **Pay does not validate the JWT** | `GET {One:BaseUrl}/me` with the **same** `Authorization` and optional `X-Lazuar-Tenant-Id` | snake_case `WhoamiResponse`; errors `{status,title,detail}` | none |
| `GET` | `/v1/orgs/{orgId}/ready` | Bearer + `MemberGate` (`authz/check` relation `member`, object `tenant` / `{orgId}`) | `POST {One:BaseUrl}/tenants/{orgId}/authz/check` | `{"org_id":"...","ready":true}` if allowed | none (dummy admin; “ready” is membership, not a charge capability) |
| `POST` | `/v1/checkouts` | Bearer + `MemberGate` on `body.org_id` | same `authz/check` as ready | 201 `CheckoutSession` (`status: open`); 400 if `amount` ≤ 0 | **in-memory `CheckoutStore`** |
| `GET` | `/v1/checkouts/{id}` | If missing: **404 without calling One**. If present: Bearer + `MemberGate` on **session.org_id** (not the path) | `authz/check` only after a hit | 200 session or 403 | **in-memory** |

**Not mapped (do not invent on a “production” PR unless a later paper owns them):**

| Missing door | Who owns it |
|--------------|-------------|
| `POST /v1/auth/login`, `/one/auth/*`, cookie `SignIn` | Never. Sign-in is One `:5175` |
| `GET /v1/me` alias of whoami | Never. One’s path is `/me`; Pay’s sold name is `/whoami` |
| `GET /ready`, `/health/ready`, `/health/live`, `/health/metrics` | This paper’s gap (§3.3) |
| Buyer-public `GET /v1/checkouts/{id}` without merchant Bearer | Paper 05 (hosted pay). Today GET is **merchant member**. A buyer on `:5179` cannot load the fixture without a staff token — CORS allows 5179 but `MemberGate` will 401/403 |
| PSP webhook `POST /v1/webhooks/...` | Paper 06 |
| One HMAC receiver | Paper 08 |
| `POST /v1/checkouts/{id}/complete` / capture | Papers 06–07 |
| Metrics scrape `/metrics` (Prometheus) | Not in this repo’s focused host; Hub uses `/health/metrics` JSON, not Prometheus |

**CORS:** `UseCors()` applies the default policy to every map, including health. `AllowAnyHeader` + `AllowAnyMethod`, **no** `AllowCredentials` (correct for Bearer; do not add cookies). Origins are four literals (see §2.7). `CorsTests` lock 5178/5179 allow and **3003 deny**. OPTIONS preflight is not asserted (gap, not a new origin).

**Whoami mapping (sold JSON, not One’s document verbatim):**

| One `/me` field | Pay `/v1/whoami` field |
|-----------------|-------------------------|
| `user_id` | `user_id` |
| `email` | `email` |
| `is_platform_admin` | `is_platform_admin` |
| `active_tenant_id` | **`active_org_id`** (Pay noun) |
| `tenants[].id/slug/name/role/status` | same |
| `active_role` | **dropped** (present on `OneMeResponse`, not copied by `OneMeMapper`) |
| `name` | **dropped** (not on `OneMeResponse` at all in this host) |
| `tenants[].permissions` | **dropped** (not on the DTO) |

Whoami status mapping (`WhoamiEndpoints.Map`):

| Upstream / transport | Pay |
|----------------------|-----|
| 200 + mappable body | 200 `WhoamiResponse` |
| missing Bearer | 401, **SendCount 0** |
| One 401 | 401 |
| One 403 | 403 |
| timeout / `HttpRequestException` | 503 `Identity provider unreachable` |
| One 500, 404, 429, garbage JSON, missing `user_id` | **503** `Identity provider failed` (parse miss is 503 from `OneClient`, not 502) |

012-03 recommended 502 for “One answered badly” vs 503 for “unreachable.” The implementation collapsed both to **503**. Tests lock 503 (`Whoami_maps_one_500_to_503`, `Whoami_maps_one_timeout_to_503`). A production process *may* split 502/503 later; do not flip it casually — the tests are the contract. Either way, **do not return 200 with a partial lie**, and **do not put One’s URL in the problem body.**

Org-ready and checkout use `MemberGate`, which is **not middleware**:

- No Bearer → 401, no One call.
- Empty `orgId` → 400.
- One 200 `allowed: true` → proceed.
- One 200 `allowed: false` or One 403 → 403 `Not a member of this org`.
- Transport fail → 503.
- Other One statuses → 503.

`authz/check` body is `{ relation: "member", object: { type: "tenant", id: orgId } }`. OrgReadyTests lock: path org, not `X-Lazuar-Tenant-Id`; body does not contain `user_id`. VIEWER is not a One tenant role (`owner` / `admin` / `member`); README already says ready is membership, not “cannot charge.” Do not “fix” ready by inventing VIEWER in Pay.

### 2.5 DI inventory (every registration)

| Registration | Lifetime | Notes |
|--------------|----------|--------|
| `IOptions<OneOptions>` | options | `BindConfiguration("One")`. Defaults in the class: `BaseUrl = http://localhost:8080/api/v1`, `TimeoutSeconds = 5`. **No** `[Required]`, **no** `ValidateOnStart`, **no** URI scheme check |
| `OneClient` | typed HttpClient (transient client, handler pooled) | Constructor applies BaseAddress + Timeout from options. **Public** class (012 wanted internal; tests construct it) |
| `CheckoutStore` | **singleton** | `ConcurrentDictionary` of sessions + idempotency. Dies with the process. Not a ledger |
| CORS default policy | singleton policy | four localhost origins, any header, any method |
| `ConfigureHttpJsonOptions` | options | snake_case + case-insensitive |

**Not registered:** `IHttpContextAccessor`, `IMemoryCache`, `IEventBus`, `IJwtService`, `ISecretVault`, `IPasswordService`, MediatR, any `DbContext`, any hosted service, any authentication scheme, exception handler, problem-details service (beyond ad hoc `PayErrors`).

Hub’s `Program.cs` registers all of those plus `MigrateAllModuleDatabasesAsync` before mapping. That is the contamination catalog (§8).

### 2.6 `OneClient` and forwarding (the trust model that must survive production)

`OneClient` is a typed `HttpClient` wrapper with two methods: `GetWhoamiAsync` and `CheckMemberAsync`. Both:

1. Build a relative URL (`me` or `tenants/{orgId}/authz/check`) against `BaseAddress` that the constructor forces to `BaseUrl.TrimEnd('/') + "/"`.
2. `TryAddWithoutValidation("Authorization", authorization)` — **verbatim caller header**, not a parsed `AuthenticationHeaderValue` (so odd `lzr_sk_` shapes do not throw).
3. Optionally forward `X-Lazuar-Tenant-Id` if the incoming hint is non-empty. **Never authorize from that header.** Org-ready and checkout pass the **path or body org id** into `authz/check`’s object id. OrgReadyTests lock path-org vs header-org.
4. Do **not** set `DefaultRequestHeaders.Authorization` from config. There is no `lzr_sk_` in `OneOptions`. There must not be.
5. Catch `TaskCanceledException` → `TimedOut`; `HttpRequestException` → `TransportFailed`. No Polly. No retry. `/me` can write (JIT join); retrying it is a write amplifier. `authz/check` is a POST; retrying it without idempotency is sloppy. Production does not add retries “for reliability.”
6. **Does not log.** There is no `ILogger<OneClient>` constructor argument. That is a production gap (§3.5) and also a safety: it cannot log the Bearer today because it never logs. When logging is added, the rule is: never log `authorization`, never log the raw token, `user_id` on success is fine.

`OneOptions` is two properties. Production does not grow `ZitadelPat`, `FgaStoreId`, `ApiKey`, `Retries`, `EnableWhoami` into this class. OIDC `client_id` when it appears (paper 04) is a **different section** (`PayOidc` / `Authentication`), not a field on `OneOptions`. `lzr_sk_` for jobs when it appears (paper 08) is a **different env** (`Pay__OneApiKey` or `ONE_API_KEY`), injected only into the worker that needs Pay’s machine identity — never into whoami.

`Bearer.TryGet` requires scheme `Bearer ` (case-insensitive) and a non-whitespace token. Missing/empty/non-Bearer → 401 **without** calling One. Tests lock this on whoami, ready, and checkout create.

### 2.7 CORS as actually compiled

Hard-coded in `Program.cs`, not in appsettings, not in env:

```
http://localhost:5178
http://127.0.0.1:5178
http://localhost:5179
http://127.0.0.1:5179
```

That is merchant Vite and checkout Vite (papers 04–05). **Not** Hub ops `:3003`, portal `:3004`, admin `:3005` / One Login V2 collision, `lazuar-admin` `:5173`, One login `:5175`, One app `:5174`.

`CorsTests`:

| Origin | Expectation |
|--------|-------------|
| `http://localhost:5178` on `GET /health` | 200 + `Access-Control-Allow-Origin: http://localhost:5178` |
| `http://localhost:5179` on `GET /health` | 200 + ACAO for 5179 |
| `http://localhost:3003` on `GET /health` | 200 **and no** `Access-Control-Allow-Origin` |

The 3003 test is a **product lock**, not a style preference. Pointing old ops at 8081 is how someone stubs `/one/auth/login` on Pay. CORS denying 3003 does not stop a non-browser client, but it stops the accidental SPA retarget. **Keep the test. Never add 3003/3004/3005/5173 to the allowlist, including in production config.** Production will replace localhost with HTTPS merchant/checkout origins — still not Hub UIs.

Gaps vs Hub CORS (do not copy Hub’s `AllowCredentials` or AllowAnyOrigin-in-dev):

- Hub reads `App:CorsOrigins` and **fails boot** in Production/Staging if empty (`EnsureCorsOriginsConfigured`). Pay has no such env, no Production check, and will ship localhost origins into a container if you forget.
- Hub `AllowCredentials()` because Hub cookies exist. Pay must **not** grow cookies. Bearer from the SPA does not need credentials mode.
- Pay does not list `https://` origins, `localhost` without a port, or the future public hostnames.

### 2.8 Config files as they sit on disk

**`appsettings.json`:**

```json
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

**`appsettings.Development.json`:** logging only (Default Information, AspNetCore Warning). Inherits One.

**`Properties/launchSettings.json`:** one profile `http`, `applicationUrl: http://localhost:8081`, `ASPNETCORE_ENVIRONMENT=Development`, `launchBrowser: false`. **This is still the only 8081 pin in source.** `Program.cs` does not call `UseUrls`. There is no `Kestrel` / `Urls` section in appsettings. There is no `ASPNETCORE_URLS` in any file under `apps/lazuar-pay`.

**`.env.example`:**

```
# One HTTP façade (no PAT, no OpenFGA admin, no lzr_sk_ in C-phases).
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5
```

**`.env.example` is not loaded by the process.** Hub’s `Program.cs` reads a hand-rolled `../../../../.env` parser. Pay does not. ASP.NET Core binds `One__BaseUrl` from **process environment** and from appsettings. A developer who copies `.env.example` to `.env` and does not `export` or use a dotenv loader still gets the committed appsettings default (same values, luckily). Production must use real env / compose `environment:`, not a committed `.env` in the image.

**`OneOptions` has no `ValidateOnStart`.** An empty `One__BaseUrl` in production becomes `http://localhost:8080/api/v1` via the class default when the property is missing, or an empty string if someone sets `One__BaseUrl=` — constructor then does `"".TrimEnd('/') + "/"` and `new Uri("/")` throws at **first One call**, not at boot. Health still 200s. That is a production seam: fail boot on bad BaseUrl, or health stays green while whoami 500s on first merchant.

### 2.9 `CheckoutStore` — say this out loud

```csharp
/// <summary>In-memory fixture store. Not a ledger. Replace when money is real.</summary>
public sealed class CheckoutStore
{
    readonly ConcurrentDictionary<string, CheckoutSession> _byId = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.Ordinal);
    // Create / Get ...
}
```

Registered `AddSingleton<CheckoutStore>()`. Facts the comment already admits and production must not forget:

| Fact | Consequence |
|------|-------------|
| Process-local memory | `dotnet watch` restart, container recycle, OOM kill, deploy → **all open checkouts vanish** |
| One singleton per process | Two replicas = two stores. Idempotency key `orgId + "\n" + key` does not cluster |
| `Guid.NewGuid().ToString("N")` ids | Not `ch_` prefixed, not ULID, not DB-assigned. Fine for a fixture; collisions are not the problem — **loss** is |
| Status is always `"open"` | There is no pay, expire, void, or webhook transition in this host. Papers 06–07 own transitions. This paper owns **durability of the row** |
| Idempotency is best-effort in memory | Second POST with the same key returns the first session **in this process**. After restart, the same key creates a second session. Tests lock in-process idempotency (`Create_idempotent_on_key`) |
| GET 404 before auth | Unknown id 404s without calling One (CheckoutTests). Known id then MemberGate. That is an existence oracle. Acceptable for a fixture; a public buyer GET later must not leak other orgs’ ids via timing+404 vs 401 |
| GET is merchant-gated | Hosted checkout SPA (`:5179`) cannot load a session as a buyer. CORS allows 5179; `MemberGate` does not. Paper 05 must add a **buyer** read (tokenized path or signed cookie-less token), not “log the buyer into Zitadel” |

README already says: “Checkout is an in-memory fixture (`status: open`). Not a real charge. Buyer has no One account.” Production-ready money **cannot** ship with this store. Production-ready **process** can ship health/whoami without it — but the map is already public, so a restart in staging will strand any SPA that held an id.

This paper may sketch a `checkouts` table (§4). It may not pretend the dictionary is a ledger.

### 2.10 JSON policy (one convention)

| Mechanism | Where | Policy |
|-----------|--------|--------|
| `ConfigureHttpJsonOptions` | `Program.cs` | `SnakeCaseLower` + case-insensitive |
| `OneClient.Json` static | `OneClient` | same |
| `[JsonPropertyName]` | **not used** | names come from the naming policy (`UserId` → `user_id`, `ActiveOrgId` → `active_org_id`) |
| `PayErrors` | anonymous `{ status, title, detail }` | already lowercase identifiers |
| Health | anonymous `{ status }` | `{"status":"ok"}` |

Pay `/v1` is snake_case, same as One’s public JSON. Do not add Newtonsoft. Do not add a second policy for “internal” vs “sold.” Hub also set `SnakeCaseLower` in its `Program.cs`; copying **that one line** was correct. Copying Hub’s Newtonsoft package (in Hub `Directory.Packages.props`) is not.

### 2.11 Listen path vs outbound One (repeat until it sticks)

| Direction | URL | Who |
|-----------|-----|-----|
| **Listen** | `http://localhost:8081` (launchSettings only) | This process |
| **Dial One** | `http://localhost:8080/api/v1` default | Sibling identity product |
| Hub API (must be **off** for live whoami) | also wants **8080** | Cathedral. README: leave `task dev` / compose `lazuar-api` off |
| ASP.NET container default | often **8080** | Why a future Dockerfile must set `ASPNETCORE_URLS=http://+:8081` |
| OpenFGA gRPC in One compose | host 8091 → container 8081 | Must not steal **host** 8081 from Pay |

`One:BaseUrl` pointing at 8080 is **not** stealing 8080. Binding Kestrel to 8080 is.

### 2.12 Solution, tasks, workspace

**`Lazuar.Pay.slnx`:** two projects (host + tests). This is the only solution this host belongs in. Grep of Hub `Lazuar.slnx` for `Lazuar.Pay` is empty. Keep it empty.

**`package.json` name `lazuar-pay`:** turbo `build` / `test` / `lint` / `check-types` / `dev` scripts call `dotnet` on `Lazuar.Pay.slnx`. Root `pnpm test` is `turbo run test --filter=!@examples/*`, which **would** build and test this package if someone ran it. **GitHub `ci.yml` does not.** `ci.yml` `dotnet` job `working-directory: apps/lazuar-api` and tests Hub assemblies only. `contracts` job is Hub TypeSpec honesty. **CI at `6f866ff0` can go green while `Lazuar.Pay.slnx` is red.**

**Taskfile `pay:*`:** `pay:restore`, `pay:build`, `pay:dev` (8081), `pay:spec`, `pay:merchant`, `pay:checkout`. `pay:test` description still says “health + isolation” — it actually runs whoami, CORS, org ready, checkout too. Update the description when someone is in the file; behavior is already `dotnet test Lazuar.Pay.slnx`.

**`mprocs-dev.yaml`:** Hub frontends + optional Caddy. Focused Pay is **not** a proc. Correct. Do not add it to Hub mprocs; `task pay:dev` is the way.

**Compose / bake / GHCR:** Hub images only (`lazuar-hub-api|ops|portal|superadmin|developers`). Focused Pay is not an image. `docker-compose.yml` publishes Hub API `8080:8080`. Caddy prod `handle /health` and `handle /api/*` → `api:8080`. Dev Caddy `:9080` same map to `host.docker.internal:8080`. **Pay 8081 is invisible to every current gateway.**

### 2.13 TypeSpec vs process (honesty, not a frontend)

`packages/pay-spec/main.tsp` server `http://localhost:8081`. Documents `/v1/health`, `/v1/whoami`, `/v1/orgs/{orgId}/ready`, `POST/GET /v1/checkouts`. Comment: “Checkout is a fixture (open session), not a charge.” README of pay-spec still says “Grow `main.tsp` when `POST /v1/checkouts` exists” — the spec already has it; the README sentence is stale.

Unversioned `GET /health` exists in `Program.cs` and is what Hub-style healthchecks usually hit. TypeSpec does not list it. Production Docker `HEALTHCHECK` should hit `/health` or `/v1/health` (both local). Do not invent a third path.

### 2.14 What “production” cannot mean for this host today (preview of §3)

The process at `6f866ff0` **can**: answer liveness without One; forward a Bearer; refuse ops origin; create an open checkout in RAM; compile with zero host NuGets; stay out of `Lazuar.slnx`.

The process **cannot**: remember a checkout across a deploy; tell Kubernetes it is ready for money (it has nothing to be ready *for* except “the process started”); bind a stable port in a container; restrict CORS to a real HTTPS origin; fail boot on missing One URL; emit JSON logs with request id; limit `POST /v1/checkouts` abuse; terminate TLS; validate that the Bearer is even a JWT; run two replicas of checkout.

That is the gap list, not a dare to import Hub’s `Program.cs`.

---

## 3. Gaps vs a production process

Each gap is a **process** seam. Money-domain gaps (no Stripe, no journal, no `RCPT-`) are real and owned by 06–07; they are listed here only when they leak into process (in-memory store, no durable idempotency).

### 3.1 No Postgres

There is no `ConnectionStrings` section, no Npgsql package, no `DbContext`, no `NpgsqlDataSource`. Hub has three connection-string **names** (`Default`, `TenantConnection`, `MessagingConnection`) pointed at the **same** database with different pool sizes — and then **nine** schemas inside it. Pay must not copy the three names or the nine schemas. When a database appears it is one name, e.g. `ConnectionStrings:Pay` / `ConnectionStrings:Default`, one database, one schema.

Without Postgres:

- Checkout durability is a dictionary.
- Ready probe has nothing to probe but the process itself (so `/health` == `/ready` today, which is a lie the moment you add a DB and forget a ready endpoint).
- Multi-instance is split-brain on idempotency.

Do not add SQLite “for production simplicity.” The repo’s production database is Postgres 16 (compose `postgres:16-alpine`, Hub Neon). Pay’s production database is Postgres. Tests may stay in-memory until a store exists; then WAF tests either use Testcontainers (Hub already references `Testcontainers.PostgreSql` in **Hub** `Directory.Packages.props` — do not inherit that file; if Pay tests need a container, reference the package from **Pay’s test csproj only**) or a fake store that implements the same interface as the PG store. Prefer: keep `CheckoutStore` as a class with a later PG-backed twin, or replace the class. **Do not** introduce `ICheckoutRepository` + `CheckoutRepository` + `CheckoutRepositoryAdapter` + MediatR. A class and a table are enough.

### 3.2 No migrations

Hub applies EF `MigrateAsync` for nine contexts at boot (`DatabaseMigrationExtensions`). Taskfile `api:db:migrate` is nine `dotnet ef database update --context *DbContext` lines. That is the tax 011-04 named.

Pay has no migrations folder. When tables appear, **one** migrator, **one** history table, **one** folder. Boot-time migrate on a **single** context is acceptable for a single replica; Hub’s own comment already warns multi-instance races. Prefer: migrate in an init command / CI job / `task pay:db:migrate` before the process starts accepting checkout POSTs, rather than copying `MigrateAllModuleDatabasesAsync`.

See §10 for which migrator. See §4 for which tables. The gap is: **there is no story at all today.**

### 3.3 No durable checkout

Already stated in §2.9. Production consequences in one place:

| Event | In-memory fixture | Production need |
|-------|-------------------|-----------------|
| Process restart | all sessions gone | row still `open` until expiry |
| Second replica | other replica 404s the id | shared DB |
| Idempotency-Key retry after deploy | second charge session | same row |
| Buyer pays after recycle | webhook has nowhere to attach | row + attempt log |
| Legal/audit “what was presented” | vanished | row + amount/currency/urls |

Papers 06–07 will add status transitions and journal lines. They will fail if the host still stores checkout in RAM. Replacing `CheckoutStore` is a **host** change even though money semantics are not this paper’s.

Do not “fix” durability with Redis as a second production dependency unless Pay already runs Redis (it does not). Postgres is the store. Redis is C# gravity wearing an ops hat.

### 3.4 No structured logging / metrics

Focused host uses generic host **console** logging from appsettings (`Default Information`, `Microsoft.AspNetCore Warning`). No Serilog. No OpenTelemetry packages (grep of `apps/lazuar-pay` is empty; grep of Hub packages for OpenTelemetry is empty too). Hub uses **Serilog.AspNetCore + Serilog.Sinks.Console** and a homegrown `PlatformMetricsCollector` hosted service that scrapes outbox lag, dead letters, LHDN stuck counts, exposed as `GET /health/metrics`.

What Pay must **not** copy: `BuildingBlocks.Infrastructure.Observability`, `LazuarMetricsGauges`, `IOutboxSchemaRegistration`, `/health/metrics` fields named `lhdn_stuck_count`. Those metrics are the cathedral’s organs.

What Pay is missing for a production process:

| Gap | Why it hurts |
|-----|----------------|
| No request id / correlation id in logs | Cannot join Pay 503 with One 500 |
| `OneClient` logs nothing | Cannot see upstream status without a debugger |
| No JSON console (structured) | VPS grep vs a log pipeline |
| No metrics | Cannot alert “whoami 5xx rate” or “checkout create latency” |
| Console format is MEL default | Fine for laptop; thin for prod |

When logging is added: **never log `Authorization`, never log `lzr_sk_`, never log BYOK secret material, never log One HMAC.** Log `user_id` after whoami maps, `org_id` on checkout create, One status code, elapsed ms. Hub Serilog config is console-only in committed appsettings — they did not even add Seq/App Insights in the host csproj (test host pulls `Microsoft.ApplicationInsights` transitively from VSTest, not as a product choice). If Pay adds a package, Serilog.AspNetCore is what this monorepo already knows. OpenTelemetry is **not** in the repo; do not invent a full OTel collector graph to look like 2024 CNCF.

Metrics: ASP.NET built-in `EventCounters` / `Meter` exist in the shared framework. A later `/metrics` (Prometheus) or a later cloud agent can scrape. Do not add App Insights SDK because the test `bin/` folder happens to contain `Microsoft.ApplicationInsights.dll`.

### 3.5 No real ready probe vs liveness

Hub:

| Path | Meaning |
|------|---------|
| `GET /health` | liveness, `{status:ok}`, no DB, what Caddy and Docker HEALTHCHECK hit |
| `GET /health/ready` | DB reachable + optional outbox lag threshold; 503 if not |
| `GET /health/metrics` | JSON snapshot of cathedral gauges |

Pay:

| Path | Meaning |
|------|---------|
| `GET /health` | `{status:ok}`, no One, no DB |
| `GET /v1/health` | identical |

There is no ready. Docker/K8s that treat `/health` as readiness will send traffic the moment Kestrel accepts a socket, including during a future migration, including when Postgres is down (today: Postgres is not used, so the lie is invisible).

**Rules for when probes grow:**

1. **Liveness** (`/health` and/or `/v1/health`) stays **local**. Never One. Never Postgres if a stuck connection pool could hang liveness and kill a healthy replica that only needed a recycle of *connections*. Prefer liveness = “the process can allocate a byte and return JSON.”
2. **Readiness** (new `/health/ready` or `/ready`) checks **Pay’s** Postgres when that exists. **Never One.** If One is down, whoami/org-ready/checkout-create should 503; hosted checkout **pay** (buyer, no One account) and health must not. 011-07: money stays true if membership lags; a ready probe that requires One inverts that.
3. Do not copy Hub’s outbox-lag ready gate until Pay has an outbox. Pay should not have an outbox to talk to itself.
4. Keep `HealthTests.Health_does_not_call_one` (already exists). Add `Ready_does_not_call_one` when ready exists.

Hub Docker HEALTHCHECK: `curl -fsS http://127.0.0.1:8080/health` with `start-period=90s` because nine EF migrations run at boot. Pay should not need a 90s start-period if it does not migrate nine schemas at boot. If Pay migrates one schema at boot, a short start-period is enough; better: migrate before the container is ready (init command).

### 3.6 CORS hardcoded to laptop origins

See §2.7. Production hostnames, HTTPS, and extra origins (preview deploys) cannot be added without a code change. Hub at least reads `App:CorsOrigins` and **fails boot** in Production/Staging if empty (`EnsureCorsOriginsConfigured`). Steal **that rule** (fail boot if prod CORS is empty), not Hub’s origin list (`3000–3005, 8080, 8090, 9080`) and not `AllowCredentials`.

Config shape (sketch, not applied):

```json
"Cors": {
  "Origins": [
    "http://localhost:5178",
    "http://127.0.0.1:5178",
    "http://localhost:5179",
    "http://127.0.0.1:5179"
  ]
}
```

Env: `Cors__Origins__0=https://pay.example.com` etc. Staging/Production: `ValidateOnStart` that the list is non-empty and contains no `localhost:3003`. IsolationTests or CorsTests should keep asserting 3003 is denied **even if** someone adds it to appsettings — a unit test that the policy builder rejects 3003 is stronger than hoping config is right. Today CorsTests hard-code the running policy; after config, tests should `UseSetting` an origins list that includes 3003 and still assert no ACAO, **or** a dedicated test that the default Development settings still deny 3003.

Never `AllowAnyOrigin` in Development as Hub does when parse fails. Pay’s Development list is explicit.

### 3.7 No auth of Pay’s own JWT — it trusts One by forwarding

This is **by design** for Consumer-0 and remains the production trust model for merchant routes:

1. Caller sends `Authorization: Bearer …` (Zitadel access_token or `lzr_sk_`).
2. Pay does not parse `urn:zitadel:iam:org:project:roles`.
3. Pay forwards to One `/me` or `/tenants/{id}/authz/check`.
4. One is the authority.

Production gaps that are **not** “therefore add JwtBearer as source of truth”:

| Gap | Why it exists | What production may add later |
|-----|----------------|-------------------------------|
| Obviously garbage tokens still leave Pay for One | No local signature check | Optional JwtBearer **signature** against Zitadel JWKS so random strings 401 at the edge. Membership still `/me` + `authz/check`. **Not** role claims SoT |
| Pay has no resource-server audience check | One’s token may be for another API | When Pay has an OIDC `client_id` / API audience in One, validate `aud`. Paper 04/08 |
| `lzr_sk_` and user JWT share the same header | One distinguishes them | Pay must keep forwarding; do not regex `lzr_sk_` and skip One |
| No Pay-issued JWT | Correct | Hub `Jwt:Secret` / `IJwtService` is a **second IdP**. Never copy |
| Buyer has no token | Correct | Buyer doors must not use `MemberGate`. Do not mint a Zitadel user for a cardholder |

**Do not** add `AddLazuarAuthentication` from Hub. That stack is cookies + homemade JWT + API keys in `one.ApiCredentials`. New Pay’s keys are One’s `lzr_sk_`.

Trust-by-forwarding is production-legal **if** One is the IdP and Pay is Consumer-0. It is not unfinished auth. Unfinished is: no rate limit on whoami (token stuffing against One through Pay), no TLS, no ready, no durable checkout.

### 3.8 No rate limit

Grep of `apps/lazuar-pay` for `AddRateLimiter` / `UseRateLimiter` is empty. Hub does **not** use the ASP.NET middleware either; it has bespoke `TokenBucketRateLimiter` classes in One/Commerce (`PublicAuthRateLimiter`, `PublicRegisterRateLimiter`, `PortalMagicLinkRateLimiter`, `IntegratorProvisionRateLimiter`). Do not copy those classes. If Pay needs a limit, use shared-framework `AddRateLimiter` on public surfaces:

| Surface | Why |
|---------|-----|
| `GET /v1/whoami` | Forwards to `/me`, which can write (JIT). A loop is a write amplifier on One |
| `POST /v1/checkouts` | Idempotency helps retries; it does not stop a flood of new keys |
| Future buyer GET / pay | Card-testing / enumeration |
| Future PSP webhook | Provider retries are fine; random internet POST is not |

Global 1000 rps on `/health` is unnecessary. Do not rate-limit liveness. Do not copy Hub’s per-email magic-link buckets into this host “because production.”

### 3.9 No TLS story in the process

The process speaks **HTTP**. launchSettings is `http://localhost:8081`. There is no `https` profile, no Kestrel cert, no `UseHttpsRedirection`, no HSTS, no `ForwardedHeaders` for `X-Forwarded-Proto`.

Hub production does **not** terminate TLS in Kestrel either. `deploy/prod/docker-compose.yml`: only Caddy publishes 80/443; `api` has `ASPNETCORE_URLS: http://+:8080` and `expose: 8080`. `deploy/prod/Caddyfile` reverse_proxies `/health` and `/api/*` to `api:8080`. That is the story Pay should copy **as a shape**: **process HTTP on 8081 inside the network, gateway TLS outside.**

What Pay must not copy: Caddy `handle /api/* → api:8080` without a new handle for Pay. A later gateway should `reverse_proxy pay:8081` on Pay’s public host, not steal Hub’s `/api` path on `hub.lazuar.com`. New Pay is not Hub’s `/api/v1` under a new container name.

Until a gateway exists, laptop dogfood is HTTP localhost. That is acceptable. Production on a public NIC without TLS is not. The process does not need to become an HTTPS server to be production-shaped; it needs to sit behind one.

`ForwardedHeaders` (`X-Forwarded-For` / `X-Forwarded-Proto`) becomes required when a gateway appears, otherwise scheme/host for `success_url` construction and logs will be wrong. Not today (no gateway points at 8081). Do not add `KnownProxies = Any` on a laptop.

### 3.10 Other process gaps (shorter, still real)

| Gap | Evidence | Production need |
|-----|----------|-----------------|
| No `ValidateOnStart` on `OneOptions` | bind only | Boot fail if `BaseUrl` empty or not absolute http(s) |
| No listen pin except launchSettings | §2.8 | `ASPNETCORE_URLS=http://+:8081` in image and/or `Urls` in appsettings |
| `.env.example` not loaded | Program.cs has no dotenv parser | Good — do not copy Hub’s hand-rolled `.env` reader. Use env / compose |
| No `appsettings.Production.json` | absent | Optional; env can override. If added, **no secrets** in git |
| No exception handler | unhandled → default 500 HTML/text | `UseExceptionHandler` + `PayErrors` JSON; do not leak stack traces; do not leak One URL |
| Whoami 5xx collapsed to 503 | tests lock it | Acceptable; optional later 502 vs 503. Do not 200 |
| `OneClient` is `public` | tests construct it | Fine. Do not add `IOneClient` |
| No request size cap on One responses | `ReadFromJsonAsync` unbounded | 012-03 asked for a cap (e.g. 256 KiB). Still missing |
| No request logging redaction | no logging of headers | When logging exists, redact `Authorization` |
| GET checkout 404 vs 401 oracle | 404 if missing before auth | Acceptable for merchant GET; buyer GET later must not |
| Merchant-only GET checkout | `MemberGate` | Paper 05 must add a buyer read; do not “fix” by logging buyers into One |
| No `UserSecretsId` | csproj | Fine. Do not add Hub’s id |
| `AllowedHosts: *` | appsettings | Fine behind a gateway; can tighten later |
| IsolationTests does not scan `Lazuar.Pay.slnx` | test file | Widen: slnx must not name `lazuar-api` |
| IsolationTests does not scan test `*.cs` for MediatR | only `src/` | A test that `using MediatR` would pass. Widen if cheap |
| `pay:test` Taskfile description stale | “health + isolation” | Cosmetic |
| CI does not run Pay tests | `ci.yml` `working-directory: apps/lazuar-api` | Paper 10 owns the job; this paper records the gap: **the production process is untested in CI** |
| GHCR / bake / compose ignore Pay | Hub images only | §6 |
| No Dockerfile | none | §6 — must listen **8081** |
| Options defaults mask empty env | `BaseUrl` default localhost:8080 | Prod must set `One__BaseUrl` to the real One origin |
| `OneClient` constructor mutates `HttpClient` | BaseAddress set in ctor | Works; tests that forget to replace the client will dial localhost:8080. WAF health tests that use raw `WebApplicationFactory` do not call One. Whoami tests use `PayApiFactory` |

### 3.11 Auth forwarding vs “Pay’s own JWT” (again, because someone will add JwtBearer)

Hub production `deploy/prod/env.example` still has:

```
Jwt__Secret=change-me-jwt-secret-minimum-32-characters
Jwt__Issuer=lazuar-api
Jwt__Audience=lazuar-clients
Kms__MasterKey=change-me-kms-master-key-minimum-32-characters
```

Those secrets exist because Hub **is** an IdP (homemade JWT + AES vault for tenant PSP keys). New Pay is not an IdP. Production Pay env must **not** grow `Jwt__Secret`. BYOK encryption needs a **Pay data key** (paper 06), named as such (`Pay__DataKey` / envelope via cloud KMS), never `Kms__MasterKey` copied from Hub, never “fall back to Jwt__Secret if unset” (Hub’s env.example comment).

Forwarding is the product. Local JWT validation, if added, is a **filter** on obviously bad tokens, not a second membership list.

---

### 3.12 Gaps vs a production process (checklist form)

The narrative is §3.1–3.11. This table is the punch list a later implementer can tick without rereading Hub.

| # | Gap | Today | Production bar (this host) |
|---|-----|-------|----------------------------|
| G1 | Postgres | none | One database, one connection string |
| G2 | Migrations | none | One folder, one history table, not nine `*DbContext` |
| G3 | Durable checkout | `ConcurrentDictionary` singleton | Table (or replace store). Idempotency survives restart |
| G4 | Structured logs | MEL console | JSON console or Serilog console; **redact Authorization** |
| G5 | Metrics | none | Optional later; do not copy `lhdn_stuck_count` |
| G6 | Ready vs live | both `/health` are live | Ready = Pay DB (when it exists). **Never One** |
| G7 | CORS | four localhost literals | Config list; prod fail-boot if empty; **never 3003** |
| G8 | Pay-issued JWT | none (correct) | Stay none. Optional JWKS **signature** later |
| G9 | Rate limit | none | Shared-framework limiter on whoami/checkout/public buyer; not Hub token-bucket classes |
| G10 | TLS | HTTP process | Gateway (Caddy) terminates; process HTTP **8081** inside the net |
| G11 | Listen pin | launchSettings only | Image `ASPNETCORE_URLS=http://+:8081` |
| G12 | Options validation | none | `ValidateOnStart` on `One:BaseUrl` |
| G13 | Exception handler | none | JSON `PayErrors`, no stack to client |
| G14 | Dockerfile | none | New file, **8081**, not Hub’s 8080 HEALTHCHECK |
| G15 | CI job | Hub only | `dotnet test Lazuar.Pay.slnx` (paper 10 owns the YAML; this host needs it) |
| G16 | Secrets surface | `.env.example` Two One keys | See §5. Never PAT / FGA / masterkey |
| G17 | Multi-instance | one laptop process | DB-backed store before replicas |
| G18 | Buyer checkout GET | merchant `MemberGate` | Paper 05; do not Zitadel the buyer |

G3/G1 are the same wall: without a DB, checkout cannot be production money. Whoami **can** be production-shaped without a DB (it is a proxy). A process that already maps `POST /v1/checkouts` cannot call itself production-money without G3. It can call itself production-identity-proxy once G6 (as “live only”), G7 (config), G10–G12, G14, G16 are honest.

---

## 4. Recommended persistence shape (one DB, few tables, no module schemas)

### 4.1 Law (repeat until the PR that adds `CommerceDbContext` is rejected)

1. **One Pay process** (already true: `Lazuar.Pay.csproj`).
2. **One Pay database** when a database appears. Not Neon-for-commerce plus Neon-for-billing. Not `TenantConnection` vs `MessagingConnection` vs `Default` pointing at the same cluster as a costume.
3. **One schema** (`public` or a single `pay` schema). Not `HasDefaultSchema("commerce")` plus eight friends.
4. **One migrator.** Not `MigrateAllModuleDatabasesAsync` looping nine contexts.
5. **One writer per fact.** Membership is One’s. Money is Pay’s. Do not create `members` / `organizations` / `users` tables that duplicate `/me`.
6. **Call the function** for Pay talking to Pay (011-04). Journal insert is in the same handler/transaction as “mark checkout paid” when that exists (paper 07). Not `CheckoutPaidIntegrationEvent`.
7. **Bezos is the door** (011-08): strangers and the merchant SPA use `/v1`. They do not `SELECT` Pay tables. One is reached with HTTP, not a linked `Modules.One.Infrastructure`.

Hub’s nine boot contexts, for the refuse list (from `DatabaseMigrationExtensions.cs`):

```
OneDbContext, MessagingDbContext, PaymentsDbContext, CrmDbContext,
OpsDbContext, BillingDbContext, LhdnDbContext, CommerceDbContext,
CommunicationsDbContext
```

Schemas those contexts pin: `one`, `messaging`, `payments`, `crm`, `ops`, `billing`, `lhdn`, `commerce`, `communications`. **Do not recreate these names** inside Pay, even as “just folders.” A Pay table `checkouts` in `public` is enough. A Pay schema `commerce` plus `billing` is the museum teleporting.

### 4.2 What “no second org table” means (and what it does not)

011 / 012: One tenant id **is** Pay `org_id`. Pay must not mint org ids. Pay must not store Ada’s password, Ada’s memberships, or a `role` column that can disagree with `authz/check`.

Allowed: a **thin** `org_settings` (or `orgs`) row **keyed by One tenant id**, holding Pay-only settings (default currency, statement descriptor, whether charges are paused because `tenant.suspended` arrived). That is not a membership table. It is a foreign key to a person you cannot JOIN because they live in another process.

Forbidden: `users`, `memberships`, `organizations.slug` uniqueness as Pay’s SoT, `ApiCredentials`, `DeveloperApiKeys`, `InviteTokens`. Those are One’s (or Hub museum).

`GET /v1/whoami` remains the chrome snapshot. Do not cache `/me` into a `users` table “for performance” in the first persistence PR.

### 4.3 Candidate tables (names only — not nine modules)

This is a **host persistence sketch** so the connection string has somewhere to point. Column-level money design is papers 06–07. Do not turn each bullet into a project.

| Table (candidate) | Why the **process** needs it | Not |
|-------------------|------------------------------|-----|
| `checkouts` | Replace `CheckoutStore._byId`. Id, `org_id`, amount, currency, status, urls, timestamps | Not a ledger. Not Hub `commerce.CheckoutSessions` copied row-for-row |
| `idempotency_keys` | Replace `_idempotency` so `Idempotency-Key` survives restart and replicas. Keyed `(org_id, key)` → `checkout_id` (or a generic `(org_id, route, key)` later) | Not an outbox |
| `org_settings` | Pay-only settings keyed by One tenant id | Not memberships |
| `gateway_credentials` | BYOK Stripe/CHIP secrets **encrypted** (paper 06). Exists as a name so you do not put `sk_live_` in `org_settings` plaintext | Not One `lzr_sk_` |
| `psp_webhook_events` | Provider event idempotency `(org_id, provider, event_id)` (paper 06) | Not One HMAC deliveries |
| `one_webhook_events` | One → Pay HMAC deliveries, different secret, different table (paper 08) | Not PSP |
| `journal_entries` / `journal_lines` | Paper 07. Named so checkout paid and receipt share a transaction | Not `billing.LedgerEntries` copied |
| `documents` | `RCPT-` numbers, **not** tax invoices (paper 07) | Not LHDN VALID |
| `products` / `prices` | Catalog when S1 is real | Not nine product types |
| `subscriptions` | Wrap-rails later; not Stripe Billing SoT | Not Hub dunning engine copy |
| `audit_events` | Same process, same DB, append in the money transaction (011-07) | Not an audit **service** |
| `mail_outbox` | Receipt mail later, still Pay DB until Notify extracts | Not `communications` schema |

**Minimum to retire `CheckoutStore`:** `checkouts` + `idempotency_keys` (or idempotency columns unique on `checkouts`). Everything else can wait for the paper that owns the noun. Do not create `outbox_messages` + `inbox_messages` per bounded context because Hub has them on every module.

**Indexes (sketch, not a migration):** `checkouts(org_id, created_at)`; unique `idempotency_keys(org_id, key)`; unique `checkouts(id)`. Do not unique-on-slug an org table you should not have.

**Do not:**

- `HasDefaultSchema("commerce")` because the table is named checkout.
- `CheckoutDbContext` + `BillingDbContext`.
- `ICheckoutRepository` in an Application project.
- Outbox to tell yourself the checkout was created.

### 4.4 How the host should talk to the table (C# gravity)

011-05: *C# is fine if you write it like a 2008 ASP.NET app: one project, one schema, SQL or a single EF context, handlers as functions, no event bus.*

Allowed shapes when `checkouts` appears:

| Shape | OK? | Condition |
|-------|-----|-----------|
| `NpgsqlDataSource` + SQL in `CheckoutStore` (rename) | Yes | One project, SQL in one folder, parameters, transactions |
| **One** `PayDbContext` with `DbSet<CheckoutRow>` | Yes | **One** migrations folder; no `HasDefaultSchema` per module; no second context “for billing” |
| Dapper on the same connection | Yes | Hub already uses Dapper in places; do not import Hub’s stores |
| Nine contexts / MediatR / `IRepository<T>` | **No** | IsolationTests should keep failing MediatR |

Do not add `Microsoft.EntityFrameworkCore.Design` to the host “because Hub has it” until a single context exists. Do not add `Npgsql.EntityFrameworkCore.PostgreSQL` at the same time as MediatR.

`CheckoutStore` today is a concrete class. The production replacement can stay a concrete class used by `CheckoutEndpoints`. Tests that need isolation already use WAF + in-process store; when PG exists, either Testcontainers in **Pay’s test csproj** or a test double of the store **class** (subclass or replace in DI) — same pattern as `PayApiFactory` replacing `OneClient`. Do not invent `ICheckoutStore` until a second implementation exists. The second implementation **is** the replacement, not a parallel fake forever.

### 4.5 Transactions (preview for 06–07, constraint for the host)

When paid + journal + `RCPT-` exist, they must share **one** connection and **one** transaction inside this process. That constraint is why there is one DB. A host that opens `CheckoutDbContext` and `BillingDbContext` cannot keep that promise without `TransactionScope` tricks across schemas — the tax. Persistence shape here is what makes paper 07 possible.

Mail/audit: tables in the same DB; `mail_outbox` insert in the same transaction as the receipt if you need “paid iff we queued mail.” Extract Notify only when a second product shares a sending domain (011-07).

### 4.6 What “replace CheckoutStore” looks like without designing money

Sketch (not applied):

1. Table `checkouts` with the fields `CheckoutSession` already has (`id`, `org_id`, `amount`, `currency`, `status`, `success_url`, `cancel_url`, `created_at`) plus `updated_at`.
2. Unique `(org_id, idempotency_key)` where key is null-able (only rows that had a key).
3. `CheckoutStore.Create` becomes INSERT … ON CONFLICT return existing.
4. `Get` becomes SELECT by id.
5. DI: still one class, registered **singleton** if it only holds `NpgsqlDataSource` (the source is singleton-safe); **not** a dictionary.
6. Tests: existing CheckoutTests stay green against Testcontainers **or** keep an in-memory implementation **only in tests** if you refuse containers in unit CI. Prefer one implementation (PG) and one test job with Postgres, like Hub’s `LAZUAR_TEST_PG` — but **one** database `lazuar_pay_test`, not `lazuar_mvp` with nine schemas.

Do not port Hub `commerce.CheckoutSessions` columns (`GatewayName`, `AdHocLineItems`, `MetadataJson`, …) “so migration is easier.” Paper 09 owns Hub data. This host’s table is new.

---

## 5. Config / env matrix (local / staging / prod)

### 5.1 What exists today vs what production must grow

| Key | Today | Local | Staging | Prod |
|-----|-------|-------|---------|------|
| `One__BaseUrl` | appsettings `http://localhost:8080/api/v1`; `.env.example` same | Same. One API **must** occupy 8080; Hub API off | `https://<one-staging>/api/v1` (HTTPS to One; the *protocol to One is HTTP REST*, not “plain http://”) | `https://<one-prod>/api/v1` |
| `One__TimeoutSeconds` | 5 | 5 | 5–10 | 5–10 |
| `ASPNETCORE_ENVIRONMENT` | launchSettings `Development` | Development | Staging | Production |
| `ASPNETCORE_URLS` | unset (launchSettings 8081) | `http://localhost:8081` | `http://+:8081` | `http://+:8081` |
| `Cors__Origins` | **not a key** (literals in Program.cs) | 5178 + 5179 (+ 127.0.0.1) | HTTPS merchant + checkout origins | same, real hostnames |
| `ConnectionStrings__Pay` (name can be `Default`) | **absent** | local compose Postgres when it exists | Neon/PG staging **one** DB | Neon/PG prod **one** DB |
| `AllowedHosts` | `*` | `*` | gateway hostnames optional | optional tighten |

Locked meaning of “One:BaseUrl HTTP”: Pay talks to One with **HTTP REST** (`HttpClient`), not gRPC, not a project reference. Staging/prod BaseUrl should be **https://** to One. Do not ship `http://localhost:8080/api/v1` in a production container.

### 5.2 Secrets Pay may hold **later** (and how they bind)

From 011-02 and 012-08/09, plus this program’s lock. None of these exist on the focused host today except the public One URL.

| Secret / value | Env (candidate) | When | Notes |
|----------------|-----------------|------|--------|
| One BaseUrl | `One__BaseUrl` | now | Not a secret; mis-set to Hub 8080 is a dogfood bug |
| One timeout | `One__TimeoutSeconds` | now | int |
| Pay OIDC `client_id` | `Pay__Oidc__ClientId` (public) | paper 04 | **Public.** May live in merchant Vite too. Not a PAT |
| Pay OIDC `client_secret` | **none** if public SPA PKCE | paper 04 | Public client. Do not invent a confidential secret for the Vite app |
| Pay machine `lzr_sk_` | `Pay__OneApiKey` or `ONE_API_KEY` | jobs / HMAC registration later | Bound to **one** One tenant. Not for whoami. Never `DefaultRequestHeaders` on the typed client used by whoami |
| One → Pay webhook HMAC (`whsec_…`) | `Pay__OneWebhookSecret` | paper 08 | Shown once by One. Pay stores it. One stores AES-wrapped copy. **Pay never holds One’s `Webhooks:SigningSecretEncryptionKey`** |
| BYOK Stripe/CHIP secret keys | encrypted in `gateway_credentials`; envelope key `Pay__DataKey` or cloud KMS | paper 06 | Not `Authorization` to One. Not Hub `Kms__MasterKey` |
| Postgres | `ConnectionStrings__Pay` | when DB appears | Not three names |

### 5.3 Secrets Pay must **never** hold (scan env.example / Key Vault)

| Forbidden | Who actually holds it | If it appears in Pay |
|-----------|----------------------|----------------------|
| Zitadel **masterkey** / first-instance | One ops | Stop. You have rebuilt One |
| `ZITADEL_PAT` Management | One seed / provisioner | Isolation-level incident |
| OpenFGA store admin / model writer | One ops | Same |
| Login-client PAT | `lazuar-login` only | Same |
| One webhook **AES / pepper** (One’s wrapping key) | One API config | Pay holds `whsec_`, not the wrap key |
| Hub `Jwt__Secret` / `Jwt__Issuer=lazuar-api` | Hub IdP | Second token vocabulary |
| Hub `Kms__MasterKey` | Hub AES vault | Different product |
| `PLATFORM_ADMIN_PASSWORD` | Hub/One seed | Not Pay |
| `INTEGRATOR_PROVISION_SECRET` | Hub | Not Pay |
| OpenAI / OpenRouter keys | Hub AI | Not this host |
| R2 keys | Hub object storage | Not until Pay actually uploads |

`.env.example` today is already honest (Two One HTTP settings, comment “no PAT, no OpenFGA admin, no lzr_sk_ in C-phases”). When `lzr_sk_` appears, the comment must change to “jobs only, not whoami,” not vanish.

### 5.4 Local / staging / prod matrix (filled)

| Variable | Local laptop | Staging | Production |
|----------|--------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Staging | Production |
| `ASPNETCORE_URLS` | `http://localhost:8081` (or launchSettings) | `http://+:8081` | `http://+:8081` |
| `One__BaseUrl` | `http://localhost:8080/api/v1` | `https://one.<staging>/api/v1` | `https://one.<prod>/api/v1` |
| `One__TimeoutSeconds` | `5` | `5` | `5` |
| CORS | 5178/5179 HTTP localhost | HTTPS preview origins + still **no 3003** | HTTPS merchant + checkout origins, **no Hub UIs** |
| `ConnectionStrings__Pay` | unset until DB; then `Host=localhost;Port=5432;Database=lazuar_pay;…` | one DB | one DB |
| `Pay__Oidc__ClientId` | unset until paper 04 | set | set (public) |
| `Pay__OneApiKey` | unset until a job exists | set if jobs | set if jobs |
| `Pay__OneWebhookSecret` | unset until HMAC receiver | set | set |
| `Pay__DataKey` | unset until BYOK | set | set or cloud KMS |
| Azure Key Vault | **off** | optional later | optional later — do not copy Hub’s boot `try/catch` that prints Key Vault failure and continues |

Hub prod `deploy/prod/env.example` is a **negative example**: Jwt, Kms, three connection names, `App__CorsOrigins=https://hub.lazuar.com`, `App__OpsUrl`, `App__ClientUrl` portal, Billplz, Resend, OpenAI. Pay’s prod env should be **short**. If Pay’s prod env.example grows to Hub length, it has grown Hub nouns.

### 5.5 CORS env vs Hub `App:CorsOrigins`

Hub: comma-separated string, `AllowCredentials`, fail boot in Production/Staging if empty, Development may AllowAnyOrigin if parse fails.

Pay: **list** of origins (configuration array), **no credentials**, fail boot in Production/Staging if empty, Development uses the four localhost origins, **policy builder still refuses 3003** (test). Do not name the section `App:CorsOrigins` — that is Hub’s section and invites copy-paste of Hub’s origin list. `Cors:Origins` is enough.

Steal Hub’s **fail-boot-if-empty-in-prod** function as an idea. Do not copy `AuthAndCorsExtensions.cs`.

### 5.6 One BaseUrl validation (should exist before prod)

`ValidateOnStart` sketch (not applied):

- Non-empty
- Absolute URI
- Scheme `http` or `https`
- Path contains `api/v1` (optional but saves the classic `GET /me` 404)
- Host:port must **not** equal Pay’s listen URL (loop)

Empty string in prod must not silently become localhost:8080 via class defaults **after** bind. `BindConfiguration` then default-property is subtle: missing key → class default; empty env `One__BaseUrl=` → empty string. Validate both.

### 5.7 `.env.example` vs Hub `.env` loader

Keep Pay’s `.env.example` as **documentation**. Do not add Hub’s `File.ReadAllLines("../../../../.env")` parser to `Program.cs`. That parser is how secrets appear in a file next to a process that also has Azure Key Vault in the same method. Production: compose `environment` / `env_file` (gitignored `.env` on the server). Laptop: export, or direnv, or launchSettings `environmentVariables` (do not put secrets in launchSettings committed).

When a DB exists, add `ConnectionStrings__Pay=` to `.env.example` empty. When OIDC exists, add the public client id. Never add PAT placeholders “so ops remembers.”

---

## 6. Deploy: process vs container vs compose swap from `lazuar-api`

### 6.1 Three run shapes (only one is how 8081 works today)

| Shape | How | Listen | Used today? |
|-------|-----|--------|-------------|
| **Process** | `task pay:dev` / `dotnet watch run --project src/Lazuar.Pay/Lazuar.Pay.csproj` | launchSettings **8081** | **Yes.** README dogfood |
| **`dotnet Lazuar.Pay.dll`** | published binary | Kestrel default — **often 8080 in containers**, 5000 historically | **Untested.** No pin |
| **Container** | none | Hub image defaults **8080** | **No Pay image** |

012-03 said absence of Dockerfile was correct for whoami. 013 says a production process needs a defined listen for the second and third shapes. Pin:

1. Keep launchSettings **8081**.
2. Set `ASPNETCORE_URLS=http://+:8081` in any Dockerfile / compose / systemd unit.
3. Optional `"Urls": "http://localhost:8081"` in appsettings for `dotnet Lazuar.Pay.dll` on a laptop without launchSettings. WAF does not bind a real port; HealthTests stay green.

**Never** `ASPNETCORE_URLS=http://+:8080`. **Never** `EXPOSE 8080`. **Never** compose `8080:8081` mistyped as `8080:8080`.

### 6.2 Hub container contract (negative example to steal shape from, not ports)

`apps/lazuar-api/Dockerfile`:

- SDK 10.0 build → aspnet 10.0 runtime
- `EXPOSE 8080`
- `ENV ASPNETCORE_URLS=http://+:8080`
- `ENV ASPNETCORE_ENVIRONMENT=Production`
- `HEALTHCHECK` curl `http://127.0.0.1:8080/health` start-period **90s**
- `ENTRYPOINT ["dotnet", "Lazuar.Api.dll"]`
- Copies `Directory.Build.props`, entire Modules graph, `packages/api-types-dotnet`

Pay Dockerfile (sketch, not applied) should:

- Context: enough to restore **only** `apps/lazuar-pay/**` (two csprojs). **Do not** `COPY apps/lazuar-api`.
- `EXPOSE 8081`
- `ENV ASPNETCORE_URLS=http://+:8081`
- HEALTHCHECK curl `http://127.0.0.1:8081/health` (liveness). Start-period **short** until boot migrations exist; if migrate-at-boot, still not 90s for nine schemas
- `ENTRYPOINT ["dotnet", "Lazuar.Pay.dll"]`
- `USER` non-root (Hub switches to `app` after apt curl). Fine to copy **that**
- **Not** named `lazuar-hub-api`
- **Not** a bake target in the Hub matrix until paper 10 says so — or a **new** bake group `pay`

`.dockerignore` when the file exists: `**/bin`, `**/obj`, Hub apps, `node_modules`. Do not dockerignore `apps/lazuar-pay/src`.

### 6.3 Compose swap from `lazuar-api` (when, and what it is not)

Root `docker-compose.yml` today: `db` (Postgres 16, `lazuar_mvp`, host 5432) + `api` build `apps/lazuar-api/Dockerfile` **8080:8080** + profile `full` frontends 3003/3004/3005/3002.

README of focused Pay: “Compose still points at `apps/lazuar-api`. Swap later when S1 dogfood is real. Do not set ops/portal `VITE_API_URL` to 8081.”

**Swap meaning (this paper):** a compose **service** for focused Pay that publishes **8081:8081**, env `One__BaseUrl` pointing at One (not at Hub), later `ConnectionStrings__Pay` pointing at **a Pay database** (not `lazuar_mvp` with nine schemas). It does **not** mean:

- Retarget `lazuar-ops` / `lazuar-portal` at 8081 (papers 01/02/04/05 refuse this).
- Drop Hub `api` the same afternoon (paper 02 cutover).
- Reuse service name `api` so Caddy `api:8080` silently becomes Pay and every Hub path 404s.
- Map `8080:8081` and tell people “Pay is on 8080 now.” One needs 8080 locally; Hub prod Caddy uses `api:8080` internally. Pay’s **container-local** port is 8081 so a future compose network can run One (8080) and Pay (8081) **without** a clash even inside Docker (One’s container may also EXPOSE 8080; Pay must not).

Sketch (not applied):

```yaml
pay:
  build:
    context: .
    dockerfile: apps/lazuar-pay/Dockerfile
  image: ghcr.io/<org>/lazuar-pay:local
  ports:
    - "8081:8081"
  environment:
    ASPNETCORE_URLS: http://+:8081
    One__BaseUrl: http://one:8080/api/v1   # or host.docker.internal
    # ConnectionStrings__Pay: ... later
  # depends_on: db only when Pay has a DB
```

Do not `depends_on: api` (Hub). Do not join Pay to Hub’s `lazuar-network` as a way to share Hub’s database.

**Postgres:** Hub compose `db` is `lazuar_mvp`. Pay may **reuse the engine** on a laptop (same Postgres 16, **different database name** `lazuar_pay`) or a second compose service. Do not run Pay migrations against `lazuar_mvp` schemas `commerce`/`billing`. Paper 09 may read Hub tables; the new process must not attach its EF to them.

### 6.4 Gateway later (Caddy)

**Today:** no Caddy handle points at 8081. Prod Caddy `/health` and `/api/*` → Hub `api:8080`. Dev Caddy `:9080` same.

**Later:** a **Pay** hostname (not `hub.lazuar.com/api`) reverse_proxies to `pay:8081`. TLS at Caddy. Process stays HTTP. Forwarded headers then.

Do not put Pay under `handle /api/*` on the Hub hostname as a “swap.” That is how merchants hitting Hub ops get Pay 404s and someone stubs `/one/auth/login`.

Do not publish Pay 8081 on 80/443 without a gateway.

### 6.5 GHCR / bake / ci.yml (honesty)

| Surface | Hub | Pay at `6f866ff0` |
|---------|-----|-------------------|
| `.github/workflows/ci.yml` `dotnet` job | restore/build/test `Lazuar.slnx` + Postgres `lazuar_mvp` | **not run** |
| `contracts` job | Hub TypeSpec honesty | pay-spec not in the dirty-check list |
| `ghcr.yml` matrix | 5 Hub images | **not included** |
| `docker-bake.hcl` | Hub targets | **not included** |
| turbo `pnpm test` | would include `lazuar-pay` **if** someone ran it in CI | CI does not run turbo test |

Paper 10 owns adding a CI job. This paper’s process claim: **a production host whose tests never run on the PR is not production-shaped.** The job should be `dotnet test Lazuar.Pay.slnx` **without** Hub Postgres and **without** live One. When Pay gains a DB, a **Pay** Postgres service (`lazuar_pay`, not `lazuar_mvp`) may appear. Do not attach Pay tests to Hub’s `LAZUAR_TEST_PG`.

GHCR image name, when it exists: something like `lazuar-pay` / `lazuar-pay-api`, **not** `lazuar-hub-api`. Do not push a health-only image as if Hub cut over.

### 6.6 systemd / raw process (allowed)

A production process does not **require** a container. `dotnet Lazuar.Pay.dll` behind Caddy with `ASPNETCORE_URLS=http://127.0.0.1:8081` is valid. The pin is still 8081, still HTTP locally, still env for One BaseUrl. Container is how this monorepo deploys Hub; matching that ops shape is convenient, not a religion.

### 6.7 Dual-run with Hub (laptop and prod)

| Place | Hub API | One API | Pay |
|-------|---------|---------|-----|
| Laptop dogfood One+Pay | **Off** (port war) | **8080** | **8081** |
| Laptop Hub work | **8080** | off or remapped | 8081 if launchSettings; cannot live-whoami One |
| Prod Hub VPS today | internal 8080 | not this VPS’s job | **not deployed** |
| Future Pay prod | Hub may still run until paper 02 kill | One’s prod origin | internal **8081** |

Pay’s listen port never becomes 8080 to “simplify Caddy.” Simplifying Caddy is a new `handle` / new hostname.

### 6.8 What “swap compose” is not

- Not retargeting `VITE_API_URL` of ops/portal.
- Not adding Pay to `mprocs-dev.yaml` next to `lazuar-ops`.
- Not `docker compose --profile full` growing a Pay frontend on 3003.
- Not publishing OpenFGA on host 8081 (One maps gRPC 8091→container 8081; host 8081 stays Pay).

---

## 7. Test seams to keep

The host is small enough that the tests **are** the architecture. Production work that weakens them to “move faster” is how Hub grew NetArchTest allowlists.

### 7.1 `FakeOneHandler` + `PayApiFactory` (keep)

`FakeOneHandler`: subclass `HttpMessageHandler`, record `SendCount` / `LastRequest` / `LastBody`, `ThrowOnSend`, `Delay`, `Responder`. No WireMock, no HttpListener, no NSubstitute.

`PayApiFactory`: `WebApplicationFactory<Program>` that **removes** `OneClient` registrations and `AddTransient` a new `OneClient(new HttpClient(One, disposeHandler: false) { BaseAddress = http://one.test/api/v1/ })`. Comment in Program.cs already describes this.

012-03 preferred `Configure<HttpClientFactoryOptions>(nameof(OneClient))` so tests share the production registration path. The implementation used **remove-and-replace**. Both are valid **if** they still exercise header forwarding on a real `HttpClient` send. **Invalid:** `IOneClient` mock that returns a DTO without proving `Authorization` was copied.

Keep `disposeHandler: false` — the factory owns the handler across requests.

Health tests that must prove **no** One call can use `PayApiFactory` + `ThrowOnSend` (`Health_does_not_call_one`, `CheckoutTests.Health_still_skips_one`). Keep both.

**Do not** switch Whoami/OrgReady/Checkout tests to raw `WebApplicationFactory` without a fake — they would dial `localhost:8080` (appsettings default).

### 7.2 Raw `WebApplicationFactory<Program>` (keep for CORS + simple health)

`CorsTests` and the original Health success tests use **unmodified** WAF. That is correct: CORS is the real policy, not a test `UseSetting`. When CORS moves to config, CorsTests should still boot the **default** Development policy and deny 3003, plus a test with `UseSetting` for a production-like origin list.

Do not point CorsTests at `PayApiFactory` unless necessary — extra OneClient replace is unrelated to CORS. Harmless if you do.

### 7.3 IsolationTests (keep and slightly widen)

Banned substrings on **csproj** text: `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`.

Also: every csproj under Pay root must not contain `apps/lazuar-api` / `apps\lazuar-api`.

Source `src/**/*.cs`: no `MediatR`, `Modules.One`, `BuildingBlocks`.

**Does not scan** `Lazuar.Pay.slnx`, README, or test `.cs`. README **must** name `lazuar-api` as a refuse; do not scan markdown. **Do** scan the slnx so a “convenience” solution folder cannot appear. **Do** scan test `.cs` for `using MediatR` if cheap.

Do not replace this with NetArchTest.Rules (Hub package). File text is the style 012 locked.

### 7.4 Endpoint matrices (keep)

Whoami, OrgReady, Checkout tests already lock:

- 401 without Bearer, SendCount 0
- Forward Bearer to `/me` or `/authz/check`
- Path org vs header org
- 403 when `allowed: false`
- 503 on One 500 / timeout / transport
- Checkout idempotency in-process
- Checkout 404 unknown without One
- Currency default MYR
- Amount > 0
- Health still skips One

Production PRs that add a DB must **keep** these HTTP facts. A Testcontainers checkout test is additive.

### 7.5 What not to add in the name of production

| Temptation | Why refuse |
|------------|------------|
| NSubstitute `IOneClient` | Skips header bugs |
| WireMock | Extra process; FakeOneHandler is enough |
| FluentAssertions | Tests use NUnit `Assert.That` |
| Live Zitadel in `pay:test` | 012-03; env-gated curl is enough |
| Testcontainers **Zitadel** | One is HTTP; fake the HTTP |
| ArchitectureTests project | IsolationTests is the architecture test |
| Sharing Hub `Lazuar.IntegrationTests` helpers | Path to Modules |
| `WebApplicationFactory` hitting real 8081 | WAF is in-process; live curl is documented in README |

### 7.6 HealthTests vs factory

Keep `Health_returns_ok` / `V1_health_returns_ok` on raw WAF so a `ValidateOnStart` that requires a fake One cannot silently break liveness **or** — if you add ValidateOnStart that needs One:BaseUrl — committed appsettings default keeps them green. `Health_does_not_call_one` stays on `PayApiFactory`.

When `/health/ready` appears: raw WAF without Postgres should **fail ready** (503), not 200. That is the test that stops ready == live. Liveness still 200 without Postgres.

### 7.7 CorsTests (keep, extend when config exists)

Keep 5178 allow, 5179 allow, 3003 deny on `GET /health`. When CORS is configured, add: Production-like settings with empty origins fail boot (optional WAF test); a list that includes `http://localhost:3003` still yields no ACAO **if** you implement a deny-list. Minimum: default config never includes 3003; CorsTests remain the lock.

OPTIONS preflight is untested. A single OPTIONS test for 5178 on `/v1/whoami` would lock `AllowAnyMethod`. Not required to keep the seam.

### 7.8 IsolationTests widen (recommended in the same production program, still this host)

| Scan | Today | Recommend |
|------|-------|-----------|
| Host csproj banned tokens | yes | keep |
| Test csproj banned tokens | yes | keep |
| src `*.cs` MediatR / Modules.One / BuildingBlocks | yes | keep; add `IRequestHandler`, `AddMediatR` |
| all csproj `apps/lazuar-api` | yes | keep |
| `Lazuar.Pay.slnx` | **no** | **yes** |
| test `*.cs` MediatR | no | yes if cheap |
| README | no | **never** (must name the museum) |

### 7.9 Live One

README curl against 8081 with `$ACCESS_TOKEN` remains the live dogfood. **Not** `pay:test`. Do not add `[Explicit]` live tests that fail CI when One is down. Paper 012-03 already settled this; production does not reopen it.

---

## 8. Cathedral contamination risks

The cathedral is `apps/lazuar-api` plus Hub UIs. Contamination is **copying its shape into `apps/lazuar-pay` because production is scary.** Each row is a PR you should reject even if tests pass.

### 8.1 MSBuild / solution

| Risk | How it starts | Why it is fatal |
|------|----------------|-----------------|
| Add Pay csproj to `Lazuar.slnx` | “one solution in the IDE” | IsolationTests does not scan Hub slnx; Hub `task api:build` starts compiling Pay; ProjectReference to Modules becomes one click |
| `Directory.Build.props` at repo root | “share TFM” | Pay inherits `ManagePackageVersionsCentrally` and Hub’s MediatR versions |
| `ProjectReference` to `Modules.One.Infrastructure` | “authz/check is already implemented” | You **are** Modules/One. IsolationTests must fail |
| `ProjectReference` to `Lazuar.ApiContracts` / `packages/api-types-dotnet` | “DTOs exist” | Old TypeSpec surface, old nouns (`/one/auth/me`) |
| `ProjectReference` to BuildingBlocks | “need IEventBus” | Event bus to talk to yourself |
| Central package versions from Hub `Directory.Packages.props` | “Npgsql version already pinned” | Pulls MediatR 12.4.1 into the same file you “only wanted Npgsql from” |

IsolationTests already forbids several of these **as substrings in Pay’s own files**. It cannot forbid editing `Lazuar.slnx` in the other folder. Human review + paper 10 CI grep.

### 8.2 Program.cs gravity (the 240-line Hub root)

Hub `Program.cs` does, in order: hand-rolled `.env` loader, Azure Key Vault, Serilog, BackgroundWorker options, Observability options, **platform admin password**, metrics collector hosted service, optional API-key migrator hosted service, optional webhook-subscription migrator, `IHttpContextAccessor`, memory cache, `IExecutionContextAccessor`, `IPasswordService`, `IJwtService`, `ISecretVault` (AES), `InMemoryEventBus`, R2/S3, API key cache, `AddLazuarAuthentication`, `AddLazuarAuthorizationPolicies`, `AddLazuarCors`, snake_case JSON (the one line worth stealing — **already stolen**), `AddExceptionHandler`, `AddProblemDetails`, **`AddLazuarMediatR`**, **`AddAllModules`**, **`MigrateAllModuleDatabasesAsync`**, pipeline, module endpoints.

Pay `Program.cs` does: snake_case JSON, One options, HttpClient, CheckoutStore, CORS, four map groups, Run.

**Every Hub line is a contamination candidate** the first week someone says “production.” Allowed steal list is short:

| Hub line | Steal? |
|----------|--------|
| `ConfigureHttpJsonOptions` snake_case | Already in Pay |
| Console logging | Already MEL; Serilog console optional later |
| CORS fail-boot if empty in prod | **Idea only** — rewrite, do not copy `AddLazuarCors` (`AllowCredentials`, Hub origin list) |
| Exception handler → JSON | **Idea** — `PayErrors`, not `GlobalExceptionHandler` from BuildingBlocks |
| HEALTHCHECK curl `/health` | **Idea** — port **8081** |
| Caddy terminates TLS | **Shape** — new hostname |
| `MigrateAllModuleDatabasesAsync` | **Never** |
| `AddLazuarMediatR` / `AddAllModules` | **Never** |
| `IJwtService` / `IPasswordService` | **Never** |
| `InMemoryEventBus` | **Never** |
| Key Vault try/catch continue | **Never** as a pattern (fail boot if prod secrets missing) |
| Hand-rolled `.env` parser | **Never** |
| Platform admin password in config | **Never** |

### 8.3 Persistence gravity

| Offer | Refuse |
|-------|--------|
| Nine `HasDefaultSchema` | One schema |
| `OutboxMessages` + `InboxMessages` on every context | Pay talking to Pay is a function call |
| `IgnoreQueryFilters` worker with empty tenant | No ambient tenant filter copied from Hub |
| `dotnet ef` nine Taskfile lines | `pay:db:migrate` once |
| `CommerceDbContext` because the table is checkout | `checkouts` table |
| Copy `commerce.CheckoutSessions` model | New table, paper 09 maps Hub data if ever |

### 8.4 Auth gravity

| Offer | Refuse |
|-------|--------|
| `AddLazuarAuthentication` | Forward Bearer |
| `Jwt:Secret` | One is the IdP |
| Cookie `SignIn` / `lazuar_auth` | SPA sends access_token |
| `POST /one/auth/login` on 8081 | Never |
| JwtBearer with `MapInboundClaims` Zitadel roles | `/me` + `authz/check` |
| `IOneClient` + mock | Fake handler |

### 8.5 Frontend / CORS gravity

| Offer | Refuse |
|-------|--------|
| Add `http://localhost:3003` “so ops can try Pay” | CorsTests |
| `AllowCredentials` because Hub has cookies | Bearer |
| `AllowAnyOrigin` in Development | Explicit 5178/5179 |
| Point `VITE_API_URL` of ops at 8081 | Papers 01/04 |

### 8.6 Package gravity (011-05 applied to production)

| NuGet | First excuse | Verdict |
|-------|----------------|---------|
| MediatR | “thin endpoints” | IsolationTests; the endpoint **is** the use case |
| FluentValidation | “pipeline” | Validate in the handler |
| ErrorOr / Ardalis.Result | “OneCallResult is amateur” | `OneCallResult` is enough |
| AutoMapper | “DTOs” | Hand map |
| Polly / Microsoft.Extensions.Http.Resilience | “production HTTP” | Do not retry `/me` |
| OpenTelemetry.* | “observability” | Not in repo; MEL/Serilog first |
| Serilog.AspNetCore | “Hub uses it” | **Allowed later** as console JSON, not as BuildingBlocks |
| EF + nine contexts | “we know it” | One context or SQL |
| Stripe.net on the host the same week as MediatR | “BYOK” | Stripe.net may appear for paper 06; MediatR still no |
| NSubstitute | “faster tests” | Fake handler |
| NetArchTest | “stronger isolation” | File bans are the style |

Hub `Directory.Packages.props` also pins FluentAssertions, NSubstitute, NetArchTest, OpenAI, Razorpay, QuestPDF, Newtonsoft. **None** of those belong on the focused host csproj because Hub’s file listed them.

A **legitimate** first host `PackageReference` is Npgsql or EF when the DB appears, or Serilog.AspNetCore when JSON logs appear, or Stripe.net when paper 06 lands. IsolationTests should not become a forever-empty allowlist that forbids Npgsql. Optional: IsolationTests continues to ban **names**, not package count.

### 8.7 “Just this once” MediatR

011-05: *A rewrite in C# is the highest risk of rebuilding the museum with cleaner names.* The sentence that appears in production PRs is: “MediatR only for the checkout module so we can extract it later.” That is how nine modules started. IsolationTests must keep failing `MediatR` as a **substring** in src and csproj. There is no allowlist file.

`IRequest<CreateCheckoutResponse>` is not a production requirement. `CheckoutEndpoints.Create` already is the handler.

### 8.8 BuildingBlocks

Hub BuildingBlocks include Application (execution context, observability ports), Infrastructure (EF, config, observability collector), HTTP bits. Pay needs none of them to listen on 8081. Copying `IExecutionContextAccessor` is how ambient tenant returns; Hub workers then `IgnoreQueryFilters`. Do not.

### 8.9 Hub env.example as a template

`deploy/prod/env.example` is Hub. Copying it into a Pay deploy folder copies Jwt, Kms, three connection strings, Billplz, OpenAI, integrator provision secret. Pay’s future `deploy/pay/env.example` should look like `.env.example` **grown by a few lines**, not like Hub’s.

### 8.10 Test gravity

Hub tests: ArchitectureTests, IntegrationTests, ModuleTests, Billing, Ops, Testcontainers, NSubstitute, FluentAssertions, NetArchTest. Pay tests: one project, NUnit, WAF, FakeOneHandler. Production does not require a second test assembly named `Lazuar.Pay.ArchitectureTests`.

### 8.11 README / Taskfile gravity

`pay:test` description still says health + isolation — harmless. `task dev` still starts Hub. Do not make `task dev` start Pay on 8081 **and** Hub on 8080; they cannot share 8080 with One. `task pay:dev` stays the Pay entry.

---

## 9. Anti-goals

If a PR does any of these, it is the wrong production program even if the process “feels deployable.”

1. **Bind 8080.** Steal One’s (and Hub’s) port.
2. **Add `apps/lazuar-pay` to `Lazuar.slnx`.**
3. **MediatR, `IRequest`, `AddMediatR`, `Modules.*`, BuildingBlocks, `ProjectReference` into `apps/lazuar-api`.** IsolationTests is the floor, not the ceiling.
4. **Nine schemas / nine DbContexts / `MigrateAllModuleDatabasesAsync`.**
5. **Second org/membership/users table as SoT.** One tenant id is `org_id`.
6. **Pay-issued JWT / `Jwt:Secret` / cookie session / `POST /one/auth/login`.**
7. **Zitadel PAT, OpenFGA admin, Zitadel masterkey, One webhook AES wrap key** in Pay env.
8. **Whoami middleware** that calls `/me` on every request, including health.
9. **Health or ready that calls One.**
10. **Retry `/me`.**
11. **CORS allow `localhost:3003` / `3004` / `3005` / `5173`.** CorsTests must stay red if you do.
12. **Retarget `lazuar-ops` / `lazuar-portal` at 8081.**
13. **Dockerfile `EXPOSE 8080` / `ASPNETCORE_URLS=http://+:8080` / HEALTHCHECK on 8080.**
14. **Publish as `lazuar-hub-api`.**
15. **AllowCredentials + Hub cookies.**
16. **IOneClient mock** as the only whoami test.
17. **Live Zitadel in `pay:test`.**
18. **Treat in-memory `CheckoutStore` as production money.**
19. **Redis-as-checkout-store** to avoid Postgres.
20. **OpenTelemetry collector graph** before JSON logs exist.
21. **Copy Hub `PlatformMetricsCollector` / LHDN gauges.**
22. **Azure Key Vault try/catch continue** as the secrets story.
23. **Hand-rolled `.env` parser** from Hub `Program.cs`.
24. **`JwtBearer` as membership SoT** (Zitadel role claims).
25. **Buyer becomes a Zitadel user.**
26. **Rate-limit `/health`.**
27. **Ready probe requires One up.**
28. **Add Pay to Hub mprocs / Hub Caddy `/api/*` handle as a silent swap.**
29. **Polly on OneClient.**
30. **Generate C# from One OpenAPI / Kiota into this host** as a production requirement.
31. **NetArchTest / FluentAssertions / NSubstitute** as a production testing upgrade.
32. **UserSecretsId `lazuar-api-dev-secrets`.**
33. **`Kms:MasterKey` fallback to `Jwt:Secret`.**
34. **Outbox/inbox tables so checkout can talk to ledger in another “module.”**
35. **Reopen Go-vs-C#** as a reason not to pin 8081 or add a connection string.

---

## 10. Open questions (pick from what the repo already uses)

These are real choices. This paper picks a **default** where the repo already has a default; remaining forks stay named.

### 10.1 Which migrator?

**What the repo already uses:** EF Core `Database.MigrateAsync` per module `DbContext`, `dotnet ef migrations add` in Taskfile against nine infrastructure projects, Npgsql EF provider in Hub `Directory.Packages.props` (`Npgsql.EntityFrameworkCore.PostgreSQL` 10 preview). There is **no** DbUp, **no** FluentMigrator, **no** golang-migrate, **no** Atlas in this tree.

**Default for Pay (C# host that exists):** **one** EF Core `PayDbContext` (or raw SQL + the same EF history table) in **this** host project or a folder inside it — **not** a `Modules/Commerce/Infrastructure` project. One `dotnet ef database update --context PayDbContext`. Refuse a second context.

**Better 011-05 alignment, not already in the repo:** SQL files in `src/Lazuar.Pay/sql/` applied by a tiny runner. Introducing DbUp is a **new** tool. Do not introduce it *and* EF. Pick one.

**Open:** EF-one-context vs SQL files. **Closed:** nine contexts. **Closed:** migrate Hub schemas. **Closed:** copying `DatabaseMigrationExtensions`.

If EF: Design package on the host or a tiny `Lazuar.Pay.csproj` still the only production project (Design can be PrivateAssets). Do not a second “Infrastructure” csproj “because EF likes it.”

### 10.2 Which log stack?

**What the focused host already uses:** `Microsoft.Extensions.Logging` console, appsettings `LogLevel`.

**What Hub already uses:** `Serilog.AspNetCore` 9.0.0 + `Serilog.Sinks.Console` 6.0.0, `UseSerilog()`, console sink, no Seq, no App Insights **as a product package**.

**What the repo does not use:** OpenTelemetry.* packages, Prometheus middleware, Application Insights SDK (the DLL in test `bin/` is VSTest telemetry, not a choice).

**Default:** stay on MEL console until a production deploy needs JSON lines. Then **Serilog.AspNetCore + Console** (the packages the repo already pins in Hub) with a JSON formatter, **without** `BuildingBlocks.Infrastructure.Observability`. Do not add OpenTelemetry because a blog post said so.

**Closed:** copying `PlatformMetricsCollector` / LHDN gauges. **Open:** when to switch MEL → Serilog (a real VPS deploy is the trigger, not this paper).

### 10.3 Ready probe shape?

**Hub:** `/health/ready` asks a metrics collector about DB + outbox lag.

**Default for Pay:** `/health` liveness remains `{status:ok}` no I/O. `/health/ready` (or `/ready`) `SELECT 1` against Pay’s database when it exists; **503** if not; **never One**. Until a DB exists, do not add ready (it would duplicate live and lie). After a DB exists, ready is mandatory before compose health-gates traffic.

**Open:** path name `/health/ready` vs `/ready`. Prefer `/health/ready` only if you want Hub operators to feel at home; prefer `/ready` if you want to look unlike Hub. Either is fine if health never calls One.

### 10.4 Listen pin in appsettings vs only env?

**Open, weakly:** optional `"Urls": "http://localhost:8081"` in appsettings vs only `ASPNETCORE_URLS` in image. **Closed:** launchSettings stays 8081. **Closed:** never 8080.

### 10.5 502 vs 503 for One failures?

Implementation and tests: **503** for almost everything except 401/403. 012-03 wanted 502 for bad gateway. **Default: keep 503** until a client cares. Document in pay-spec if you split. Do not flip without tests.

### 10.6 JWT signature at the edge?

**Default: not in the first production-shape PR.** Forwarding is the model. Optional later JWKS check is hardening, not SoT. **Closed:** Hub Jwt:Secret.

### 10.7 Rate limiter API?

**Default:** ASP.NET `AddRateLimiter` (shared framework) when a public buyer door exists or whoami is abused. **Closed:** copy Hub `*RateLimiter` classes.

### 10.8 TLS certificates in Kestrel vs Caddy?

**Default:** Caddy (or equivalent) like Hub prod. Process HTTP 8081. **Closed:** Kestrel HTTPS on 8081 as a requirement.

### 10.9 Testcontainers for Pay DB?

Hub test csproj graph already has `Testcontainers.PostgreSql`. Pay tests do not. **Default:** when `checkouts` is PG-backed, either Testcontainers in **Pay test csproj** or CI service container `lazuar_pay`. **Closed:** reuse `lazuar_mvp` + nine schemas.

### 10.10 Serilog vs MEL for the first prod deploy?

See §10.2. **Trigger:** first VPS/container that is not a laptop. Until then MEL is a production-enough laptop logger. Do not block a Dockerfile on Serilog.

### 10.11 Connection string name?

**Open:** `ConnectionStrings:Pay` vs `Default`. Prefer `Pay` so nobody points it at Hub `Default` by muscle memory. **Closed:** `TenantConnection` + `MessagingConnection`.

### 10.12 Schema name `public` vs `pay`?

**Open, weakly.** `public` is fewer moving parts. `pay` is a fence if someone mistakenly grants this role on a shared cluster. **Closed:** `commerce` / `billing` / `one`.

### 10.13 Boot migrate vs init job?

Hub boots `MigrateAsync` for nine contexts and documents the race. **Default for Pay:** `task pay:db:migrate` / container init **before** ready, especially before replicas. Single-replica boot migrate on **one** context is acceptable as a start. **Closed:** nine-context loop.

---

## 11. Binding rules for the later implementer

1. **One host project. `Lazuar.Pay.slnx` only. Stay out of `Lazuar.slnx`.**
2. **Listen 8081. Never 8080. Image `ASPNETCORE_URLS=http://+:8081`.**
3. **One Pay database when a DB appears. One schema. One migrator. No per-module DbContexts.**
4. **`CheckoutStore` is an in-memory fixture until a table replaces it. Do not ship money on it.**
5. **`GET /v1/whoami` is an endpoint. Health never calls One. Ready never calls One.**
6. **Caller Bearer is forwarded. No Pay-issued JWT. No PAT. No FGA admin. No masterkey.**
7. **CORS may move to config but `:3003` stays denied. CorsTests stay.**
8. **`FakeOneHandler` + `PayApiFactory` stay the One test seam. No `IOneClient` mock-only tests.**
9. **IsolationTests stay and keep forbidding `lazuar-api` / `Modules.` / `BuildingBlocks` / `MediatR` / `Lazuar.Api`.**
10. **Do not copy Hub Program.cs, nine migrations, MediatR, BuildingBlocks, Jwt/Kms env, or Hub Caddy `/api` handle.**
11. **Gateway later terminates TLS. Process HTTP on 8081.**
12. **C# gravity is the defect. Production is not a second cathedral.**
13. **Frontends are papers 04–05. Money rails 06–07. This paper does not implement them.**
14. **`public partial class Program;` and `InternalsVisibleTo` stay.**
15. **Zero host PackageReference until a real one (Npgsql/EF/Serilog/Stripe) has a noun. Never MediatR.**

---

## 12. Inventory of seams (index)

| Seam | Location today | Production insert |
|------|----------------|-------------------|
| Composition | `Program.cs` | connection, ready, CORS config, exception handler — still visible |
| JSON | `ConfigureHttpJsonOptions` + `OneClient.Json` | keep snake_case |
| One HTTP | `OneClient` | stay typed client; validate options at boot |
| Auth | `Bearer` + `MemberGate` | stay endpoint/function; optional JWKS later |
| Checkout durability | `CheckoutStore` singleton dict | `checkouts` + idempotency table |
| CORS | four literals | `Cors:Origins`; fail boot if empty in prod; never 3003 |
| Listen | launchSettings 8081 | env URL 8081 in image |
| Liveness | `/health`, `/v1/health` | keep; never One |
| Readiness | **missing** | `/health/ready` = Pay DB only, after DB exists |
| Logs | MEL console | JSON console / Serilog console later; redact Authorization |
| TLS | none | Caddy; process HTTP |
| Rate limit | none | `AddRateLimiter` on public doors later |
| Dockerfile | none | **8081**, not Hub 8080 |
| Compose | Hub `api` 8080 | new `pay` 8081; not ops/portal retarget |
| CI | Hub slnx only | `Lazuar.Pay.slnx` job (paper 10) |
| GHCR | Hub images | new name, not `lazuar-hub-api` |
| Isolation | IsolationTests | keep; scan slnx too |
| One tests | FakeOneHandler | keep |
| Secrets | `.env.example` Two keys | grow honestly; never PAT |
| Persistence law | no DB | §4 tables, not nine schemas |

The host is still one project, one slnx, six maps, a fake HTTP handler, and a dictionary pretending to be a card network. Production is **more of that honesty**, plus a database and a listen pin — not a copy of `Lazuar.Api/Program.cs`.

---

## 13. Endpoint list (repeat, complete)

Application maps in `Program.cs` at SHA `6f866ff0`:

1. `GET /health` → `{status:ok}` — liveness, no One  
2. `GET /v1/health` → `{status:ok}` — liveness, no One  
3. `GET /v1/whoami` → `WhoamiEndpoints.Handle` — Bearer → One `GET /me`  
4. `GET /v1/orgs/{orgId}/ready` → `OrgReadyEndpoints.Handle` — Bearer + `authz/check` member  
5. `POST /v1/checkouts` → `CheckoutEndpoints.Create` — Bearer + member + **in-memory** 201  
6. `GET /v1/checkouts/{id}` → `CheckoutEndpoints.Get` — 404 if missing (no One); else Bearer + member of `session.OrgId`

CORS: default policy via `UseCors()` on all of the above.

That is the entire sold HTTP surface of the process. Everything else in this paper is how that surface becomes something you can run without lying about restarts, origins, ports, or identity.

---

End of paper. **Not an implementation.** Re-inventory `apps/lazuar-pay` at HEAD before applying any sketch.
