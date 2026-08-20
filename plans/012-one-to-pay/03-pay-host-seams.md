# 03 — Pay host seams: insert One HTTP trust (first slice)

**Date:** 20 August 2026  
**Repo:** `lazuar-pay` (this tree)  
**SHA:** `6ca8f19f4b28c056f852b7b579b5b30428e48ad6`  
**Branch at writing:** `feat/012-one-to-pay`  
**Commit subject at SHA:** `feat(pay): add TypeSpec package for the focused Pay host`  
**Host at SHA:** scaffold from `b536993a` (`feat(pay): scaffold focused Pay host on 8081`) plus pay-spec. Still health-only. No One call. No money.  
**Type:** Seam analysis for the **new focused Pay host** at `apps/lazuar-pay`. **Not an implementation.** No code in this paper is to be applied as a patch from this file.  
**Scope of this paper:** `Program.cs`, the host csproj, the test project, `package.json`, Taskfile `pay:*`, port **8081**, and how to add **HttpClient → One**, a **GET /v1/whoami** door, and (if anything) middleware, **without MediatR** and **without a `Modules/One` copy**.

Parent product law: `plans/011-new-lazuar-pay/` — especially [02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) (what Pay calls on One), [03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) / [12-first-slice-tracker.md](../011-new-lazuar-pay/12-first-slice-tracker.md) (dogfood order), [05-language.md](../011-new-lazuar-pay/05-language.md) (C# gravity), [04-linux-shape.md](../011-new-lazuar-pay/04-linux-shape.md) (call the function *inside* Pay), [08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md) (HTTP *to* One and on Pay’s `/v1`).

This slice is **S0 proof that the new host can trust One over HTTP**. It is not SPA registration, not OIDC PKCE, not `authz/check`, not webhooks, not BYOK keys, not checkout. Tracker rows this slice can honestly start (and only these):

| ID | Why this slice |
|----|----------------|
| NP-ONE-003 | Send the caller’s **access_token** (or `lzr_sk_`) as `Authorization: Bearer` — Pay forwards, does not invent a second token |
| NP-ONE-006 | Reach One `GET /me` (Pay’s sold name for that snapshot is `/v1/whoami`) |
| NP-API-001 family (door) | Public `/v1` exists; this adds the first authenticated door beside `/v1/health` |
| NP-ONE-020 (partial) | Pay still must not hold a Zitadel PAT / FGA admin token; `OneOptions` this slice is **BaseUrl + Timeout only** |

It does **not** complete NP-ONE-001 (register SPA), NP-ONE-002 (OIDC in Pay), NP-ONE-007 (path `{tenantId}` authz), NP-ONE-015 (`authz/check` on admin routes). Those need more than a whoami relay.

---

## 0. Verdict (read this before the inventory)

**Stay on one host project.** Add a folder of **plain types + one typed `HttpClient`**, not a module, not a class library, not MediatR, not a reverse proxy.

**`GET /v1/whoami` is an endpoint, not global middleware.** The handler forwards the incoming `Authorization` (and optional `X-Lazuar-Tenant-Id` hint) to One `GET {BaseUrl}/me`, maps a small DTO, and returns it. Health stays anonymous and must not call One.

**Do not call One `/me` on every request.** One documents that `GET /me` can **write** (domain auto-join, SSO JIT). Hammering it from middleware is both a hot-loop and a write amplifier. 011 already forbids that.

**Tests replace `HttpMessageHandler`, not One with a live Zitadel.** `WebApplicationFactory<Program>` already exists. A fake handler is the right seam. Live dogfood is optional, env-gated, and not required for `task pay:test`.

**Keep listening on 8081.** Do not bind 8080. 8080 is One’s API in the sibling identity repo and also the historical listener in this monorepo’s *other* host. Focused Pay exists on 8081 so those can keep 8080.

**Do not add this host to `Lazuar.slnx`.** That solution is the old tree. This host already has `apps/lazuar-pay/Lazuar.Pay.slnx`.

**No Dockerfile this slice.** Absence is correct.

**C# gravity (011-05) is the real risk, not missing NuGet.** The host is already `net10.0` + minimal APIs. That is fine. The failure mode is the default C# business-app toolkit: `IWhoamiQuery` + MediatR + `IOneService` + `OneModule.AddOne()` + `BuildingBlocks.Http` + a project reference into a Contracts assembly. Write this like a small ASP.NET app: one project, functions, `HttpClient`, options. Fight the ecosystem for this folder the way 011 said you must if you stay on C#.

This paper does **not** reopen the Go-vs-C# kernel choice. The focused host that exists is C#. Insert One HTTP *here*.

---

## 1. Title / date / SHA (binding)

| Field | Value |
|-------|--------|
| Title | Pay host seams: insert One HTTP trust (first slice) |
| Date | 20 August 2026 |
| SHA of `lazuar-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` |
| Parent of SHA | `b536993a` (host scaffold on 8081) |
| Host TFM | `net10.0` (`global.json` pins SDK `10.0.100`, `rollForward: latestFeature`; restore graph on this machine used SDK 10.0.101) |
| Listen | `http://localhost:8081` via `Properties/launchSettings.json` only (see §7 — this is a real seam, not already pinned in `Program.cs`) |

If this file is read after later commits, treat the SHA above as the **analysis baseline**. Re-inventory `apps/lazuar-pay` before implementing; do not assume `Program.cs` is still nine lines.

---

## 2. Current file inventory

The focused host is small. **Source of truth for this paper is the tree under `apps/lazuar-pay/` excluding `bin/` and `obj/`** (those are gitignored via root `.gitignore` `[Bb]in/` / `[Oo]bj/`). There is no app-local `.gitignore`. There is no `Directory.Build.props` / `Directory.Packages.props` under `apps/lazuar-pay` or at repo root, so MSBuild will **not** accidentally inherit the old tree’s props: Directory.Build.* walks *up* from the csproj (`apps/lazuar-pay/src/Lazuar.Pay/` → `src/` → `apps/lazuar-pay/` → `apps/` → repo root), never sideways into a sibling app folder.

### 2.1 Tree (source, tests, workspace glue)

```
apps/lazuar-pay/
  global.json                          SDK pin 10.0.100
  Lazuar.Pay.slnx                      THIS host’s solution (two projects)
  package.json                         pnpm package name `lazuar-pay`
  README.md                            8081, pay:* tasks, do not copy Modules/One
  src/Lazuar.Pay/
    Lazuar.Pay.csproj                  Sdk.Web, no PackageReference
    Program.cs                         health only + public partial Program
    appsettings.json                   Logging + AllowedHosts
    appsettings.Development.json       Logging only
    Properties/launchSettings.json     applicationUrl http://localhost:8081
  tests/Lazuar.Pay.Tests/
    Lazuar.Pay.Tests.csproj            NUnit + Mvc.Testing; ProjectReference to host
    HealthTests.cs                     WAF GET /health and /v1/health
    IsolationTests.cs                  host csproj string bans
```

No `Dockerfile`. No `.dockerignore`. No `appsettings.Production.json`. No `UserSecretsId`. No `.http` file. No `nunit.runsettings`. No `Directory.Build.props`. No second class library. No `One/` folder yet. No authentication. No `HttpClient` registration (the *namespace* `System.Net.Http` is already in the SDK implicit usings for the Web SDK — that is not a registered client).

`packages/pay-spec/` is **not** inside `apps/lazuar-pay/` but is the focused host’s TypeSpec (server `http://localhost:8081`, today only `GET /v1/health`). Task `pay:spec` compiles it. This slice should grow that spec when `/v1/whoami` exists (Bezos door honesty). It is a sibling package, not a project reference, and not a reason to generate C# models this slice.

### 2.2 `Program.cs` (entire host today)

Nine lines. Top-level statements. No services registered. No middleware pipeline (`Use*` never called — the default WebApplication pipeline is empty of auth, CORS, exception handler, HTTPS redirection). Two GETs. `app.Run()` with **no URL argument**. `public partial class Program;` so `WebApplicationFactory<Program>` can see the entry point.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

Implications for the slice:

- There is **nowhere** to hang `AddHttpClient` except *before* `builder.Build()`. That is the DI seam.
- There is **nowhere** to hang `UseAuthentication` / custom middleware except *after* `Build()` and *before* `MapGet`. That is the pipeline seam. This paper recommends leaving it empty for whoami (see §4).
- `Results.Ok(new { status = "ok" })` is anonymous-type JSON. Default System.Text.Json on ASP.NET Core is **camelCase**. `status` is already lowercase, so health JSON is `{"status":"ok"}`. HealthTests assert substring `"ok"`, not a schema. Whoami fields from One are **snake_case** (`user_id`, `active_tenant_id`). Decide JSON policy *now* (see §10.8) before a third endpoint exists.
- `public partial class Program;` must stay. Do not convert to a `Program.Main` in another file without keeping a public Program type the WAF can use. `InternalsVisibleTo` is already on the host csproj for the test assembly.

### 2.3 Host csproj

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

- **Zero `PackageReference`.** Framework reference is `Microsoft.AspNetCore.App` (shared framework). Restore graph `project.assets.json` for the host has `"targets": { "net10.0": {} }` and empty libraries. `IHttpClientFactory`, `IOptions<T>`, `HttpClient`, `System.Text.Json`, `WebApplicationFactory`’s host bits — the *production* ones — are already in that shared framework. **This slice does not need a NuGet to talk HTTP.**
- `TreatWarningsAsErrors` is on. Unused usings, nullable holes, and dead parameters fail the build. Keep new files used. Do not leave `#pragma warning disable` as a substitute.
- `InternalsVisibleTo` already names `Lazuar.Pay.Tests`. Internal types in an `One/` folder are visible to tests without making them public. Prefer `internal` for One DTOs that are not the sold `/v1` shape.
- There is no `ProjectReference`. IsolationTests will fail if one is added whose path or name contains the banned substrings (see §2.6).

### 2.4 Test csproj

TFM `net10.0`, `IsPackable=false`, `IsTestProject=true`, `ImplicitUsings`, `Nullable`, `LangVersion=latest`. Packages (versions as restored on this SHA):

| Package | Version | Why it is already there |
|---------|---------|-------------------------|
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.11 | `WebApplicationFactory<Program>` in HealthTests |
| `Microsoft.NET.Test.Sdk` | 17.14.0 | VSTest / `dotnet test` |
| `NUnit` | 4.3.2 | `[Test]`, `Assert.That` |
| `NUnit3TestAdapter` | 5.0.0 | runner |
| `NUnit.Analyzers` | 4.7.0 | analyzers |
| `coverlet.collector` | 6.0.4 | coverage collector (not required for this slice) |

Single `ProjectReference`: `..\..\src\Lazuar.Pay\Lazuar.Pay.csproj`.

There is **no** `NSubstitute`, **no** WireMock, **no** `Microsoft.AspNetCore.Authentication.JwtBearer`, **no** `RichardSzalay.MockHttp`. Do not add them for this slice. A subclass of `HttpMessageHandler` in the test project is enough (see §6). `HttpClient` is already in test implicit usings.

Global usings generated for tests: `NUnit.Framework`, `System.Net.Http`, basic BCL. Not `System.Net.Http.Json` — tests that parse JSON should `using System.Text.Json` or `ReadAsStringAsync` + substring, matching HealthTests’ style, or add the using.

`MvcTestingAppManifest.json` in `obj` maps assembly `Lazuar.Pay` to the host project directory. WAF content root is the host project. `appsettings.json` **is** loaded in tests. That matters when we add an `One` section (see §5.4).

### 2.5 Tests as they stand

**HealthTests** (26 lines): two facts. `GET /health` and `GET /v1/health` are success and the body contains `ok`. Each test `await using var factory = new WebApplicationFactory<Program>();` — no custom factory, no config override, no `ConfigureTestServices`. This is the **regression tripwire** for the slice: if `OneOptions` `ValidateOnStart` requires a missing section, or if a middleware calls One with no handler, **health goes red**. The implementation must keep these two tests green without a live network.

**IsolationTests** (34 lines): reads **only** `src/Lazuar.Pay/Lazuar.Pay.csproj` by walking parents of `AppContext.BaseDirectory` until `src/Lazuar.Pay/Lazuar.Pay.csproj` exists (that works because test output is `tests/Lazuar.Pay.Tests/bin/Debug/net10.0/`, and `apps/lazuar-pay/` is an ancestor that contains `src/Lazuar.Pay/...`). Asserts the csproj text does **not** contain:

- `lazuar-api`
- `Modules.`
- `BuildingBlocks`
- `MediatR`
- `Lazuar.Api`

It does **not** scan the test csproj, `Program.cs`, other `.cs` files, or `Lazuar.Pay.slnx`. A `ProjectReference` to a banned tree added **only** on the test project would pass IsolationTests today. A `using MediatR;` in a new `.cs` file would pass IsolationTests today. **Expand the scan in the same slice as One HTTP** (see §6.5). Do not weaken the existing string bans.

IsolationTests itself has **no** project reference except the host. It must stay that way. Do not make IsolationTests open a path into another app in order to assert the old solution file is untouched — that would be IsolationTests “referencing” that tree. The ban lives as **strings inside the focused host’s own files**.

### 2.6 `package.json` (pnpm / turbo)

```json
{
  "name": "lazuar-pay",
  "version": "0.0.0",
  "private": true,
  "scripts": {
    "build": "dotnet build Lazuar.Pay.slnx",
    "test": "dotnet test Lazuar.Pay.slnx --nologo --verbosity minimal",
    "dev": "dotnet watch run --project src/Lazuar.Pay/Lazuar.Pay.csproj",
    "lint": "dotnet format Lazuar.Pay.slnx --verify-no-changes",
    "format": "dotnet format Lazuar.Pay.slnx",
    "check-types": "dotnet build Lazuar.Pay.slnx --no-incremental"
  }
}
```

Workspace: root `pnpm-workspace.yaml` includes `apps/*`, so `pnpm --filter lazuar-pay test` and `turbo run test` already include this package. `pnpm-lock.yaml` has `apps/lazuar-pay: {}` (no Node dependencies). **No package.json change is required** to add HttpClient. `dev` does not pass `--urls`. It relies on launchSettings (see §7).

### 2.7 Taskfile `pay:*`

Defined at repo root `Taskfile.yml` with `dir: apps/lazuar-pay` (except `pay:spec`).

| Task | Command | Seam notes |
|------|---------|------------|
| `pay:restore` | `dotnet restore Lazuar.Pay.slnx` | Enough; no extra feeds. Host still has zero packages. |
| `pay:build` | `dotnet build Lazuar.Pay.slnx` (deps restore) | `TreatWarningsAsErrors` on host |
| `pay:test` | `dotnet test Lazuar.Pay.slnx --nologo --verbosity minimal` | Description today: “health + isolation”. After this slice it will also run whoami tests. **Do not** point `pay:test` at live One. Description string can be updated when tests exist; behavior stays “all tests in this slnx”. |
| `pay:dev` | `dotnet watch run --project src/Lazuar.Pay/Lazuar.Pay.csproj` | Description already: `http://localhost:8081` (other host stays on 8080). Cwd is `apps/lazuar-pay`, so launchSettings next to the csproj is found. |
| `pay:spec` | `pnpm exec tsp compile .` in `packages/pay-spec` | Grow `main.tsp` when whoami is sold. Not a C# compile. |

No `pay:live` task exists. Optional live dogfood can stay a documented curl, not a Taskfile, until someone wants `pay:whoami-live` that fails closed without env (see §6.6).

Root `task dev` still starts the **other** API via pnpm. `mprocs-dev.yaml` does **not** include focused Pay. `docker-compose.yml` still publishes **8080** for the other API. **Do not** add focused Pay to compose or mprocs in this slice — that is how you accidentally steal 8080 or start two money hosts. `task pay:dev` is the way to run 8081 by hand.

### 2.8 `Lazuar.Pay.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Lazuar.Pay/Lazuar.Pay.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj" />
  </Folder>
</Solution>
```

Two projects. This is the **only** solution this host belongs in. Adding `Lazuar.Pay.One.csproj` here would already be the wrong shape (see §3). Adding this csproj to the other solution is forbidden (see §8).

### 2.9 Config and listen files

**`appsettings.json`:** `Logging:LogLevel` Default Information, `Microsoft.AspNetCore` Warning; `AllowedHosts: *`. No `Urls`. No `Kestrel` section. No `One` section.

**`appsettings.Development.json`:** logging only. Overrides nothing about URLs.

**`Properties/launchSettings.json`:** one profile `http`, `commandName: Project`, `launchBrowser: false`, `applicationUrl: http://localhost:8081`, `ASPNETCORE_ENVIRONMENT=Development`. This is the **only** 8081 pin in source. `Program.cs` does not call `UseUrls`. Kestrel’s default when launchSettings is not applied is **not** 8081 (historically http://localhost:5000, and in many container images 8080). See §7.

### 2.10 README promises (host)

`apps/lazuar-pay/README.md` already states the laws this slice must not violate:

- New money process. Not the modular monolith in the other app folder.
- One solution, one host, one test project.
- Listen on **8081**.
- Merchants come from **lazuar-one** (not yet wired). Do not copy `Modules/One`.
- Do not add MediatR, per-module DbContexts, or a project reference into the other API app.
- TypeSpec is `packages/pay-spec`, not `packages/api-spec`.
- Compose still points at the other API; swap later when S1 dogfood is real.

Whoami is the first “wired” sentence. Do not “wire” it by copying identity tables into Pay.

### 2.11 Implicit usings (generated, not source)

Host `Lazuar.Pay.GlobalUsings.g.cs` already includes `System.Net.Http` and `System.Net.Http.Json`. Implementing `OneClient` does not need extra usings for `HttpClient` / `GetFromJsonAsync` (this slice should prefer `SendAsync` anyway, to set headers per request). Tests do **not** get `Http.Json`.

### 2.12 What is *not* in the host (on purpose)

| Absent | Why it must stay absent this slice |
|--------|-------------------------------------|
| MediatR, FluentValidation, ErrorOr, Ardalis.Result | C# gravity. IsolationTests already bans MediatR in the host csproj |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Whoami forwards a Bearer to One; Pay does not validate Zitadel JWTs yet |
| OpenIdConnect handler | Browser login is a later S0 step; this host is not the SPA |
| YARP / `MapForwarder` | Would leak One’s URL shape and skip Pay’s DTO |
| Refit, Flurl, RestSharp, Kiota-generated One client | Extra surface; repo root `dotnet-tools.json` has Kiota for *other* work — do not point it at this host |
| Polly / `Microsoft.Extensions.Http.Resilience` | `/me` can write; do not retry |
| EF, Npgsql, Redis | No Pay database in this slice |
| Serilog, OpenTelemetry packages | Not required to prove trust |
| Dockerfile / bake target | §9 |
| Second `.csproj` | §3 |

---

## 3. Recommended project shape for the first slice

### 3.1 Still one host project

Keep:

- `src/Lazuar.Pay/Lazuar.Pay.csproj` as the only production project
- `tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj` as the only test project
- `Lazuar.Pay.slnx` as the only solution

Do **not** create:

- `src/Lazuar.Pay.One/Lazuar.Pay.One.csproj`
- `src/Lazuar.Pay.Application/`
- `src/BuildingBlocks.Http/`
- `Modules/One/{Domain,Application,Infrastructure,Contracts}`
- A NuGet `Lazuar.One.Client` in `packages/` this slice (One’s unpublished workspace clients are TypeScript; Pay may not wait on npm, and must not vendor a C# SDK that does not exist)

A second project is how C# gravity starts: “the client might be reused.” There is one consumer (this host). When a second *process* needs the same client, extract. Not now.

### 3.2 A folder of plain types, not a module

Recommended layout **inside** the existing host (names are the sketch; not applied code):

```
src/Lazuar.Pay/
  Program.cs                 # register options + HttpClient; MapGet whoami; health unchanged
  appsettings.json           # add One:BaseUrl, One:TimeoutSeconds; optionally Urls
  appsettings.Development.json
  Properties/launchSettings.json
  One/
    OneOptions.cs            # BaseUrl, TimeoutSeconds
    OneClient.cs             # typed HttpClient: GetMeAsync(authorization, tenantHint, ct)
    OneMe.cs                 # internal DTO matching One GET /me (snake_case via STJ)
    OneResult.cs             # small result union: Ok / Upstream / Unreachable
  Whoami/
    WhoamiResponse.cs        # sold /v1 DTO (Pay’s door, not One’s document)
    WhoamiEndpoint.cs        # static Handle(HttpContext, OneClient, ct) → IResult
```

Why `One/` + `Whoami/` instead of `One/` only: **One is the upstream; whoami is the door.** Mixing them in one file is fine while both are tiny; splitting the sold DTO from the upstream DTO is the seam that prevents “return One’s JSON verbatim forever.” If you keep a single `One/Whoami.cs` that contains both records plus the endpoint method, that is still acceptable. Folders are not bounded contexts.

Why this is **not** a module:

| Module smell (refuse) | Folder of types (do) |
|-----------------------|----------------------|
| `OneModule.AddOne(this WebApplicationBuilder)` that hides MediatR + DbContext + outbox | 8–15 lines in `Program.cs`: `Configure<OneOptions>` + `AddHttpClient<OneClient>(...)` + `MapGet("/v1/whoami", WhoamiEndpoint.Handle)` |
| `IOneService` / `IOneFacade` / `IWhoamiQuery` | `OneClient` is a class. Tests fake HTTP, not an interface (see §6.3) |
| `GetMeQuery` + `GetMeQueryHandler` + `IRequest<WhoamiResponse>` | A static method or a two-line lambda |
| `OneDbContext`, membership tables, “cache of tenants” | No database. One tenant id becomes Pay `org_id` **later**, not this slice |
| `One:Infrastructure` vs `One:Application` | One class that calls HTTP |
| Integration events `UserSignedIn` | None |
| Architecture test allowlist | IsolationTests string bans only |

011-04 (Linux shape): inside Pay, call the function. `WhoamiEndpoint.Handle` calls `one.GetMeAsync`. That is a function call. 011-08 (Bezos door): the *sold* thing is `GET /v1/whoami`. 011-02: the *other product* is reached with HTTP. Those three sentences are the whole architecture of this slice.

### 3.3 Keep maps in `Program.cs` until it is noisy

Today `Program.cs` is nine lines. After this slice it might be ~40 lines (options, client, three maps). That is still the right place to *see the whole host*. Extract `WhoamiEndpoint.Handle` so Program does not contain JSON mapping. Do not extract `AddPayOne(this IServiceCollection)` into a 200-line extension “for cleanliness” — that is how composition roots disappear.

A reasonable Program sketch (not applied):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<OneOptions>()
    .Bind(builder.Configuration.GetSection(OneOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(static o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var u)
                          && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps),
              "One:BaseUrl must be an absolute http(s) URI")
    .ValidateOnStart();

builder.Services.AddHttpClient<OneClient>((sp, http) =>
{
    var one = sp.GetRequiredService<IOptions<OneOptions>>().Value;
    http.BaseAddress = OneOptions.ToBaseAddress(one.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(one.TimeoutSeconds);
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/whoami", WhoamiEndpoint.Handle);

app.Run();

public partial class Program;
```

No `app.UseAuthentication()`. No `app.UseAuthorization()`. No `app.UseMiddleware<OneTrustMiddleware>()`.

### 3.4 Namespaces

Match the test project: file-scoped namespaces.

- `Lazuar.Pay` — Program (implicit)
- `Lazuar.Pay.One` — options, client, upstream DTOs
- `Lazuar.Pay.Whoami` — sold DTO + endpoint
- `Lazuar.Pay.Tests` — existing; add `WhoamiTests`, `PayWebApplicationFactory`, `FakeOneHandler`

Do not use `Lazuar.Modules.One`. Do not use `Lazuar.Pay.BuildingBlocks`.

### 3.5 C# gravity checklist (011-05 applied to this folder)

011-05: *“C# is fine if you write it like a 2008 ASP.NET app: one project, one schema, SQL or a single EF context, handlers as functions, no event bus. That requires fighting the ecosystem every week.”*

This slice has **no schema**. The fight is still real. Refuse, by name:

| Gravity offer | Why it is wrong here |
|---------------|----------------------|
| MediatR because “endpoints should be thin” | The endpoint *is* the use case: forward Bearer, map JSON. A handler type is a second home for ten lines |
| `IOneClient` because “tests need mocks” | Tests need a fake **transport**. `HttpMessageHandler` is that. An interface lets tests skip the header-forwarding bugs |
| `IHttpClientFactory` wrapper service | `AddHttpClient<OneClient>` already is the factory. Don’t wrap the wrap |
| AutoMapper / Mapster | Two records. Map by hand (`user_id` → `user_id`) |
| FluentValidation pipeline | DataAnnotations on `OneOptions` + a `Validate` lambda. Request validation is “Authorization present” |
| ProblemDetails NuGet | `Results.Problem(...)` / `TypedResults.Unauthorized()` |
| `Result<T>` library | Internal `OneResult<T>` with three cases is enough; or just return `(int status, T? body)` |
| JwtBearer + `MapInboundClaims` | Pay must not parse `urn:zitadel:iam:org:project:roles`. Membership SoT is `/me` |
| Caching `/me` in `IMemoryCache` this slice | Right instinct for later hot paths; wrong for a single whoami door (caller opted in). Don’t invent TTL before a second caller |
| `DelegatingHandler` “auth handler” that reads `HttpContext` via `IHttpContextAccessor` | Hidden ambient token. Pass `Authorization` as a method argument |
| Generating a C# client from One’s OpenAPI | Couples Pay’s compile to One’s whole surface. Hand-write `GetMe` only |
| Copying One’s membership model into Pay POCOs “for later EF” | That is a second org table in waiting. NP-XX-014 refuse |

If a PR for this slice adds any PackageReference to the **host** csproj, IsolationTests plus review should ask “is this in the shared framework already?” `AddHttpClient` is.

### 3.6 What “trust One” means in this host (and what it does not)

Trust, this slice:

1. The **caller** of Pay presents a Bearer (user access_token or `lzr_sk_`).
2. Pay **does not inspect** the token (no JWT parse, no Zitadel JWKS, no key format check beyond “header present”).
3. Pay **forwards** that Bearer to One `GET /me`.
4. One is the authority: 200 means this principal exists and these memberships are true; 401 means Pay 401s.
5. Pay **maps** a sold JSON door so merchants/clients hit `http://localhost:8081/v1/whoami`, not One’s origin.

Trust is **not**:

- Pay becoming an OIDC relying party (later).
- Pay holding `ZITADEL_PAT`, OpenFGA store admin, or One’s webhook AES (never).
- Pay attaching **its own** `lzr_sk_` to whoami. If it did, whoami would be Pay’s machine identity, not the caller’s. `OneClient` must take `authorization` per call. **No** `DefaultRequestHeaders.Authorization` on the typed client from config.
- Pay authorizing money routes from `X-Lazuar-Tenant-Id`. The hint may be forwarded; 011 says never authorize by header alone. Whoami may echo `active_tenant_id` from One when the hint matched. That is still a hint.

---

## 4. Middleware vs endpoint-only forwarding for whoami

This is the design fork the slice actually has. Everything else is options and tests.

### 4.1 What “forwarding” could mean (four shapes)

**A. Endpoint-only (recommended).**  
`MapGet("/v1/whoami", ...)` is the only code that calls One. Health does not. Future routes do not, until they have their own reason (`authz/check` on admin POST, etc.).

**B. Global middleware that calls `/me` and sets `HttpContext.Items` / `HttpContext.User`.**  
Every request, including `/health` unless skipped, becomes an upstream round-trip.

**C. Middleware on a `MapGroup("/v1")` excluding health.**  
Still a `/me` call per request the moment a second `/v1/*` route exists (checkout, webhooks). Webhooks must **not** present a One user JWT; they present a PSP signature. A group-wide One middleware would break the money path later.

**D. Reverse-proxy middleware (YARP / `HttpContext` copy to One and stream the response).**  
Pay would not own the JSON. Path would be One’s `/me` or a rewrite. Status codes and error bodies would leak One’s ProblemDetails unchanged. Pay’s `/v1` would not be a product.

### 4.2 Why endpoint-only wins for this slice

1. **`GET /me` can write.** 011-02: domain auto-join, SSO JIT. Middleware on every Pay request would create/join as a side effect of a health-check scraper or a buyer hitting a pay link. Buyers must not become One humans (NP-CHK-007 / NP-XX-013). A public `/health` must not JIT-join anything. Even `/v1/whoami` is an **explicit** “tell me who One says I am” — that is the one place a write-on-read is acceptable, because the caller asked for identity.

2. **011-02: do not hammer `/me` from a hot loop.** Middleware *is* a hot loop. Whoami is on-demand.

3. **Failure domains.** If One is down, whoami should 502/503. `/health` and later `POST /v1/checkouts` (buyer, no One account) must not 502 because identity is down. 011-07: money stays true in Pay if membership lags. A global One middleware inverts that.

4. **Buyer plane.** Hosted checkout is anonymous to One. Global auth middleware is how someone “accidentally” requires a Zitadel login on the cash register.

5. **The host has no auth pipeline today.** Adding `UseAuthentication` + JwtBearer is a second product (JWKS, authority `http://localhost:8085`, audience, claim mapping). 011 says do **not** parse Zitadel project-role claims; chrome SoT is `/me` + `authz/check`. Local JWT validation without `/me` would invent a second role vocabulary.

6. **Test surface.** Endpoint-only: one WAF test file, fake handler, done. Middleware: order, skip-lists, `IHttpContextAccessor`, “does /health skip?”, “does OPTIONS skip?”.

7. **Bezos door.** The sold operation is `GET /v1/whoami`. Middleware is not an operation. You cannot put middleware in TypeSpec.

### 4.3 When middleware *would* be justified (not this slice)

Later S0, merchant admin routes will need “this Bearer is a member of `{tenantId}`.” Options that are still not “call `/me` every time”:

| Later tool | Role |
|------------|------|
| Endpoint filter / `RequireOneBearer()` on a `MapGroup("/v1/...")` **that is not checkout and not PSP webhooks** | Explicit opt-in, still better than `UseMiddleware` at the app root |
| `POST {One}/tenants/{id}/authz/check` | Authz SoT for mutating merchant routes (NP-ONE-015). Different HTTP call than `/me` |
| Short-TTL cache of `/me` keyed by token hash | Only after a second Pay route needs the snapshot *and* a profiler says so. Hash the token; never log it |
| Local JwtBearer **signature** check (issuer 8085) **plus** `/me` for membership | Optional hardening so obviously garbage JWTs never leave Pay. Still not role SoT. Still not this slice |

If someone adds middleware this slice, the only **acceptable** kind is **not One-calling**:

- `UseExceptionHandler` mapping unhandled `HttpRequestException` — but the endpoint should catch that itself and return 502, so the middleware is redundant.
- Request logging that **redacts** `Authorization`. Not required.

Do **not** add correlation-id / HTTPS-redirection / CORS middleware “while we’re in Program.cs.” CORS belongs when a browser origin exists (Pay SPA). Buyer and merchant origins are not this slice.

### 4.4 Endpoint behavior (contract of the door)

`GET /v1/whoami`

| Incoming | Action |
|----------|--------|
| No `Authorization` header, or empty, or not starting with `Bearer ` (case-insensitive scheme) | **401** immediately. **Do not** call One. Fake handler must not be invoked in tests |
| `Authorization: Bearer …` | Forward that header **verbatim** to One `GET {BaseUrl}/me` (keep the `Bearer ` prefix) |
| `X-Lazuar-Tenant-Id` present | Forward as the same header. One may set `active_tenant_id` if it matches a membership. Pay still does not authorize from it |
| `X-Lazuar-Tenant-Id` absent | Do not invent one |
| Other incoming headers (`Cookie`, `Host`, `X-Forwarded-*`, `Cookie` session) | **Do not** forward. Pay is not a transparent proxy |

Upstream mapping:

| One HTTP | Pay HTTP | Body |
|----------|----------|------|
| 200 + JSON | 200 | Pay `WhoamiResponse` (mapped; see §10.5). Do not pass through unknown One-only staff fields blindly — known fields only. Extra One properties dropped |
| 401 | 401 | Pay problem (`status: 401`). Do not copy One’s body (might change). Do not leak One URL |
| 403 | 403 | Same |
| 404 | 502 | `/me` should not 404 for a well-configured One; treat as bad gateway |
| 409 / 422 | 502 | Unexpected on GET /me |
| 429 | 503 | Optional `Retry-After` if One sent one; not required this slice |
| 5xx | 502 | Bad gateway |
| Connect refused, DNS, TLS, `TaskCanceledException` (timeout) | 503 | Pay is up; identity is unreachable. Distinguish from 502 “One answered badly” if cheap; if not, 502 for all upstream failures is acceptable if tests lock one choice |
| Unparseable 200 JSON | 502 | Do not 200 with a partial lie |

Liveness: `GET /health` and `GET /v1/health` remain **200 `{status:ok}` with no outbound HTTP**. Never wait on One to say the process is alive.

Do not add `GET /v1/me` as an alias. One’s name is `/me`. Pay’s sold name is `/whoami` (One’s own CLI already calls the *command* `whoami` while the HTTP path is `/me`). One door, one name.

Do not add `GET /whoami` unversioned. Public door is `/v1` (011-08). Unversioned `/health` may stay as process liveness next to `/v1/health` (already there).

### 4.5 Reject: authenticating whoami with Pay-issued cookies

No cookie session this slice. No `SignInAsync`. Bearer in, JSON out. The SPA (later) will send the access_token. A `lzr_sk_` will send the key. Both are Authorization headers. That is the whole auth story for whoami.

---

## 5. Options pattern for `OneOptions` (`BaseUrl`, `Timeout`)

### 5.1 Shape

```csharp
namespace Lazuar.Pay.One;

internal sealed class OneOptions
{
    public const string SectionName = "One";

    [Required]
    public string BaseUrl { get; set; } = "http://localhost:8080/api/v1";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 5;

    public static Uri ToBaseAddress(string baseUrl)
    {
        var u = baseUrl.Trim().TrimEnd('/') + "/";
        return new Uri(u, UriKind.Absolute);
    }
}
```

Bind from section `"One"`. Environment override (ASP.NET Core convention):

- `One__BaseUrl`
- `One__TimeoutSeconds`

JSON:

```json
"One": {
  "BaseUrl": "http://localhost:8080/api/v1",
  "TimeoutSeconds": 5
}
```

Use **`TimeoutSeconds` as `int`**, not `TimeSpan Timeout`. TimeSpan binding from JSON (`"00:00:05"` vs `"5"` vs `"00:00:05.000"`) is a footgun in appsettings. An integer is honest. Map to `http.Timeout = TimeSpan.FromSeconds(TimeoutSeconds)` in `AddHttpClient`.

`BaseUrl` **includes One’s `/api/v1` prefix**. Then the client path is `"me"` (or `"/me"`) and the wire URL is `http://localhost:8080/api/v1/me`. That matches One’s documented product surface and 011-02 (“Routes are One `/api/v1` unless noted”).

If `BaseUrl` is accidentally `http://localhost:8080` (origin only), whoami becomes `GET http://localhost:8080/me` and One 404s → Pay 502. Optional extra validate: `BaseUrl` path should contain `api/v1`. Worth doing; cheap; saves a dogfood hour.

`ToBaseAddress` must end with `/` so `new Uri(base, "me")` replaces the last segment correctly (`.../api/v1` + `me` without trailing slash becomes `.../api/me` — a classic `HttpClient.BaseAddress` bug). This is a **required** implementation detail, not style.

### 5.2 What `OneOptions` must not contain this slice

| Property | Why not |
|----------|---------|
| `Authority` / `ClientId` / `Audience` | OIDC is NP-ONE-002, not whoami |
| `ApiKey` / `lzr_sk_` / `ServiceToken` | Whoami is the **caller’s** Bearer. A config key would impersonate Pay |
| `ZitadelPat` | NP-ONE-020 never |
| `FgaStoreId` | Never in Pay |
| `WebhookHmac` | Later, NP-ONE-017 |
| `Retries` | Do not retry `/me` |
| `EnableWhoami` feature flag | If the door is mapped, it is real. Don’t ship a dark route |

Secrets stay out of committed appsettings. There are no secrets in this slice’s options. Do not add `UserSecretsId` “for later.”

### 5.3 Defaults vs environment

**`appsettings.json` (committed):** default `BaseUrl` `http://localhost:8080/api/v1`, `TimeoutSeconds` 5. This is local dogfood against sibling One, **and** it keeps `ValidateOnStart` from breaking HealthTests (WAF loads this file).

**`appsettings.Development.json`:** can repeat the same BaseUrl or omit (inherit). Do not point at a hosted SKU. One staging proof is NOT PASSED (011-02); local is the target.

**Production (when it exists):** set `One__BaseUrl` via env / compose. Do not invent `appsettings.Production.json` this slice. There is no production host yet (no Dockerfile).

**Tests:** WAF should `UseSetting("One:BaseUrl", "http://one.test/api/v1")` so a mistaken real handler cannot graze localhost:8080. The fake handler never uses the host, but the typed client still needs a valid absolute URI.

### 5.4 `ValidateOnStart` vs HealthTests

If options validation runs at startup (recommended — fail boot on `BaseUrl=""`), then:

- Committed default in `appsettings.json` keeps HealthTests green without a custom factory.
- A custom `PayWebApplicationFactory` is **still** recommended so whoami tests and health tests share one place that installs the fake handler. Health then cannot accidentally call a real network if someone later “helpfully” pings One from a hosted service constructor.

Do not put HTTP in `OneClient`’s constructor. Constructor only stores `HttpClient` + logger. First outbound bytes happen in `GetMeAsync`.

`ValidateDataAnnotations()` requires `using System.ComponentModel.DataAnnotations` on `OneOptions`. That namespace is in the shared framework. Do not add a NuGet.

`TreatWarningsAsErrors`: `[Required]` on a string with a non-null default is fine; the default exists so JSON omit still binds.

### 5.5 Timeout policy

Five seconds is plenty for `/me` on localhost and a starting point for remote. `HttpClient.Timeout` covers the whole request. Also pass `CancellationToken` from `HttpContext.RequestAborted` so a disconnected caller does not keep an upstream call. Do not add a second `CancellationTokenSource` unless you want a per-call timeout shorter than the client timeout — unnecessary if both are 5s.

Do **not** retry. `/me` is not idempotent in the “safe to replay” sense One documented (it can write). One timeout → 503 once.

### 5.6 Named client vs typed client

Use **typed** `AddHttpClient<OneClient>`. The name the factory uses is `"OneClient"` (CLR type name). Tests that reconfigure `HttpClientFactoryOptions` must use that name (see §6.4).

Do not also register a named client `"One"` unless you want two sockets. Pick one: typed.

`IHttpClientFactory.CreateClient("One")` inside a static local function is worse: no compile-time miss, easy to forget BaseAddress. Typed client constructor injection in the endpoint is the Linux shape (`Handle` calls a function on a concrete type).

---

## 6. Test plan

### 6.1 Goals

| Goal | How |
|------|-----|
| `task pay:test` / `pnpm --filter lazuar-pay test` never needs One, Zitadel, Postgres, or the network | Fake `HttpMessageHandler` |
| Health still 200 | Same as today; factory must not break it |
| Isolation still forbids the museum | Expand file scan; keep strings |
| Whoami header-forwarding is tested, not mocked away | Fake **handler**, not `IOneClient` |
| Live dogfood is optional | Env-gated / `[Explicit]` / `Assert.Ignore`; default skip |

### 6.2 Keep HealthTests

Do not rewrite them to require One. Optionally switch them to `PayWebApplicationFactory` so every WAF in the project installs the fake handler. If you do that, add one assertion: **GET /health does not invoke the fake handler** (call count 0). That locks “liveness is local.”

```
Health_returns_ok
V1_health_returns_ok
Health_does_not_call_One
```

### 6.3 Fake One handler (the seam)

A test-only type, e.g. `tests/Lazuar.Pay.Tests/FakeOneHandler.cs`:

- Subclass `HttpMessageHandler`.
- Override `SendAsync`.
- Record `HttpRequestMessage` (method, `RequestUri`, `Authorization`, `X-Lazuar-Tenant-Id`, that `Cookie` was **not** copied).
- Return a configured `HttpResponseMessage` (status + JSON body), or throw `HttpRequestException`, or delay past timeout.
- Thread-safe enough for one test at a time (WAF per test is sequential in NUnit default).

Do **not** use WireMock. Do **not** spin `HttpListener`. Do **not** use NSubstitute on `HttpMessageHandler` — subclassing is clearer and avoids a new package.

Optional second type: `OneClientTests` that do `new OneClient(new HttpClient(handler) { BaseAddress = new Uri("http://one.test/api/v1/") }, NullLogger<OneClient>.Instance)` **without** WAF. These lock URI combination (`.../api/v1/me`), JSON deserialization, and error mapping. They do not lock that `Program.cs` mapped `/v1/whoami`. Need **both** layers:

| Layer | Instantiation | What it proves |
|-------|----------------|----------------|
| `OneClient` unit | `new OneClient(http, log)` | Path, headers, DTO, status mapping |
| Endpoint / WAF | `PayWebApplicationFactory` | Route exists, 401 without calling One, JSON door, DI wiring |

Skipping the client unit tests is acceptable if WAF tests assert URI and headers on the handler (they will). Skipping WAF and only unit-testing the client is **not** acceptable — the sold door could be unwired.

### 6.4 `WebApplicationFactory` and replacing the handler (the sharp edge)

Production registers `AddHttpClient<OneClient>(...)`. Tests must replace the **primary handler** of that client, not the whole `OneClient` type (replacing the type with a hand-rolled stub would skip HTTP assertions).

Naive `ConfigureTestServices(s => s.AddHttpClient<OneClient>().ConfigurePrimaryHttpMessageHandler(() => fake))` **adds a second** typed-client registration. Last-wins depends on DI order and is a known source of “tests still hit the network.”

Recommended pattern (sketch):

1. Create the fake handler instance on the factory (`public FakeOneHandler One { get; } = new();`).
2. `builder.UseSetting("One:BaseUrl", "http://one.test/api/v1");` `UseSetting("One:TimeoutSeconds", "5");`
3. In `ConfigureTestServices`, configure the **existing** named client:

```csharp
services.Configure<HttpClientFactoryOptions>(nameof(OneClient), options =>
{
    options.HttpMessageHandlerBuilderActions.Add(b =>
    {
        b.PrimaryHandler = One; // the fake
    });
});
```

`HttpClientFactoryOptions` lives in `Microsoft.Extensions.Http`. Available from the shared framework. Typed client name is the type name `OneClient`.

Alternative that also works: `ConfigureTestServices` `RemoveAll<OneClient>()` then `AddSingleton(new OneClient(new HttpClient(One) { BaseAddress = ... }, logger))`. This **bypasses** `IHttpClientFactory` in tests (no handler rotation). Acceptable. Slightly less faithful. Prefer the `HttpClientFactoryOptions` hook so production and tests share the same registration path.

Do **not** implement `IHttpMessageHandlerFactory` (not a public seam).

`PayWebApplicationFactory` should live in the test project:

```csharp
internal sealed class PayWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("One:BaseUrl", "http://one.test/api/v1");
        builder.UseSetting("One:TimeoutSeconds", "5");
        builder.ConfigureTestServices(services =>
        {
            services.Configure<HttpClientFactoryOptions>(nameof(OneClient), options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = One);
            });
        });
    }
}
```

HealthTests may keep raw `WebApplicationFactory<Program>` **if** the default BaseUrl never gets used. Prefer migrating them to `PayWebApplicationFactory` so there is one factory.

NUnit + WAF: `await using var factory = new PayWebApplicationFactory();` per test, same as today. Do not build a static factory for the fixture unless you reset `FakeOneHandler` state; per-test is simpler and matches HealthTests.

### 6.5 Whoami test matrix (unit / WAF, no live One)

All of these belong in `WhoamiTests.cs` (or split Client vs Endpoint). Names are illustrative.

| Test | Arrange | Assert |
|------|---------|--------|
| `Whoami_without_authorization_is_401_and_does_not_call_One` | no header | 401; `One.Calls == 0` |
| `Whoami_with_empty_bearer_is_401_and_does_not_call_One` | `Authorization: Bearer ` | 401; no call |
| `Whoami_forwards_bearer_to_One_me` | Bearer `tok`; One 200 sample JSON | handler URI ends with `/me` (and includes `/api/v1/`); method GET; `Authorization` is `Bearer tok` |
| `Whoami_forwards_tenant_hint` | header `X-Lazuar-Tenant-Id: t1` | same header on upstream |
| `Whoami_does_not_forward_cookie` | incoming Cookie | upstream has no Cookie |
| `Whoami_maps_me_json` | One 200 with `user_id`, `email`, `tenants[]`, `is_platform_admin`, `active_tenant_id`, `active_role` | Pay 200 JSON has those sold names; `tenants[0].id/slug/name/role` |
| `Whoami_One_401_becomes_401` | upstream 401 | Pay 401; body is Pay problem, not necessarily One’s |
| `Whoami_One_403_becomes_403` | upstream 403 | 403 |
| `Whoami_One_500_becomes_502` | upstream 500 | 502 |
| `Whoami_One_unreachable_becomes_503` | handler throws `HttpRequestException` | 503 (or 502 if you locked a single upstream-failure code — pick one and test it) |
| `Whoami_One_garbage_json_becomes_502` | 200 `not-json` | 502 |
| `Whoami_does_not_log_the_token` | (optional) test logger sink | no `tok` in logged messages |

Sample One 200 body to freeze mapping (from One’s `MeResponse` + 011-02):

```json
{
  "user_id": "u1",
  "email": "ada@acme.test",
  "name": "Ada",
  "tenants": [
    {
      "id": "t1",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active",
      "permissions": ["tenant:read"]
    }
  ],
  "is_platform_admin": false,
  "active_tenant_id": "t1",
  "active_role": "owner"
}
```

Include `permissions: []` on tenants if you map that field (chrome hint, never authz). API-key flavor: `user_id` is a key id, `tenants` 0–1, `is_platform_admin` always false. One test of that shape is enough to prove Pay does not assume a human email.

NUnit: stay on `[Test]` + `Assert.That`. Do not add FluentAssertions.

### 6.6 Optional live dogfood (not `pay:test`)

A live test is **optional**. It is not required to merge the slice. If added:

- Class or method marked `[Explicit]` **or** first line `if (Environment.GetEnvironmentVariable("PAY_ONE_LIVE") != "1") Assert.Ignore("live One dogfood");`
- Reads `One__BaseUrl` (default localhost:8080/api/v1) and `PAY_ONE_BEARER` from env. **Never** commit a token.
- `GET http://localhost:8081/v1/whoami` is **not** what the WAF does — WAF in-process is not 8081. A live test should either:
  - **A (preferred for CI-off laptop):** `WebApplicationFactory` **without** fake handler, so the typed client really calls One on 8080. Still does not bind 8081. Requires One up, not Zitadel if you use a minted `lzr_sk_`.
  - **B:** skip WAF; `HttpClient` to `http://localhost:8081` assuming `task pay:dev` is running. Fragile in CI. Document as curl instead.

**Recommended:** do **not** add a live NUnit test this slice. Document curl in the host README:

```bash
# One API on :8080, focused Pay on :8081, old host not listening
task pay:dev
curl -sS http://localhost:8081/v1/whoami \
  -H "Authorization: Bearer $LAZUAR_ONE_TOKEN" \
  -H "Accept: application/json"
```

`$LAZUAR_ONE_TOKEN` is an access_token from login `:5175` or a `lzr_sk_` minted in One. Pay does not obtain it. This is Consumer-0 dogfood of NP-ONE-003 + NP-ONE-006.

Live failure modes to expect (not to paper over):

| Symptom | Likely cause |
|---------|----------------|
| Pay 401, handler never… (live: One 401) | Bad token, wrong scheme (`id_token` instead of access_token) |
| Pay 502 | BaseUrl missing `/api/v1`, or One not that path |
| Connect 503 | One not on 8080; or the *other* host is on 8080 and is not One |
| CORS errors | curl has no CORS; browsers will, later |

Zitadel (`:8085`) is **not** a Pay dependency for whoami. If the token is already minted, Pay never talks to 8085. Unit tests must not mention 8085.

### 6.7 IsolationTests — keep and widen

Keep the existing host-csproj test. Add, in the same class or a neighbor:

1. **Scan every `*.csproj` under the Pay root** (`src/` and `tests/`). Same banned substrings. This catches a test-only `ProjectReference` into the old tree.
2. **Scan every `*.cs` under `src/Lazuar.Pay/`** (not `bin`/`obj`). Ban:
   - `MediatR`
   - `IRequestHandler`
   - `INotificationHandler`
   - `AddMediatR`
   - `using MediatR`
   - `Modules.` (false-positive risk is low in this host)
   - `BuildingBlocks`
   - `lazuar-api`
   - `Lazuar.Api`
3. **Scan `Lazuar.Pay.slnx`** for the same strings (would catch a solution-level path into the other app).
4. **Do not** open files outside `apps/lazuar-pay`. Isolation is “this host does not name the old tree,” not “this test compiles the old tree.”

Optional extra asserts (cheap, high value):

- Host csproj still has **no** `PackageReference` **or** an allowlist that starts empty. If you prefer not to freeze “zero packages” forever, skip this; HttpClient does not need a package. A later Stripe SDK will need one. Don’t make IsolationTests a package allowlist this slice.
- Host csproj still `InternalsVisibleTo` the tests (prevents accidental delete).

Do not import an architecture-test framework. File text is enough. That is the same style as the existing test.

### 6.8 What not to test this slice

- OIDC code flow, PKCE, refresh.
- `authz/check`.
- Tenant create.
- JWT signature.
- OpenFGA.
- Pay database.
- TypeSpec compile inside `dotnet test` (that is `pay:spec` / pnpm). Optionally a later honesty test that `/v1/whoami` is in `packages/pay-spec/dist/openapi.yaml` — nice, not required to insert the client.

### 6.9 Test project packages

Do not add packages. `Microsoft.AspNetCore.Mvc.Testing` already pulls TestHost. `IOptions` and `HttpClientFactoryOptions` are available to the test assembly through the host.

If `HttpClientFactoryOptions` is awkward to name from tests, the `RemoveAll<OneClient>()` + singleton `HttpClient(fake)` alternative needs **zero** extra types from the factory.

---

## 7. Keep listening on 8081; do not steal 8080

### 7.1 Why 8081 exists

| Listener | Port | Who |
|----------|------|-----|
| Focused Pay (this host) | **8081** | `apps/lazuar-pay`, this paper |
| Sibling One API | **8080** | `lazuar-one` product surface `/api/v1` |
| Historical host in this monorepo | **8080** | must keep it; focused Pay was split *so that* 8080 stays theirs |
| Zitadel | **8085** | issuer; Pay does not bind it and does not call it this slice |
| Login | **5175** | product sign-in; not Pay’s homepage |
| Stock Login V2 | **3005** | break-glass; never ship merchants there |
| `lazuar-admin` | **5173** | staff; merchants never |

Focused Pay on 8081 is what makes **One on 8080 + Pay on 8081** a legal laptop layout. If this host binds 8080, Consumer-0 dogfood is impossible without stopping One. If this host binds 8080 while the historical host is running, bind fails. **Stealing 8080 is a product bug, not a preference.**

### 7.2 How 8081 is pinned today (weak)

Only `Properties/launchSettings.json` `applicationUrl`. That applies when:

- `dotnet run` / `dotnet watch run` from the project (Taskfile `pay:dev`, `pnpm --filter lazuar-pay dev`)
- Visual Studio / `commandName: Project`

It does **not** apply when:

- `dotnet /path/Lazuar.Pay.dll`
- A container (ASP.NET Core 8+ images often listen **8080** by default — the exact port this host must not steal)
- `ASPNETCORE_URLS` is set in the environment to something else
- Tests (WAF uses TestServer; **must not** bind 8081 or 8080; HealthTests already don’t)

### 7.3 What this slice should do about the pin

Minimum: **do not change launchSettings off 8081.** Do not add a second profile on 8080. Do not set `ASPNETCORE_URLS=http://+:8080` in any file under `apps/lazuar-pay`.

Recommended (still analysis, not applied): pin in **appsettings** as well so `dotnet Lazuar.Pay.dll` is honest:

```json
"Kestrel": {
  "Endpoints": {
    "Http": {
      "Url": "http://localhost:8081"
    }
  }
}
```

or the older `"Urls": "http://localhost:8081"`.

WAF/TestServer typically does not open a real socket; HealthTests should remain green. If a test ever used `UseKestrel` + real port, it must pick **0** (ephemeral) or 8081, never 8080.

Do **not** call `app.Run("http://localhost:8080")`.

Do **not** add focused Pay to root `docker-compose.yml` this slice. Compose already maps `8080:8080` for the other API. A second service `8080:8081` is easy to mistype as `8080:8080`.

### 7.4 `One:BaseUrl` and 8080

`One:BaseUrl` **pointing at** `http://localhost:8080/api/v1` is not stealing 8080. That is the client calling One. Pay **listens** on 8081 and **dials** 8080. Keep those directions straight in README:

- Listen: 8081
- One API (outbound): 8080

If a future operator sets `One:BaseUrl=http://localhost:8081`, whoami becomes a loop. Optional validate: BaseUrl host/port must not equal the listen URL. Not required this slice; a comment in appsettings is enough.

### 7.5 pay-spec server URL

`packages/pay-spec/main.tsp` already has `@server("http://localhost:8081", "Local focused Pay host")`. When whoami is added to TypeSpec, keep that server. Do not “fix” it to 8080.

---

## 8. Do not add to `Lazuar.slnx` (old solution)

The focused host’s solution is **`apps/lazuar-pay/Lazuar.Pay.slnx`**.

The other solution file in this monorepo is the historical API’s. **Do not add** `src/Lazuar.Pay/Lazuar.Pay.csproj` or the test project to it. Reasons:

- IsolationTests forbids the host csproj from even **naming** that tree (`lazuar-api`, `Lazuar.Api`, `Modules.`, `BuildingBlocks`, `MediatR`). Being *in* that solution is how those project references become “convenient.”
- `task api:build` / `task api:restore` must not start compiling focused Pay.
- Two money hosts in one solution is how 8080/8081 confusion lands in launch profiles.

`task pay:*` already `dir: apps/lazuar-pay` and uses `Lazuar.Pay.slnx`. `package.json` scripts the same. Turbo `build`/`test` for `lazuar-pay` the same. There is **no** missing glue that `Lazuar.slnx` would provide.

IsolationTests must **not** open the other solution file (that would be a path reference). It should only ensure **this** slnx stays two-project and does not contain banned strings.

Do not create `Lazuar.sln` (old-style) at repo root for “IDE convenience” that includes both hosts.

---

## 9. Dockerfile: none yet, fine

There is no `apps/lazuar-pay/Dockerfile`. Root `docker-bake.hcl` / `docker-compose.yml` / `docker-compose.ghcr.yml` do not mention this host. README: “Compose still points at the other API. Swap later when S1 dogfood is real.”

**This slice must not add a Dockerfile.** Reasons:

- Images default to 8080; easy steal.
- GHCR / bake would start publishing a health-only binary as if it were the product.
- One HTTP trust is a laptop + WAF fact, not a container fact.

When a Dockerfile appears (later program), it must `ENV ASPNETCORE_URLS=http://+:8081` (or listen 8081 in Kestrel config) and compose must map `8081:8081`, never `8080:8080`. Out of scope here.

`.dockerignore` is also absent; do not add it until a Dockerfile exists.

---

## 10. Concrete implementation sketch (classes / methods) — not applied code

The following is a **map for a later implementer**. It is not a patch. Names can change; responsibilities should not.

### 10.1 `OneOptions` (internal)

**File:** `src/Lazuar.Pay/One/OneOptions.cs`  
**Namespace:** `Lazuar.Pay.One`

| Member | Role |
|--------|------|
| `const string SectionName = "One"` | Bind section |
| `string BaseUrl` | Absolute http(s) URI including `/api/v1`, default `http://localhost:8080/api/v1` |
| `int TimeoutSeconds` | Default 5, range 1–60 |
| `static Uri ToBaseAddress(string baseUrl)` | Trim, ensure trailing slash, `UriKind.Absolute` |

Attributes: `[Required]` on BaseUrl, `[Range(1, 60)]` on TimeoutSeconds.

### 10.2 Upstream DTOs (internal)

**File:** `src/Lazuar.Pay/One/OneMe.cs`

Records with `[JsonPropertyName]` **or** a `JsonSerializerOptions` with `JsonNamingPolicy.SnakeCaseLower` used only inside `OneClient` (do not have to change global MVC JSON to deserialize One).

```
OneMe
  UserId: string
  Email: string?
  Name: string?
  Tenants: OneTenantSummary[]  (default empty array)
  IsPlatformAdmin: bool
  ActiveTenantId: string?
  ActiveRole: string?

OneTenantSummary
  Id: string
  Slug: string
  Name: string
  Role: string?
  Status: string?
  Permissions: string[]  (default empty; chrome hint)
```

These match One’s public `MeResponse` / `TenantSummary`. They are **not** EF entities. They are **not** sold as “One module contracts.” Pay may drop fields it does not want to sell (e.g. omit `permissions` on the door if you want a thinner whoami — then TypeSpec must match). Recommendation: **sell the same snapshot 011 listed** (`user_id`, email, `tenants[]` id/slug/name/role, `active_tenant_id`, `is_platform_admin`) plus `name` and `active_role` because One already has them. Include `permissions` as a hint or omit; if included, comment “never authorize from this.”

### 10.3 `OneResult<T>` (internal)

**File:** `src/Lazuar.Pay/One/OneResult.cs`

A closed set, not a library:

- `Ok(T Value)`
- `Upstream(int StatusCode)` — One answered with that status (body ignored)
- `Unreachable(string Reason)` — transport / timeout / parse failure (Reason for logs, not for clients)

No `IExceptionHandler`. No `Result.Success()`.

### 10.4 `OneClient` (internal)

**File:** `src/Lazuar.Pay/One/OneClient.cs`

```
internal sealed class OneClient
{
    public OneClient(HttpClient http, ILogger<OneClient> log);

    public Task<OneResult<OneMe>> GetMeAsync(
        string authorizationHeaderValue,  // full "Bearer …"
        string? tenantHint,               // X-Lazuar-Tenant-Id or null
        CancellationToken cancellationToken);
}
```

**Method body sketch:**

1. `new HttpRequestMessage(HttpMethod.Get, "me")` — relative to BaseAddress that ends with `/api/v1/`.
2. `TryAddWithoutValidation("Authorization", authorizationHeaderValue)` (tokens can be odd; `AuthenticationHeaderValue.Parse` throws on some `lzr_sk_` shapes if you are sloppy — forwarding the raw header is the trust model).
3. If tenantHint is non-empty, `X-Lazuar-Tenant-Id`.
4. `Accept: application/json` if not already on DefaultRequestHeaders.
5. `SendAsync(..., ResponseHeadersRead, ct)` then read content with a size cap (e.g. 256 KiB). `/me` is small; still cap.
6. If `HttpRequestException` or `TaskCanceledException`: log (no header values); return `Unreachable`.
7. If status is 200: `JsonSerializer.Deserialize<OneMe>`. Null or exception → `Unreachable` or a dedicated parse fail that the endpoint turns into 502.
8. Else: return `Upstream(statusCode)`. Do not deserialize error bodies.
9. **Never** log `authorizationHeaderValue`. Log `status`, `user_id` on success at Information.

Do not expose `HttpClient` as public. Do not implement `IDisposable` (factory owns the handler). Do not add `GetTenantsAsync` this slice.

No `partial class` for generated clients.

### 10.5 Sold DTO + endpoint

**File:** `src/Lazuar.Pay/Whoami/WhoamiResponse.cs`  
Public enough to serialize. Can be `internal` with STJ if the endpoint returns `Results.Json`.

Same fields as the door (snake_case on the wire). Mapping function `WhoamiResponse.From(OneMe)` — hand map, no AutoMapper.

**File:** `src/Lazuar.Pay/Whoami/WhoamiEndpoint.cs`

```
internal static class WhoamiEndpoint
{
    public static async Task<IResult> Handle(
        HttpContext http,
        OneClient one,
        CancellationToken cancellationToken);
}
```

**Handle sketch:**

1. Read `Authorization`. If missing/whitespace/`Bearer` without token: `TypedResults.Unauthorized()` or `Results.Problem(statusCode: 401, title: "Unauthorized")`. Return. No `one` call.
2. Read `X-Lazuar-Tenant-Id` (optional).
3. `var result = await one.GetMeAsync(...)`.
4. Switch:
   - `Ok me` → `TypedResults.Json(WhoamiResponse.From(me), jsonOptions)` 200
   - `Upstream 401` → 401
   - `Upstream 403` → 403
   - `Upstream 429` → 503 (optional)
   - `Upstream other` → 502
   - `Unreachable` → 503
5. Problem bodies: `{ "title": "...", "status": n }`. Do not include One’s URL or exception message (might contain the host). Log the reason server-side.

Minimal API will inject `OneClient` and `HttpContext` by convention. Register nothing extra.

Do **not** put `[Authorize]` on the endpoint. There is no auth scheme. The handler *is* the scheme.

### 10.6 `Program.cs` registration (order)

1. `CreateBuilder`
2. Options bind + validate + `ValidateOnStart`
3. `AddHttpClient<OneClient>(...)` setting `BaseAddress` and `Timeout` from `IOptions<OneOptions>`
4. `Build`
5. Map `/health`, `/v1/health`, `/v1/whoami`
6. `Run`
7. Keep `public partial class Program;`

No middleware. No `AddControllers`. No `AddMediatR`. No `AddAuthentication`.

JSON for the **sold** door: either

- `Results.Json(..., new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })`, or
- `[JsonPropertyName("user_id")]` on `WhoamiResponse`.

Do **not** switch the whole app to snake_case this slice unless you also re-check health (`status` stays `status`). Global `HttpJson` options in .NET 10 (`builder.Services.ConfigureHttpJsonOptions`) would apply to `TypedResults.Json` using defaults. Health uses anonymous `status` and would still serialize as `status`. **Safe to set global `SnakeCaseLower` now** if you want one policy for `/v1`. Document it in Program with a one-line comment: “Pay /v1 is snake_case, same as One’s public JSON.”

### 10.7 appsettings

**`appsettings.json`:** add:

```json
"One": {
  "BaseUrl": "http://localhost:8080/api/v1",
  "TimeoutSeconds": 5
}
```

Optional Kestrel URL pin to 8081 (see §7.3).

**`appsettings.Development.json`:** no secrets. May omit One (inherit).

Do not add `One` to user-secrets.

### 10.8 TypeSpec (`packages/pay-spec/main.tsp`) — same slice as the door

Today:

- `GET /v1/health` → `HealthResponse { status: string }`
- server `http://localhost:8081`

Add (sketch):

```
model WhoamiTenant {
  id: string;
  slug: string;
  name: string;
  role?: string;
}

model WhoamiResponse {
  user_id: string;
  email?: string;
  name?: string;
  tenants: WhoamiTenant[];
  is_platform_admin: boolean;
  active_tenant_id?: string;
  active_role?: string;
}

@route("/v1")
@tag("Identity")
interface Whoami {
  @get
  @route("/whoami")
  @doc("Caller snapshot from One GET /me. Forwards Authorization. Does not hammer One from other routes.")
  me(): WhoamiResponse | UnauthorizedResponse | BadGateway-ish;
}
```

Honesty: TypeSpec error unions are optional this slice; 200 shape is not. `task pay:spec` must compile. Do **not** import One’s spec package. Duplicate the *thin* sold shape. Pay owns this document.

Do not generate C# from this spec this slice. The host stays hand-written. A later honesty script can diff paths; not required to insert HttpClient.

### 10.9 Tests to add (files)

| File | Contents |
|------|----------|
| `tests/Lazuar.Pay.Tests/FakeOneHandler.cs` | `HttpMessageHandler` double |
| `tests/Lazuar.Pay.Tests/PayWebApplicationFactory.cs` | WAF + handler hook + One settings |
| `tests/Lazuar.Pay.Tests/WhoamiTests.cs` | matrix in §6.5 |
| `tests/Lazuar.Pay.Tests/HealthTests.cs` | keep; optionally factory + “does not call One” |
| `tests/Lazuar.Pay.Tests/IsolationTests.cs` | widen scan §6.7 |

No `WhoamiLiveTests.cs` unless env-gated and default skip.

### 10.10 IsolationTests method sketch

Keep `Host_csproj_does_not_reference_the_old_api`.

Add `Focused_pay_source_does_not_name_the_old_tree`:

- `FindPayRoot()` = directory that contains `src/Lazuar.Pay/Lazuar.Pay.csproj` (same walk as today).
- Enumerate `*.csproj`, `*.slnx`, `src/Lazuar.Pay/**/*.cs` excluding `bin`/`obj`.
- For each file, `File.ReadAllText`, assert does not contain the banned tokens.

Banned tokens stay the existing five. Optionally add `IRequestHandler`, `AddMediatR` on `.cs` files only (csproj would not contain those).

### 10.11 README / Taskfile copy (when implementing)

When code lands (not in this paper’s job):

- README: add `GET /v1/whoami` next to health; curl example; `One:BaseUrl` env; remind 8081 listen / 8080 outbound.
- `pay:test` description: “health + isolation + whoami (fake One)”.
- Do not add `pay:dev` flags.

### 10.12 Sequence (implementer order, still not this paper applying it)

1. `OneOptions` + appsettings default (health still passes).
2. `OneClient` + unit tests with `HttpClient(handler)` (no WAF).
3. `AddHttpClient` in Program (health still passes; client unused).
4. `WhoamiEndpoint` + `MapGet` + WAF tests.
5. Widen IsolationTests.
6. TypeSpec whoami + `task pay:spec`.
7. Manual curl if One is up. Do not block on it.

If step 4 is done before 2, that is fine — WAF tests can cover the client. Step 1 **must** precede ValidateOnStart.

### 10.13 Lines of code budget (smell test)

If the slice exceeds roughly:

- `OneClient` > ~150 lines
- `WhoamiEndpoint` > ~80 lines
- Host `Program.cs` > ~80 lines
- A new csproj
- A new NuGet on the host

…it has picked up gravity. Stop and delete, do not “organize.”

---

## 11. Out of scope (do not sneak in on the whoami PR)

- OIDC code + PKCE, Pay `client_id`, redirect allowlist (NP-ONE-001/002/004).
- `POST /tenants`, invites, `lzr_sk_` mint UI (NP-ONE-009…014).
- `authz/check` on merchant admin routes (NP-ONE-015).
- HMAC webhooks from One (NP-ONE-017).
- Pay database, org table, “sync members.”
- JwtBearer against `:8085`.
- CORS for a Pay SPA.
- Dockerfile, compose, mprocs, GHCR.
- Adding this host to the historical solution.
- MediatR, module folders, BuildingBlocks.
- Retry policies, OpenTelemetry, Serilog.
- Generating C# from One OpenAPI or from pay-spec.
- Buyer checkout, Stripe, receipts.
- Changing `/health` to call One ready.
- Port 8080 listen.
- Referencing or copying source from the historical API app.

---

## 12. Binding rules for the later implementer

1. **One host project. `One/` folder of plain types + `HttpClient`. Not a module.**
2. **`GET /v1/whoami` is endpoint-only forwarding of the caller’s Bearer to One `GET /me`. No One-calling middleware.**
3. **`OneOptions`: `BaseUrl` (absolute, includes `/api/v1`), `TimeoutSeconds`. Nothing else.**
4. **Tests: `WebApplicationFactory` + fake `HttpMessageHandler`. No live Zitadel. Live One curl is optional.**
5. **Listen 8081. Dial 8080 (One). Never bind 8080.**
6. **Do not add the host to the historical `Lazuar.slnx`.**
7. **No Dockerfile.**
8. **IsolationTests stays, and must keep forbidding `lazuar-api` / `Modules.` / `BuildingBlocks` / `MediatR` / `Lazuar.Api` in this host’s own files. Widen to tests csproj and `*.cs`.**
9. **No MediatR. No `IRequest`. No `Modules/One` copy. No second org table.**
10. **Do not attach a configured API key to the whoami client. The principal is the caller.**
11. **Do not retry `/me`. Do not hammer `/me` from other routes.**
12. **Do not parse Zitadel role claims. Whoami’s body is whatever One `/me` said, mapped.**
13. **Shared framework only on the host csproj this slice (still zero `PackageReference`).**
14. **`public partial class Program;` and `InternalsVisibleTo` stay.**
15. **C# gravity is the defect to optimize against, not missing abstractions.**

---

## 13. Inventory of seams (index)

| Seam | Location | Insert |
|------|----------|--------|
| DI / HttpClient | `Program.cs` before `Build` | `AddOptions<OneOptions>`, `AddHttpClient<OneClient>` |
| Config | `appsettings.json` | `One:BaseUrl`, `One:TimeoutSeconds` |
| Env | process env | `One__BaseUrl`, `One__TimeoutSeconds` |
| Sold door | `Program.cs` maps | `GET /v1/whoami` → `WhoamiEndpoint.Handle` |
| Upstream call | `One/OneClient.GetMeAsync` | `GET {BaseUrl}/me` with forwarded Authorization |
| JSON map | `WhoamiResponse.From(OneMe)` | snake_case door |
| Listen | `launchSettings.json` (and optional Kestrel section) | **8081 only** |
| Outbound One | options default | `http://localhost:8080/api/v1` (dial, not listen) |
| Test transport | `FakeOneHandler` + `PayWebApplicationFactory` | replace primary handler named `OneClient` |
| Isolation | `IsolationTests` | csproj + src `*.cs` + slnx strings |
| Contract | `packages/pay-spec/main.tsp` | `GET /v1/whoami` |
| Workspace | `package.json` / `pay:*` | no new scripts required; test description may widen |
| Historical solution | `Lazuar.slnx` | **do not touch** |
| Docker | (none) | **do not add** |
| Middleware pipeline | `Program.cs` after `Build` | **leave empty** |

That is the whole insertion. The host is nine lines plus two tests plus a string ban. The first One trust should look like **more of that**, not like a module.
