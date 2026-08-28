# 06 — Host production seams (compose, images, config, health, rate limits, observability, TLS, data)

**Type:** Uncondensed evaluation of the **live** focused Pay process and its deploy shape on this SHA. **Not** an implementation. **Not** a patch. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md). **Not** money semantics (occupancy, fulfill, refunds, disputes, subscriptions — that is [07](./07-money-remaining.md)). **Not** public HTTP API shape, errors, idempotency, versioning as a stranger kernel (that is [01](./01-public-http-api.md)). **Not** M2M / `lzr_sk_` (02), outbound `payment.completed` (03), Plane A/B webhook *product* dialect (04), MemberGate/writer tenancy (05), SPA-vs-headless (08), pay-spec honesty as a docs paper (09), or the ranked production-ready bar (10). Those papers may be named when a host seam *is* the evidence; they are not rewritten here.

Live files on this SHA are authority. [013-prods/03-host-production-seams.md](../013-prods/03-host-production-seams.md) (SHA `6f866ff0`, no Dockerfile, in-memory checkout) and [019-evals/01-pay-host-seams.md](../019-evals/01-pay-host-seams.md) (SHA `9f04ad58`, CORS hardcoded, no Pay image) are background. Where they disagree with this tree, this tree wins; the disagreement is named with evidence. Issue [080](../../issues/002/080-p1-cors-and-compose-still-laptop-shaped-no-pay-image.md) is **status: resolved** in the 002 tracker. This paper asks whether that resolution made a **production process**, or only a laptop-shaped image of one.

Standing law this slice must not weaken:

- One Pay binary, one Pay database. Bezos is the **door** (`/v1`); Linux is the **room** (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path.
- Steal HTTP **judgment** from Hub; Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`).
- **Refuse:** retarget root `docker-compose.yml` (or `deploy/prod/docker-compose.yml`, or `docker-compose.ghcr.yml`) onto 8081. Do not set ops `:3003` or portal `:3004` `VITE_API_URL` to Pay.

---

## Coordinates

| Field | Value |
|-------|--------|
| Title | Host production seams — compose, images, config, health, rate limits, observability, TLS, data |
| Date | 2026-08-28 |
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `fix/002-pay-host-bugs` (`.git/HEAD` → `refs/heads/fix/002-pay-host-bugs`) |
| HEAD | `6d730d155c871465c35c192cf7730bfd270b47fa` (`6d730d15`) |
| HEAD subject | `fix(pay): store per-org One webhook secrets` |
| Author / committer time | Akmal Firdaus, 2026-08-28 09:29:02 +0800 |
| Host | `apps/lazuar-pay/src/Lazuar.Pay` (`net10.0`, listen `http://localhost:8081` when launchSettings or `ASPNETCORE_URLS=http://+:8081` applies) |
| Tests | `apps/lazuar-pay/tests/Lazuar.Pay.Tests` |
| Merchant | `apps/lazuar-pay-merchant` Vite **5178** |
| Checkout | `apps/lazuar-pay-checkout` Vite **5179** |
| Type | Analysis. How to solve is analysis, not a patch. |
| Dirty tree at write | `?? plans/020-evals/` only (this program). No host source edits in the working tree. |

Binding this host already lives under, not flipped here:

- New Pay is Consumer-0 of lazuar-one. Do not rebuild `Modules/One`. Do not add a project reference into `apps/lazuar-api`.
- IsolationTests stay red on cathedral strings.
- Steal HTTP **judgment** from Hub; Hub is museum. This paper does not re-judge Stripe/CHIP/Billplz/Xendit/Razorpay HTTP.
- Receipt ≠ tax invoice. SST/LHDN stay off the pay path.
- Buyers are not One humans. `POST /v1/pay/{token}/start` has no Bearer.

002 **host** commits that this paper re-reads as live files, not as a git-log paraphrase: CORS from `Pay:CorsOrigins`, Development `MigrateAsync` try/catch, Testing skips Npgsql, Pay Dockerfiles, `docker-compose.pay.yml --profile apps`, `docker-bake.hcl` group `pay`, `PublicPayLimiter` + `Pay:StartMaxPerMinute`, unversioned `/ready` test, WrapKey 503 on PUT, `.env.example` Testing-only Stripe fallback, per-org One `whsec_`. Those subjects are coordinates. Every claim below is from files opened on `6d730d15`.

---

## 0. Verdict (read this before the inventory)

**The focused host is a laptop-shaped production *candidate*, not a production process.** 080 is honestly closed as the issue was written: there **is** a Pay image on 8081, there **are** merchant/checkout images, there **is** a bake group `pay`, CORS **is** config, empty Production/Staging CORS **fails boot**, root compose **is** still Hub museum and must stay that way. None of that is the same sentence as “a stranger can pay at an HTTPS URL that an operator can restore from backup, rotate a wrap key, see a metric, and health-gate on Postgres.”

What 002 actually shipped for *host*:

| Seam | Live on `6d730d15` | Enough for production of first-party dogfood? |
|------|--------------------|-----------------------------------------------|
| Listen 8081 | launchSettings + Dockerfile `ASPNETCORE_URLS=http://+:8081` | Yes, as a bind. |
| One Pay DB | `PayDbContext`, schema `public`, Postgres 16 on 5435 | Yes as *shape*. No as *durability* (compose has no volume). |
| Images | three Dockerfiles + bake group `pay` | Yes as *existence*. No as *CD* (GHCR workflow is Hub-only). |
| CORS | `PayCors.Resolve` + fail-boot outside Dev/Testing | Yes as *code*. Compose default is still laptop `:5178/:5179`. |
| WrapKey | required outside Testing; PUT maps 503 | Fail-closed on first vault write. **Not** fail-boot. |
| Health | `/health` + `/v1/health` liveness, never One | Yes as liveness. Dockerfile healthcheck uses **this**, not ready. |
| Ready | `/ready` calls `CanConnectAsync` and **ignores the bool** | **No.** Postgres-down can still 200. |
| Org ready | `/v1/orgs/{id}/ready` is a **member money door**, not a probe | Different door. Do not health-gate on it. |
| Rate limit | in-process start limiter, default **20**/min/token | Enough for a one-person link. Not enough for an event. Not enough for replicas. Absent on webhook/mint/whoami. |
| Observability | default Microsoft logger; one `LogError` on migrate | **Absent** as a production kit. No OTel, no metrics, no request log, no trace. |
| TLS / HSTS / forwarded headers | none in the Pay process | Correct **if** a reverse proxy exists. **No Pay Caddyfile exists.** Hub Caddy is Hub. |
| Migrations | Development auto-migrate (caught); Production does not | Production must run `task pay:db:migrate` (or equivalent) **out of band**. No runbook in compose. |
| Backup / wrap rotation | none | **Absent.** Ciphertexts are single-key AES-GCM. Volume is anonymous/none. |
| CI | `pay` job: `dotnet test` + Vite **build** + honesty | Host tests yes. Merchant/checkout **vitest not run**. No image bake. No compose smoke. |

**Stay on one host project.** Do not copy Hub’s Serilog + Azure Key Vault + nine EF migrations at boot + `Jwt:Secret` + `AddLazuarMediatR` + `/health/ready` that asks a metrics collector about outbox lag. Pay’s production kit, when it appears, is still: listen 8081 behind a **Pay** gateway, env for `One__BaseUrl` + one connection string + WrapKey + CORS + CheckoutBaseUrl + PublicBaseUrl, console logs that never print Bearer or wrap keys, a ready probe that is **Postgres only** (never One), CORS from config that still refuses ops/portal/admin.

**080’s suggested fix was done in letter and not in spirit.** The letter: “Add a Pay image after 001–006 money. Config CORS (049) and `VITE_*` (041, 050) in the same slice as the image. Keep Hub compose as museum until cutover (refuse).” Images exist. CORS is config. `VITE_PAY_API_URL` is a Dockerfile ARG that **fails the checkout build if empty**. Hub compose is still museum. The spirit: “we shipped Pay” still cannot mean a URL a stranger can pay, because compose `--profile apps` defaults `ASPNETCORE_ENVIRONMENT=Development`, laptop CORS, `postgres/postgres`, `http://localhost:5179`, empty WrapKey, `host.docker.internal:8080`, no named volume, no TLS, no GHCR push of `lazuar-pay*`.

**How this paper splits holes.** *Host-production holes* are things an operator of **this** first-party stack (One + Pay merchant + Pay checkout) hits before a second app exists: images, env, health, backups, TLS at the edge, rate limits, logs, CI. *Integrator holes* are things a stranger product needs that this host does not pretend to be: M2M Bearer, outbound webhooks, a versioned `/v1` they can pin, a sample outside this repo. This slice ranks the first set. It names the second set only to keep them off the host punch-list. Do not “fix production” by building a second-app SDK.

---

## 1. Method / SHAs

### 1.1 Binding coordinates

See the table above. If this file is read after later commits, treat `6d730d15` as the **analysis baseline**. Re-inventory `apps/lazuar-pay`, the three Dockerfiles, `docker-compose.pay.yml`, `docker-bake.hcl`, `.github/workflows/*`, and `.env.example` before implementing. Do not assume `Program.cs` is still 89 lines.

### 1.2 What was actually opened (this write-up)

**Focused host (source of truth for “what the process is”):**

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` (entire)
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj` (entire)
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`, `PayCors.cs`, `PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`, `PublicPayLimiter.cs`, `CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`, `WhoamiEndpoints.cs`, `Client/OneClient.cs`, `Client/OneOptions.cs`, `Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` (dispatch only — not rail parse)
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` (WrapKey 503 + last4)
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`, `CheckoutStore.cs` (existence; not money)
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`, `PaymentLinkOccupancy.cs` (in-process gate — replica seam)
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`, `Money/Queries/PaymentQueryEndpoints.cs` (Map* list)
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`, `Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/*` (file list + snapshot + `OrgOneWebhookCiphertext` + missing Designers)
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` (`Pay:PublicBaseUrl` https-not-loopback)
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeWebhook.cs` (`Pay:StripeWebhookSecret` Testing-only)
- `apps/lazuar-pay/.env.example`, `README.md`, `package.json`, `global.json`, `docker-compose.pay.yml`, `Dockerfile`

**Images / bake / Hub museum:**

- `apps/lazuar-pay-merchant/Dockerfile`, `package.json`, `vitest.config.ts`, `src/lib/payApi.ts`, `src/lib/checkoutOrigin.ts`, `src/auth/oidcConfig.ts`, `README.md`
- `apps/lazuar-pay-checkout/Dockerfile`, `package.json`, `vitest.config.ts`, `src/pay.ts`, `README.md`
- `docker-bake.hcl` (groups `default` and `pay`, Hub labels on Pay targets)
- repo-root `docker-compose.yml` (Hub museum comment + 8080)
- `docker-compose.ghcr.yml` (opening; Hub)
- `docker-compose.dev-proxy.yml` is Hub Caddy :9080 (opened via `deploy/dev/Caddyfile`)
- `deploy/prod/docker-compose.yml`, `deploy/prod/Caddyfile`, `deploy/prod/env.example`
- `deploy/dev/Caddyfile`
- `mprocs-dev.yaml` (Hub frontends only)

**Tests / CI / spec (this slice):**

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`, `PayPostgres.cs`
- `Hosting/HealthTests.cs`, `Hosting/CorsTests.cs`, `Secrets/SecretBoxTests.cs`
- `PaymentLinks/PaymentLinkTests.cs` (`Start_rate_limit_is_429`)
- `IsolationTests.cs`
- `Lazuar.Pay.Tests.csproj` (InMemory + Testcontainers.PostgreSql)
- `Taskfile.yml` `pay:*` block
- `.github/workflows/ci.yml` (`pay` job + Hub `dotnet` / `contracts`)
- `.github/workflows/ghcr.yml` (Hub matrix; no pay)
- `scripts/check-pay-openapi-honesty.mjs` (IMPL_ONLY `/health` `/ready`)
- `packages/pay-spec/main.tsp` (`@server("http://localhost:8081")`)
- `turbo.json` (generic `test` task; CI does not use it for Vite)

**002 / background papers (not authority):**

- `issues/002/080-p1-cors-and-compose-still-laptop-shaped-no-pay-image.md`
- `issues/002/049-p1-cors-allow-list-is-laptop-only.md`
- `issues/002/017-p1-development-wrapkey-docs-lie-vault-put-500.md`
- `issues/002/018-p1-connection-string-password-keep-replaces-whole-cs.md`
- `issues/002/021-p1-development-migrateasync-cors-health-boot-real-db.md`
- `issues/002/024-p1-env-example-advertises-dev-process-whsec-fallback.md`
- `issues/002/076-p2-unversioned-ready-mapped-and-untested.md`
- `issues/002/README.md` (001–080 resolved on this branch)
- `plans/013-prods/03-host-production-seams.md` (opening + absent-table at `6f866ff0`)
- `plans/019-evals/01-pay-host-seams.md` (opening + B5/B9/G4 quotes)
- `plans/020-evals/README.md`

**Git coordinates only:**

- `git rev-parse HEAD` → `6d730d155c871465c35c192cf7730bfd270b47fa`
- `git branch --show-current` → `fix/002-pay-host-bugs`
- `git status --short` → `?? plans/020-evals/`

**Not opened as an implementation source:** Hub `Modules/**` handlers, Hub `Composition/HealthEndpointExtensions.cs` beyond the 013 quote, Hub JwtService, One’s production IdP. Named only to forbid copying, or to contrast Hub’s *existing* production kit (GHCR + Caddy + Neon env.example) with Pay’s missing one.

### 1.3 Method

1. Inventory the focused host as it actually compiles at `6d730d15` (Program.cs, DI, maps, config, tests).
2. Inventory the **new** image/compose/bake story 080 asked for, against the **still-live** Hub museum compose, GHCR workflow, and `deploy/prod`.
3. For each production seam (listen, migrate, CORS, health, ready, wrap, rate limit, logs, metrics, CI, migrations, TLS, data), quote the live file and say whether 002 closed the 019 hole, papered it, or left it.
4. Split remaining work into **host-production holes** vs **integrator holes**. Rank the host list. Refuse Hub retarget.
5. Do not implement. Names in sketches can change; responsibilities should not.

### 1.4 What this paper is allowed to decide vs defer

| Decide here (process) | Defer |
|------------------------|--------|
| Whether compose/images/bake are production or laptop | Exact `/v1` error/idempotency shape (01) |
| Whether CORS/WrapKey/CS fail-boot or fail-first-call | M2M Bearer / `lzr_sk_` (02) |
| Whether `/ready` is a real Postgres probe | Outbound `payment.completed` (03) |
| Whether StartMaxPerMinute 20 is enough | Plane A/B HMAC dialect completeness (04) |
| Whether OTel/metrics exist | MemberGate writer overlay (05) |
| Whether CI runs vitest / bakes pay | Occupancy/fulfill leftover (07) |
| Whether TLS belongs in the process or at Caddy | Merchant/checkout as `/v1` clients (08) |
| Whether wrap rotation / backup exist | pay-spec honesty as a docs paper (09) |
| Refuse Hub compose → 8081 | Ranked production-ready bar (10) — this file **feeds** 10, does not write it |

---

## 2. `Program.cs`: listen, `MigrateAsync` Development-only, CORS, `Map*` list, Testing skips Npgsql

The composition root is 89 lines of statements plus `public partial class Program;`. Quoted in full because this file *is* the process:

```csharp
using System.Text.Json;
using Lazuar.Pay.Catalog;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Credentials;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Identity.OneWebhooks;
using Lazuar.Pay.Money;
using Lazuar.Pay.Money.Queries;
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
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
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<TestHosted>();
builder.Services.AddScoped<Fulfillment>();
builder.Services.AddScoped<IFulfillPaid>(sp => sp.GetRequiredService<Fulfillment>());
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay");
    if (string.IsNullOrWhiteSpace(payCs))
    {
        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }

    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
PayCors.Add(builder);
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "pay-db schema mismatch; run task pay:db:migrate");
    }
}

app.UseCors();

app.MapHealth();
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapPaymentLinks();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();

app.Run();

public partial class Program;
```

Read this as the production composition root it will remain. Everything a production kit adds later (fail-boot for WrapKey, a real ready bool, forwarded headers at the *edge not here*, a logger that never prints Bearer) hangs **here**, in the open, not inside `AddPayModules()`.

### 2.1 Listen is not in `Program.cs`

`app.Run()` has no URL argument. Listen URL is:

| Path | Value |
|------|--------|
| `Properties/launchSettings.json` profile `http` | `"applicationUrl": "http://localhost:8081"`, `ASPNETCORE_ENVIRONMENT=Development` |
| `apps/lazuar-pay/Dockerfile` | `EXPOSE 8081`, `ENV ASPNETCORE_URLS=http://+:8081`, `ENV ASPNETCORE_ENVIRONMENT=Production` |
| `docker-compose.pay.yml` service `pay` | `"8081:8081"`, **overrides** environment to `${ASPNETCORE_ENVIRONMENT:-Development}` |

Kestrel in a container without `ASPNETCORE_URLS` would bind 8080 (ASP.NET default). The Dockerfile **does** set 8081. That is the 013 lock (“8081 never 8080”) held in the image. Compose publishes 8081:8081. `task pay:dev` uses launchSettings via `dotnet watch run`. There is no HTTPS profile. There is no `applicationUrl` with `https://`.

`packages/pay-spec/main.tsp` still says `@server("http://localhost:8081", "Local focused Pay host")`. That is honest for dogfood. It is not a production server URL.

### 2.2 Testing skips Npgsql (021 closed as written)

```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay");
    if (string.IsNullOrWhiteSpace(payCs))
    {
        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }

    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
```

`PayApiFactory` then `UseEnvironment(EnvironmentName)` default `"Testing"`, strips any leftover `PayDbContext`, and registers InMemory (or Npgsql when `PostgresConnection` is set):

```csharp
builder.UseEnvironment(EnvironmentName);
// ...
if (!string.IsNullOrWhiteSpace(PostgresConnection))
{
    var cs = PostgresConnection;
    services.AddDbContext<PayDbContext>(o => o.UseNpgsql(cs));
}
else
{
    services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
}
```

`CorsTests` and `HealthTests` now go through `PayApiFactory`, not raw `WebApplicationFactory<Program>()`. That is the 021 suggested fix, live. CI `pay` job has **no** Postgres service. Hermetic tests do not need 5435. `PayPostgres` / Testcontainers is a *separate* path: `Assert.Ignore` if Docker cannot start `postgres:16-alpine`. InMemory is still “not a transaction proof” (factory comment; 077 lives in 07).

**Remaining hole:** Production and Staging **also** take the laptop connection-string fallback when `ConnectionStrings:Pay` is empty. `appsettings.json` has **no** `ConnectionStrings` section. `appsettings.Development.json` has `Host=localhost;Port=5435;…Password=postgres`. A Production image started without `ConnectionStrings__Pay` silently aims at laptop Postgres with user `postgres` password `postgres`. 018’s *substring* bug (`Password=` missing → replace the whole CS, including `Host`) is gone: a **non-empty** CS is used as-is. The *empty* CS path is still a hardcoded secret in the binary. That is a host-production hole, not a 018 regression.

Issue 018 frontmatter says `status: resolved`; the body still says `open` and still describes the old substring check. Live `Program.cs` matches the **suggested fix** (“If CS is non-empty, use it. Laptop default only when CS is null/whitespace”) and does **not** match the body’s “or does not contain `Password=`”. Live files win. The leftover hole is “Production should not have a laptop default at all.”

### 2.3 `MigrateAsync` is Development-only, caught, logged

```csharp
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "pay-db schema mismatch; run task pay:db:migrate");
    }
}
```

019 B9: no try/catch; Cors/Health booted Development+Npgsql and migrated the dogfood DB. Live: catch + log; tests are Testing. **021 is closed as a crash and as a test-pollution bug.**

What 021 did **not** close:

1. **Development with Postgres down still “starts.”** The host logs and serves. `/health` is 200. `/ready` is supposed to be the gate; see §5 for why it is not.
2. **The log line can contain the connection string.** `LogError(ex, …)` dumps the exception. Npgsql failures often include the CS, which includes `Password=postgres` on the laptop default. That is a secrets-in-logs hole on the only log statement in the host (see §7).
3. **Production never migrates.** Dockerfile `ASPNETCORE_ENVIRONMENT=Production`. Compose default **overrides to Development**, so `docker compose --profile apps up` **will** auto-migrate. An operator who sets Production (the image’s own default) must run `task pay:db:migrate` / `dotnet ef database update` **before** traffic. There is no migrate init container, no entrypoint wrapper, no compose `command` that migrates. A new replica of a Production image against an old schema fails at the first query, not at boot.
4. **Caught migrate failure does not fail boot.** A drifted Development DB logs and keeps going. That is friendlier than 021’s crash and worse than a clear exit code for `task pay:dev` in CI-like scripts.

`task pay:db:migrate` is:

```yaml
pay:db:migrate:
  desc: Apply PayDbContext migrations (one context)
  dir: apps/lazuar-pay
  cmds:
    - dotnet ef database update --project src/Lazuar.Pay/Lazuar.Pay.csproj --context PayDbContext
```

One context. Not `MigrateAllModuleDatabasesAsync`. Keep that.

`task pay:db:up` is `docker compose -f docker-compose.pay.yml up -d` **without** `--profile apps`. It starts **Postgres only**. That is the right laptop split (DB in Docker, `dotnet watch` on 8081). It is also why operators who read “compose is Pay” and run `pay:db:up` never get 8081.

### 2.4 CORS is no longer in `Program.cs` (049/080 letter)

`PayCors.Add(builder)` is the only CORS registration. Live `PayCors.cs`:

```csharp
internal static class PayCors
{
    public const string Key = "Pay:CorsOrigins";

    public static readonly string[] DevelopmentOrigins =
    [
        "http://localhost:5178",
        "http://127.0.0.1:5178",
        "http://localhost:5179",
        "http://127.0.0.1:5179",
        "http://localhost:4178",
        "http://127.0.0.1:4178",
        "http://localhost:4179",
        "http://127.0.0.1:4179"
    ];

    public static void Add(WebApplicationBuilder builder)
    {
        var origins = Resolve(builder.Configuration[Key], builder.Environment.EnvironmentName);
        builder.Services.AddCors(o =>
        {
            o.AddDefaultPolicy(p =>
                p.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
    }

    public static string[] Resolve(string? raw, string environmentName)
    {
        if (TryParse(raw, out var origins))
        {
            return origins;
        }

        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return DevelopmentOrigins;
        }

        throw new InvalidOperationException("Pay:CorsOrigins must be configured in Production and Staging.");
    }
}
```

Locks that are live and must stay:

- Never `AllowAnyOrigin`.
- Never `AllowCredentials` (Pay merchant/checkout send `Authorization: Bearer`, and `payApi.ts` documents “credentials omitted on purpose: localhost cookies are not port-scoped”).
- Ops `:3003` and portal `:3004` are **not** in `DevelopmentOrigins`. `CorsTests` still deny them on `/health` and on public-pay OPTIONS.
- Configured list **replaces** the laptop list (`Configured_origins_replace_laptop_list`).
- Empty Production/Staging **throws at boot** (`Empty_cors_in_production_fails_boot`).
- Public pay GET/POST/OPTIONS now have CORS tests (066 closed as written).

Locks that are **not** production:

- Development/Testing empty → eight laptop HTTP origins including Vite **preview** 4178/4179. Preview is a laptop convenience. It is not an HTTPS origin.
- Compose default `Pay__CorsOrigins` is the four 5178/5179 localhost URLs (not even 4178/4179). See §3.
- There is still no `appsettings.Production.json`. Production CORS is **env or boot-fail**. That is the right shape. Compose does not demonstrate it.

Pipeline after `Build`: `UseCors()` only. No `UseAuthentication`. No `UseAuthorization`. No `UseExceptionHandler`. No `UseHttpsRedirection`. No `UseHsts`. No `UseForwardedHeaders`. No `UseRateLimiter` (the start limiter is a hand-rolled static dictionary, not ASP.NET rate limiting). No custom One-calling middleware. Whoami remains an **endpoint**. Member checks remain **function calls**. That is still the Linux shape. Do not “upgrade” CORS to a Hub `AuthAndCorsExtensions` copy.

### 2.5 `Map*` list (process surface)

These are the live doors the host maps. Honesty (`scripts/check-pay-openapi-honesty.mjs`) treats unversioned `GET /health` and `GET /ready` as `IMPL_ONLY`. `/v1/health` is in the spec.

| Extension | Routes | Auth | Production note |
|-----------|--------|------|-----------------|
| `MapHealth` | `GET /health`, `GET /v1/health`, `GET /ready` | none | Liveness vs ready vs **org** ready. See §5. |
| `MapWhoami` | `GET /v1/whoami` | Bearer → One `/me` | No rate limit. Amplifier to One. |
| `MapOrgReady` | `GET /v1/orgs/{orgId}/ready` | member | Money door, not a probe. |
| `MapCheckouts` | `POST /v1/checkouts`, `GET /v1/checkouts/{id}`, `GET /v1/orgs/{orgId}/checkouts` | writer / member | Mint has no rate limit. |
| `MapPaymentLinks` | `POST /v1/payment-links`, `GET /v1/orgs/{orgId}/payment-links` | writer / member | Same. |
| `MapCatalog` | `POST /v1/orgs/{orgId}/products`, `GET /v1/orgs/{orgId}/products` | writer / member | Same. |
| `MapPublicPay` | `GET /v1/pay/{token}`, `POST /v1/pay/{token}/start` | **none** | Start is the only rate-limited door. |
| `MapGateways` | `PUT/GET /v1/orgs/{orgId}/gateway`, `GET /v1/orgs/{orgId}/gateways` | writer / member | WrapKey 503 on PUT. `last4` is returned. |
| `MapWebhooks` | `POST /v1/webhooks/{provider}/{orgId}` | PSP HMAC (not Bearer) | No rate limit. Public on the internet if `PublicBaseUrl` is. |
| `MapPaymentQueries` | `GET /v1/orgs/{orgId}/payments`, `…/receipts`, `…/receipts/{id}` | member | Read. |
| `MapOneWebhooks` | `POST /v1/one/webhooks`, `PUT/GET /v1/orgs/{orgId}/one-webhook` | HMAC / writer / member | Process `Pay:OneWebhookSecret` fallback. No rate limit on POST. |

JSON for `/v1` is snake_case globally via `ConfigureHttpJsonOptions`. Health’s `{ status = "ok" }` serializes as `{"status":"ok"}`. `PayErrors` (`status`, `title`, `detail`) ride the same policy. `OneClient.Json` duplicates the policy for One DTOs and for `Results.Json(..., OneClient.Json)`. Two option objects, one convention. Do not introduce camelCase.

`public partial class Program;` stays. `WebApplicationFactory<Program>` and `InternalsVisibleTo` depend on it.

### 2.6 DI that is production-relevant (and one that is dead)

- `AddOptions<OneOptions>().BindConfiguration("One")` — **no** `ValidateOnStart`. Empty/missing `One:BaseUrl` falls through to `OneClient` constructor default `http://localhost:8080/api/v1`.
- Typed `AddHttpClient<OneClient>()` plus named clients `chip` / `billplz` / `xendit` / `razorpay`. No timeout on the named PSP clients beyond HttpClient defaults. One timeout is `OneOptions.TimeoutSeconds` default 5.
- `AddDataProtection()` with **no** key-ring persistence, no Redis, no file-system path, no certificate. **Nothing in the host calls `IDataProtectionProvider`.** `SecretBox` is hand-rolled AES-GCM with `Pay:WrapKey`. DataProtection is a dead registration. If someone later “uses DataProtection for cookies,” keys vanish on container recycle. Either persist a key ring **or delete the call**. Do not copy Hub’s Key Vault.
- `AddSingleton<SecretBox>()` — wrap key loaded from config on each `Protect`/`Unprotect` (`LoadKey` reads `IConfiguration` every time; a rotated env var would take effect without recycle). There is still no dual-key unwrap. See §10.
- Rails are **explicit** `AddScoped<StripeHosted>()` etc. IsolationTests ban `IEnumerable<IHostedRail>`. Keep the switch in `PublicPayEndpoints` / `WebhookEndpoints`. Do not “clean up” into a factory catalog.
- `CheckoutStore` is **scoped** and Postgres-backed (`/// Postgres-backed checkouts. Not a ledger.`). 013’s in-memory singleton is gone. That is a host win 013 asked for. Durability still depends on the volume that compose does not declare.

### 2.7 What `Program.cs` still refuses (keep refusing)

- No MediatR, no `AddAllModules`, no `ProjectReference` into `apps/lazuar-api`.
- No `UseAuthentication` / JwtBearer. Bearer is forwarded to One on the doors that need a human. Public pay and Plane B have no Bearer. That is correct. M2M is 02, not a host JwtBearer.
- No Serilog, no OpenTelemetry packages on `Lazuar.Pay.csproj`. Three PackageReferences only: `Microsoft.EntityFrameworkCore.Design` (PrivateAssets), `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0, `Stripe.net` 48.0.0.
- No `Directory.Build.props` under Pay. Hub’s props live in `apps/lazuar-api/` and do not apply sideways. Still true, still required.

---

## 3. `docker-compose.pay.yml`, Dockerfiles, `docker-bake.hcl` group `pay`, root compose Hub museum, issue 080, remaining laptop-shaped defaults

### 3.1 Issue 080 as written vs live

080 extracted from 019 P1-10 / P1-14:

> Production-shaped holes that are not 049’s allow-list code:
> - Root `docker-compose.yml` still points at `apps/lazuar-api` (Hub). Pay has `apps/lazuar-pay/docker-compose.pay.yml` for Postgres 5435; there is **no** Pay Dockerfile / bake target for 8081 + two Vite apps.
> - CORS is hardcoded laptop origins (049).
> - Preview origins 4178/4179 were added; still no production origin config.
> - Merchant/checkout have no production env story (`VITE_*` 041, 050).

Suggested fix:

> Do not retarget ops `:3003` or portal `:3004`. Add a Pay image **after** 001–006 money. Config CORS (049) and `VITE_*` (041, 050) in the same slice as the image. Keep Hub compose as museum until cutover (refuse).

Live on `6d730d15`:

| 080 claim | Live | Closed? |
|-----------|------|---------|
| No Pay Dockerfile | `apps/lazuar-pay/Dockerfile` exists, 8081, `USER app`, curl health on `/health` | **Letter yes.** |
| No merchant/checkout Dockerfile | both exist, `serve -s dist` on 5178/5179 | **Letter yes.** |
| No bake target | `group "pay"` → `lazuar-pay`, `lazuar-pay-merchant`, `lazuar-pay-checkout` | **Letter yes.** |
| Root compose is Hub | first line: `# Hub museum. 8080 is lazuar-api, not Pay. Do not retarget this file to 8081.` | **Keep. Refuse retarget.** |
| CORS hardcoded | `PayCors` config + fail-boot | **049 letter yes.** Compose default still laptop. |
| No production `VITE_*` | checkout `RUN test -n "$VITE_PAY_API_URL"`; merchant `test -n` pay API **and** checkout origin | **Build-time yes.** Compose ARG defaults still `http://localhost:8081` / `:5179`. |

080 status in the tracker is `resolved`. Treat that as “images exist; Hub compose was not vandalized.” Do not treat it as “Pay is deployable to a URL a stranger can pay.”

### 3.2 Root `docker-compose.yml` is Hub museum (keep)

Opening comment, live:

```yaml
# Hub museum. 8080 is lazuar-api, not Pay. Do not retarget this file to 8081.
# Focused Pay is apps/lazuar-pay/docker-compose.pay.yml (Postgres 5435, profile apps → 8081/5178/5179).
# Do not set ops :3003 or portal :3004 VITE_API_URL to Pay.
#   docker compose up -d --build                 # db + Hub api
#   docker compose --profile full up -d --build  # + Hub frontends
#   docker compose -f docker-compose.ghcr.yml up -d
```

`api` still `dockerfile: apps/lazuar-api/Dockerfile`, `image: ghcr.io/proxeon/lazuar-hub-api:local`, `8080:8080`, `lazuar_mvp` on 5432. Ops 3003, portal 3004, admin 3005, developers 3002, all `VITE_API_URL` / `NEXT_PUBLIC_API_URL` → **8080**. That is the museum. An operator who types `docker compose up` at repo root still gets Hub. 080’s reproduction (“8080 is Hub, not Pay. No 8081 container”) is **still true for root compose**. That is not a bug. It is the refuse.

`docker-compose.ghcr.yml` `name: lazuar-hub`. Same six Hub services. `deploy/prod/docker-compose.yml` `name: lazuar-hub`, Caddy 80/443, `hub-api` 8080, ops/portal/superadmin/developers. `deploy/prod/Caddyfile` is `hub.lazuar.com` path map (`/api/*` → `api:8080`, `/` → ops). **There is no `pay.lazuar.com`. There is no `reverse_proxy` to 8081.** Adding Pay as a fifth upstream of this Caddyfile while `/` still serves ops is the cutover anti-pattern 013-02 / 080 forbade.

`mprocs-dev.yaml` still starts Hub frontends only. Focused Pay is `task pay:dev` / `pay:merchant` / `pay:checkout`. Fine.

`docker-compose.dev-proxy.yml` + `deploy/dev/Caddyfile` listen `:9080` and send `/health` and `/api/*` to `host.docker.internal:8080`. If One owns 8080, that proxy is One, not Pay, not Hub. **Leave it down** during Pay+One. Do not “fix” it by pointing `/api` at 8081 while `/` is still ops.

### 3.3 `docker-compose.pay.yml` is the Pay stack — and it is laptop-shaped

Entire file is 74 lines. Opening comment is honest:

```yaml
# Greenfield Pay stack. Not Hub (repo-root docker-compose.yml stays museum).
#   docker compose -f apps/lazuar-pay/docker-compose.pay.yml up -d            # Postgres 5435
#   docker compose -f apps/lazuar-pay/docker-compose.pay.yml --profile apps up -d --build
#
# Production CORS and VITE_* must be real HTTPS origins, not laptop :5178/:5179.
# Do not add ops :3003 or portal :3004. Do not retarget Hub compose to 8081.
```

**`pay-db`:**

```yaml
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

What is missing, live:

- **No `volumes:`.** Hub root compose has `pgdata:/var/lib/postgresql/data`. Pay does not. `docker compose down` (even without `-v`) drops the container and the data directory that lived in the writable layer. A named volume is the minimum durability for dogfood that has taken a test card. Production needs a real Postgres (Neon, RDS, a volume with backup) — but the file that claims to be the greenfield stack cannot lose `lazuar_pay` on every recreate.
- No `container_name`, no `restart: unless-stopped`, no memory limit, no non-default password, no `POSTGRES_PASSWORD` from env with an empty-fail.
- Port 5435 is published to the **host**. Fine for laptop (`task pay:dev` on the host talking to 5435). Wrong for a VPS where Postgres should be network-internal only.
- Healthcheck is `pg_isready`. Good. `pay` `depends_on: condition: service_healthy`. Good.

**`pay` (profile `apps`):**

```yaml
pay:
  profiles: ["apps"]
  build:
    context: ../..
    dockerfile: apps/lazuar-pay/Dockerfile
  image: ghcr.io/proxeon/lazuar-pay:local
  ports:
    - "8081:8081"
  environment:
    ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Development}
    ConnectionStrings__Pay: Host=pay-db;Port=5432;Database=lazuar_pay;Username=postgres;Password=postgres
    One__BaseUrl: ${One__BaseUrl:-http://host.docker.internal:8080/api/v1}
    Pay__CorsOrigins: ${Pay__CorsOrigins:-http://localhost:5178,http://127.0.0.1:5178,http://localhost:5179,http://127.0.0.1:5179}
    Pay__CheckoutBaseUrl: ${Pay__CheckoutBaseUrl:-http://localhost:5179}
    Pay__PublicBaseUrl: ${Pay__PublicBaseUrl:-http://localhost:8081}
    Pay__WrapKey: ${Pay__WrapKey:-}
    Pay__OneWebhookSecret: ${Pay__OneWebhookSecret:-}
```

Laptop defaults, one by one:

| Env | Compose default | Production need | Why it hurts |
|-----|-----------------|-----------------|--------------|
| `ASPNETCORE_ENVIRONMENT` | **Development** | Production | Image itself is Production. Compose **undoes** it. Test rail allowed. `MigrateAsync` runs. CORS may fall back to laptop list if the CORS env is cleared. |
| `ConnectionStrings__Pay` | `postgres/postgres` to `pay-db` | operator secret, SSL | Hardcoded password in the compose file. Hub ghcr compose at least uses `${POSTGRES_PASSWORD:-postgres}`. |
| `One__BaseUrl` | `http://host.docker.internal:8080/api/v1` | One’s HTTPS `/api/v1` | Linux without `extra_hosts: host.docker.internal:host-gateway` often cannot resolve this. There is **no** `extra_hosts`. One is not in this compose file (correct — One is a sibling repo). The default assumes Docker Desktop. |
| `Pay__CorsOrigins` | four localhost HTTP merchant/checkout | public HTTPS merchant + checkout | Browser origin of a served `serve -s dist` container is `http://localhost:5178` **from the operator’s browser on the same machine**. A phone on LAN, or `https://pay.example`, is denied. |
| `Pay__CheckoutBaseUrl` | `http://localhost:5179` | public HTTPS checkout origin | Success/cancel URLs PSP redirects to. Laptop. |
| `Pay__PublicBaseUrl` | `http://localhost:8081` | public **https** Pay origin | Billplz `TryPublicBase` **rejects** loopback and non-https (`callback base not public`). The compose default is **unusable for Billplz**. Stripe/CHIP hosted URLs do not use this, but Plane B callbacks still need a public URL in the PSP dashboard. |
| `Pay__WrapKey` | empty | 32-byte base64 | Development still requires WrapKey. First PUT gateway is 503 (`Pay:WrapKey is required`). Compose will boot. Vault will not. |
| `Pay__OneWebhookSecret` | empty | per-org PUT, or one-shop fallback | POST `/v1/one/webhooks` is 503 `One webhook secret missing` until an owner PUTs a per-org secret (needs WrapKey) or the process env is set. |
| `Pay__StartMaxPerMinute` | **unset** → C# default 20 | operator choice | Not even in the compose file. See §6. |
| `Pay__StripeWebhookSecret` | unset | must stay unset outside Testing | Correct omission. |

No `healthcheck:` on the compose service (Dockerfile HEALTHCHECK applies to `docker run` / compose if the image has one — it does, on `/health`). No `restart`. No memory limit. No `user`. Depends on `pay-db` healthy, not on One (correct: health must not call One).

**`pay-merchant` / `pay-checkout`:**

Build ARGs default to localhost. Merchant **does not** `test -n` `VITE_ZITADEL_CLIENT_ID`. Compose default for that ARG is empty. A baked merchant image with empty client id renders the LoginPage “Missing `VITE_ZITADEL_CLIENT_ID`” card. Checkout **does** fail the image build without `VITE_PAY_API_URL`. Merchant fails the image build without pay API **and** checkout origin, but **not** without OIDC. Ports 5178/5179 are the **dev** ports, reused in production images via `serve -l 5178`. That is a laptop-shaped port choice, not a correctness bug. A reverse proxy would map 443 → 5178 internally.

`serve@14.2.4` is a Node static server. Fine for dogfood. Not Caddy. No gzip/brotli config, no HTTPS, no security headers. `HEALTHCHECK` uses `wget -qO- http://127.0.0.1:5178/` (merchant) / `5179` (checkout). `node:22-alpine` typically ships BusyBox `wget`. If a future base image drops it, the healthcheck fails while the process is up. Pay’s image apt-installs `curl` as root then drops to `USER app` — the SPAs do not.

Vite env is **build-time**. Changing `VITE_PAY_API_URL` at container run does nothing. Compose `args:` bake the localhost defaults unless the operator passes them at **build**. `image: ghcr.io/proxeon/lazuar-pay-merchant:local` can silently be an old bake with the wrong origin. This is the classic SPA production seam. 050’s checkout fail-if-missing is the right check; it does not make the baked value correct.

### 3.4 Dockerfiles (Pay, merchant, checkout)

**Pay** (`apps/lazuar-pay/Dockerfile`):

- Multi-stage `sdk:10.0` → `aspnet:10.0`.
- Restores **only** `Lazuar.Pay.csproj`. Does not copy tests, does not copy Hub, does not copy `Directory.Build.props` from `apps/lazuar-api`. Good.
- `COPY apps/lazuar-pay/global.json` — SDK pin `10.0.100` `rollForward: latestFeature`.
- `EXPOSE 8081`, `ASPNETCORE_URLS=http://+:8081`, **`ASPNETCORE_ENVIRONMENT=Production`**.
- Installs `curl`, `USER app` (the distroless-ish aspnet image’s non-root user).
- `HEALTHCHECK` `curl -fsS http://127.0.0.1:8081/health` — **liveness, not ready.** A container with Postgres down is “healthy.” Orchestration that only looks at Docker HEALTHCHECK will send traffic. See §5.
- `ENTRYPOINT ["dotnet", "Lazuar.Pay.dll"]`. No migrate. No wait-for-db beyond compose `depends_on`.
- **No `.dockerignore` in the repo** (search for `.dockerignore` files: none). Bake context is repo root (`.`). Pay Dockerfile copies explicit paths, so Hub `apps/lazuar-api/Modules` is not in the Pay image, but the **build context** still uploads the museum unless the daemon is smart. Add a root `.dockerignore` that excludes `apps/lazuar-api`, `**/bin`, `**/obj`, `**/node_modules`, Hub frontends. That is a host hole, not a correctness bug.

**Merchant / checkout:**

- `pnpm@11.5.2` via corepack, `--frozen-lockfile`, filter the app.
- ARG → ENV → `vite build`. Checkout `RUN test -n "$VITE_PAY_API_URL"`. Merchant `test -n "$VITE_PAY_API_URL" && test -n "$VITE_CHECKOUT_ORIGIN"`.
- Runtime `npm install -g serve@14.2.4`, user `web` 1001, `serve -s dist -l 5178|5179`.
- Checkout README: “Do not commit `dist/`.” The tree listing still has `apps/lazuar-pay-checkout/dist/` and `apps/lazuar-pay-merchant/dist/`. Docker copies **source** and builds; it does not COPY `dist/` from the host. Checked-in dist is a 059-class SPA lie (08), not an image lie. Mentioned so operators do not `docker cp` the git dist into prod.

### 3.5 `docker-bake.hcl` group `pay`

```hcl
group "default" {
  targets = ["api", "lazuar-portal", "lazuar-ops", "lazuar-admin", "lazuar-developers"]
}

# Focused Pay. Not Hub. Bake separately: `docker buildx bake pay`
group "pay" {
  targets = ["lazuar-pay", "lazuar-pay-merchant", "lazuar-pay-checkout"]
}
```

`docker buildx bake` with no group still builds **Hub**. That is the museum default. `docker buildx bake pay` is the Pay command. README says so. GHCR workflow does **not** say so (see §9).

Pay targets inherit `_common`:

```hcl
target "_common" {
  platforms = [PLATFORMS]
  labels = {
    "org.opencontainers.image.source"      = "https://github.com/proxeon/lazuar-hub"
    "org.opencontainers.image.vendor"      = "Lazuar"
    "org.opencontainers.image.description" = "Lazuar Hub CaaS platform"
  }
}
```

`lazuar-pay` is labelled Hub CaaS, source `lazuar-hub`. Tags are `${REGISTRY}/lazuar-pay:${TAG}` with `REGISTRY` default `ghcr.io/proxeon`. Image **names** are not `lazuar-hub-api`. Labels still are. Laptop/museum leftover, not a runtime bug.

Merchant bake args default to **empty strings** (`variable "VITE_PAY_API_URL" { default = "" }` etc.). `docker buildx bake pay` without overrides **fails** the merchant/checkout `RUN test -n`. That is actually the correct fail-closed. Compose `--profile apps --build` supplies localhost defaults and **succeeds** with a laptop pixel. Two entry points, two stories. Document: bake Pay for a real origin; compose profile apps is dogfood.

`PLATFORMS` default `linux/amd64` only. No arm64. Apple Silicon bake emulates. Fine for a first GHCR; name it.

### 3.6 Remaining laptop-shaped defaults (080 spirit, still open)

Ranked, host-only:

1. Compose `ASPNETCORE_ENVIRONMENT` default Development on a Production image.
2. No Postgres volume / no backup story.
3. Hardcoded `postgres/postgres`, published 5435.
4. CORS / CheckoutBaseUrl / PublicBaseUrl / VITE_* localhost HTTP.
5. Empty WrapKey and OneWebhookSecret.
6. `host.docker.internal` without `extra_hosts`.
7. No Pay Caddy / no `deploy/prod` Pay file.
8. GHCR workflow Hub-only; bake `default` Hub-only; Hub labels on Pay images.
9. `serve` as production static, ports 5178/5179.
10. No `.dockerignore`.
11. `mprocs-dev.yaml` Hub-only (operator DX, not prod).

Do **not** “fix” 1–11 by editing root `docker-compose.yml`.

---

## 4. `.env.example` vs required Production: WrapKey, CorsOrigins, CheckoutBaseUrl, PublicBaseUrl, One__BaseUrl, connection string, OneWebhookSecret fallback

Live `apps/lazuar-pay/.env.example` (entire):

```
# One HTTP façade (no PAT, no OpenFGA admin).
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5

# Greenfield Pay DB on host 5435. Not One lazuar, not Hub lazuar_mvp.
ConnectionStrings__Pay="Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres"

# 32-byte base64 wrap key for BYOK. Required outside Testing. No Development fallback.
# Pay__WrapKey=

# Testing-only Stripe process fallback when the vault webhook_secret is empty.
# Development and Production use the per-org vault value. Do not set this in Development.
# Pay__StripeWebhookSecret=

# Public https origin Billplz can POST (tunnel). Localhost is rejected.
# Pay__PublicBaseUrl=https://example.trycloudflare.com

# Buyer return origin for hosted success/cancel defaults. Not the Billplz callback.
# Pay__CheckoutBaseUrl=http://localhost:5179

# Comma-separated browser origins allowed to call Pay. Development defaults to
# laptop merchant/checkout/preview ports. Production/Staging empty fails boot.
# Never AllowAnyOrigin. Never add ops :3003 or portal :3004.
# Docker/production: set the public merchant and checkout HTTPS origins.
# Pay__CorsOrigins=https://checkout.example,https://merchant.example

# One-shop HMAC fallback for POST /v1/one/webhooks. Multi-shop: owner PUT
# /v1/orgs/{orgId}/one-webhook { "webhook_secret" }. Pay does not register the
# URL with One (no PAT). One SSRF blocks loopback.
# Pay__OneWebhookSecret=
```

The host does **not** load `.env` itself. `WebApplication.CreateBuilder` loads `appsettings.json`, `appsettings.{Environment}.json`, user secrets (none: no `UserSecretsId`), environment variables, command-line. Operators who `cp .env.example .env` still need `set -a; source .env` or a process manager. `docker compose` interpolates `${Pay__WrapKey:-}` from the **shell / compose env file next to the compose file**, not from `apps/lazuar-pay/.env.example` automatically unless they pass `--env-file`.

### 4.1 Required outside Testing / Production — live C# vs example vs compose vs appsettings

| Knob | C# if missing | `.env.example` | `appsettings.json` | `appsettings.Development.json` | Compose default | Production operator |
|------|---------------|----------------|--------------------|--------------------------------|-----------------|---------------------|
| `One__BaseUrl` | `OneClient` defaults `http://localhost:8080/api/v1` | laptop 8080 | **same laptop default** | (inherits) | `host.docker.internal:8080/api/v1` | **Must set** to One HTTPS. Fail-boot would be better; live is silent loopback inside the container. |
| `One__TimeoutSeconds` | 5 | 5 | 5 | — | unset (5) | 5 is fine. |
| `ConnectionStrings__Pay` | hardcoded `localhost:5435 postgres/postgres` unless Testing | laptop 5435 | **absent** | laptop 5435 | `pay-db` postgres/postgres | **Must set.** Empty Production uses the binary laptop CS. |
| `Pay__WrapKey` | throw on Protect/Unprotect unless Testing SHA256 fallback | commented, “required outside Testing. No Development fallback.” | absent | absent | empty | **Must set** 32-byte base64. Not fail-boot. PUT 503. |
| `Pay__CorsOrigins` | Dev/Testing → eight laptop origins; Production/Staging **throw at boot** | commented HTTPS example | absent | eight laptop HTTP | four localhost 5178/5179 | **Must set** or the Production process will not start. |
| `Pay__CheckoutBaseUrl` | Testing → `http://localhost:5179`; else throw `Pay:CheckoutBaseUrl is required` at **start/mint**, mapped 503 in `MintOrResume` | commented laptop 5179 | absent | `http://localhost:5179` | `http://localhost:5179` | **Must set** public HTTPS checkout origin. Development appsettings hides the throw on laptop. Production image has no Development json. |
| `Pay__PublicBaseUrl` | Billplz start throws `callback base not public` if missing/http/loopback | commented tunnel https | absent | absent | **`http://localhost:8081` (rejected by Billplz)** | **Must set** https non-loopback for Billplz. Other rails ignore it. |
| `Pay__OneWebhookSecret` | POST One webhooks 503 if no per-org ciphertext either | commented empty | absent | absent | empty | One-shop fallback **or** per-org PUT (needs WrapKey). Multi-shop: do not rely on process env (029). |
| `Pay__StripeWebhookSecret` | Testing-only; Production empty ciphertext → parse fail / 503 | commented Testing-only; “Do not set this in Development.” | absent | absent | unset | **Must not set.** Vault `webhook_secret` per org. |
| `Pay__StartMaxPerMinute` | default **20** | **absent** | absent | absent | unset | See §6. Document it. |
| `Pay__ReservationTtlMinutes` | default 30 | absent | 30 | 30 | unset | Fine. |
| `AllowedHosts` | `*` in appsettings.json | absent | `*` | — | unset | `*` behind a known proxy is common; lock if you terminate on the process (you do not). |

`appsettings.json` is loaded in **every** environment, including Production:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "One": { "BaseUrl": "http://localhost:8080/api/v1", "TimeoutSeconds": 5 },
  "Pay": { "ReservationTtlMinutes": 30 }
}
```

That `One.BaseUrl` laptop default in the **base** file is the most important silent Production footgun after the CS fallback. A Production container with no `One__BaseUrl` env calls loopback:8080. Health still 200 (health never calls One). First whoami is 503 `Identity provider unreachable`.

There is **no** `appsettings.Production.json`. There is **no** `ValidateOnStart` for WrapKey / CheckoutBaseUrl / One BaseUrl / connection string. CORS is the only boot-fail. That asymmetry is the 017 leftover: WrapKey fail-closed on first PUT is correct for not wrapping with a git SHA256; fail-at-boot would be better so `task pay:dev` / compose apps dies before a merchant click.

### 4.2 017 / 024 docs vs C# (mostly closed)

017 body still says `.env.example` claims a Dev WrapKey fallback. Live example: “Required outside Testing. **No Development fallback.**” README: “`Pay__WrapKey` is required outside Testing.” `SecretBox.LoadKey`: throw unless `Testing`, then `SHA256("lazuar-pay-dev-wrap-key")`. PUT gateway and PUT one-webhook catch `InvalidOperationException` containing `WrapKey` and return **503**, not 500. `SecretBoxTests` still: Production missing throws; Testing empty hashes. **No Development test** (017 tests section still true).

024 body still says `.env.example` claims a Dev `whsec_` fallback. Live example: “Testing-only Stripe process fallback… Do not set this in Development.” `StripeWebhook.ResolveSecret`: vault ciphertext, else Testing process env, else null. README matches. **024 letter closed.**

### 4.3 OneWebhookSecret fallback (process vs per-org)

`OneWebhookEndpoints.ResolveSecretAsync`: peek JSON `org_id` / `tenant_id` → `org_settings.OneWebhookCiphertext` unwrap → else `config["Pay:OneWebhookSecret"]`. Empty both → 503 `One webhook secret missing`. HMAC after resolve. Peek-before-verify means a forged body can **select which org’s secret is used**; that is 04’s dialect paper, not this slice. This slice only: **Production can run with the process fallback** (one-shop) or with per-org PUT (needs WrapKey). `.env.example` says both. Compose leaves both empty. First-party dogfood that wants One `tenant.suspended` to pause charges must set one of them.

Pay does not POST One `/tenants/{id}/webhooks`. README is honest. Operators paste the Pay URL into One. One SSRF blocks loopback — `http://localhost:8081/v1/one/webhooks` will not work from One’s cloud. PublicBaseUrl-class tunnel is required for Plane A too, even though the knob name is Billplz’s.

### 4.4 Merchant / checkout env (host-adjacent)

Not Pay process env, but the images will not take money without them:

| Knob | Dev fallback | Production build |
|------|--------------|------------------|
| `VITE_PAY_API_URL` | checkout: `http://localhost:8081` if `import.meta.env.DEV`; merchant: `?? 'http://localhost:8081'` even in theory | checkout **throws** if missing (`pay.ts`). Dockerfile `test -n`. Merchant Dockerfile `test -n`. Merchant **source** still `?? localhost` — a production build with the ARG set is fine; a production build that somehow inlines empty would still hit localhost (the Dockerfile prevents empty). |
| `VITE_CHECKOUT_ORIGIN` | `http://localhost:5179` if not `PROD` | merchant `resolveCheckoutOrigin` returns `null` in prod if unset; UI error `VITE_CHECKOUT_ORIGIN is required in production`. Dockerfile `test -n`. |
| `VITE_ONE_API_URL` | `http://localhost:8080/api/v1` | **not** required at Docker build. Production merchant can bake One laptop URL. |
| `VITE_ZITADEL_*` | authority `http://localhost:8085`, redirect `http://localhost:5178/callback`, client id **empty** | **not** required at Docker build. Login page shows missing client id. |

Merchant README: “Production image bakes `VITE_PAY_API_URL` and `VITE_CHECKOUT_ORIGIN`. Empty fails the build. CORS on Pay must list that merchant origin.” Honest. It does not mention baking Zitadel / One URLs as required. That is a first-party dogfood hole: you can produce a merchant image that cannot log in.

Checkout README: “Dev falls back to `http://localhost:8081`. Production `pnpm build` and the checkout Dockerfile **fail** if it is missing — do not default a shipped pixel to localhost. Pay CORS must list this origin.” Honest. CI `pnpm --filter lazuar-pay-checkout build` **without** `VITE_PAY_API_URL` would fail that step **unless** the environment provides it. CI yaml does not set it. Checkout `pay.ts` throws at **module init** in non-DEV when env is missing — Vite `import.meta.env.DEV` is false in `vite build`. So **CI checkout build should fail today** unless `vite build` does not evaluate the throw until runtime of the bundle (it is a function `payApiOrigin()` called as `export const payApi = payApiOrigin()` at module scope — the built module throws when the **browser** loads, not when Vite compiles, unless something imports and runs it at build). Vite replaces `import.meta.env.VITE_PAY_API_URL` with `undefined` and `import.meta.env.DEV` with `false`; `payApiOrigin()` is called at module eval in the browser. **Build succeeds. Runtime throws.** Dockerfile `test -n` is the only build-time gate. **CI does not use the Dockerfile and does not `test -n`.** A green CI build of checkout can still be a localhost-or-throw pixel. That is a CI honesty hole (§9).

---

## 5. Health `/health` `/v1/health` `/ready` (Postgres `CanConnect`; Testing InMemory). Org ready is a different door.

Live `HealthEndpoints.cs` entire:

```csharp
internal static class HealthEndpoints
{
    public static void MapHealth(this WebApplication app)
    {
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
    }
}
```

### 5.1 Liveness

`/health` and `/v1/health` return `{ status: ok }` with **no** One call, **no** DB call. `HealthTests.Health_does_not_call_one` sets `ThrowOnSend = true` and asserts both 200 and `SendCount == 0`. Keep that. A liveness probe that dials One is how Hub-shaped ready probes become outage amplifiers.

Two liveness URLs are a spec wart (019 G4). Honesty allowlists unversioned `/health`; spec has `/v1/health`. Docker HEALTHCHECK uses `/health`. Either is fine as **liveness**. Do not delete `/health` to look tidy; the image probes it.

### 5.2 `/ready` ignores the bool — this is the live ready bug

`Database.CanConnectAsync` returns `Task<bool>`. It returns **false** when the server cannot be reached. It does not always throw. The handler `await`s the task, **discards the boolean**, and returns 200 `{ status: ready }`. 503 happens only if `CanConnectAsync` **throws** (or the DI of `PayDbContext` throws).

Implications:

- Postgres down, Npgsql `CanConnectAsync` → `false` → Pay `/ready` **200**.
- Testing InMemory `CanConnectAsync` → typically `true` → 200. `HealthTests.Unversioned_ready_returns_200_on_inmemory` locks the 200 and `SendCount == 0`. It does **not** lock Postgres-down 503. 076 closed “unmapped/untested”; it did not close “the probe is true.”
- Dockerfile HEALTHCHECK and any Caddy `health_uri` that copies Hub’s `/health` never see DB liveness. Hub `deploy/prod` health-gates `/health` too (Hub’s `/health` is a different implementation). Copying that pattern onto Pay without fixing `/ready` **and switching the gate** is how a Pay replica with a dead DB takes Billplz callbacks and 500s them.

There is no `/health/ready`, no `/health/metrics`, no outbox lag. Do not add Hub’s. Fix the bool.

Catch-all `catch` also swallows `OperationCanceledException` on probe timeout and returns 503, which is acceptable. It swallows programming errors too. Prefer catching `Exception` after checking the bool, and do not catch cancellation.

### 5.3 Org ready is not a probe

`GET /v1/orgs/{orgId}/ready` (`OrgReadyEndpoints`):

1. `MemberGate.RequireMemberAsync` (Bearer → One).
2. Load `OrgSettings` + `GatewayCredentials.Any` for that org.
3. `ready = !chargesPaused && (hasVault || PayProviders.AllowsTest(env))`.

This is a **merchant UI door**: “can this shop take money.” Test rail makes Development/Testing orgs “ready” without a vault. Production `AllowsTest` is false (not this paper’s money judgment — 07/42). Unversioned `/ready` is a **host** probe. README: “Unversioned `GET /ready` is a host probe, not org ready.” Honest. Caddy/k8s must not probe `/v1/orgs/…/ready`. It needs a Bearer and calls One.

078 (dummy `ready: true`) is resolved as “now checks pause + vault/test.” Still not a process probe.

### 5.4 Testing InMemory vs Npgsql

Program.cs skips Npgsql in Testing. Factory adds InMemory unless `PostgresConnection`. `/ready` in unit tests therefore proves “the route exists and does not call One,” not “CanConnect fails closed.” `PayPostgres.FactoryAsync` exists for TX/unique proofs and `Assert.Ignore`s when Docker is missing. There is **no** test that `/ready` is 503 when Postgres is down. CI pay job has no Postgres service; Testcontainers tests skip or run depending on Docker-in-Docker. ubuntu-latest has Docker; those tests *can* run. They still do not assert `/ready`.

### 5.5 How an operator should health-gate (today vs required)

| Layer | Today | Required for production |
|-------|-------|-------------------------|
| Docker HEALTHCHECK | `GET /health` | Keep as **liveness** (process up). |
| Compose `depends_on` | `pay-db` healthy via `pg_isready` | Keep. Does not replace Pay `/ready`. |
| k8s/caddy readiness | **none** (no Pay Caddy) | `GET /ready` **after** the bool is honoured. Never One. Never org ready. |
| Hub `deploy/prod` health-gate | `curl api:8080/health` | Do not reuse. Pay is not that stack. |

---

## 6. `PublicPayLimiter` `StartMaxPerMinute`. Production default vs factory 200. Enough?

### 6.1 Live limiter

`PublicPayLimiter.cs` entire:

```csharp
internal static class PublicPayLimiter
{
    static readonly ConcurrentDictionary<string, List<long>> Hits = new(StringComparer.Ordinal);

    public static bool TryAcquire(string key, int max, int windowSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var list = Hits.GetOrAdd(key, static _ => []);
        lock (list)
        {
            list.RemoveAll(t => t < now - windowSeconds);
            if (list.Count >= max)
            {
                return false;
            }

            list.Add(now);
            return true;
        }
    }
}
```

Used only in `PublicPayEndpoints.Start`:

```csharp
var maxStarts = config.GetValue("Pay:StartMaxPerMinute", 20);
if (maxStarts > 0 && !PublicPayLimiter.TryAcquire(token, maxStarts, 60))
{
    return PayErrors.Status(429, "Too Many Requests", "Too many start attempts");
}
```

No `Retry-After` header (grep `Retry-After` in `apps/lazuar-pay/**/*.cs`: none). No IP key. No ASP.NET `AddRateLimiter`. `maxStarts > 0` means you can disable the limiter with `Pay:StartMaxPerMinute=0`.

### 6.2 Factory 200 vs production 20

`PayApiFactory.StartMaxPerMinute` default **200**, always `UseSetting("Pay:StartMaxPerMinute", …)`. Tests that mint many starts on one token would 429 at 20. `PaymentLinkTests.Start_rate_limit_is_429` sets `StartMaxPerMinute = 2`, two OK, third 429. That locks the door. It does not lock the production default.

Production (and Development, and compose) unset → **20 per public token per 60 seconds**, in-process.

### 6.3 Is 20 enough?

Depends what “enough” means.

**As grief protection on a one-person pay link:** 20 starts/minute/token is plenty. The public token is the capability (019 B8 / issue 019). A scraper that knows the URL can still fill `MaxPayers` with junk `slot_key`s — the limiter slows that to 20 reservations/minute, not to zero. Occupancy + TTL (07) is the real cap. The limiter is a bump in the road.

**As a hot event link (unlimited or large `MaxPayers`):** 20 starts/minute is a **bottleneck**, not a shield. 200 people opening the page at 19:00 will 429. There is no `Retry-After`; the checkout SPA’s handling of 429 is 08. Factory 200 is the number tests needed, not a production recommendation.

**As a multi-replica limiter:** `static ConcurrentDictionary` is **per process**. Two replicas → ~40/min/token. Ten replicas → 200. Horizontal scale **weakens** the limit. Occupancy’s `SerializeAsync` is the same shape (`static ConcurrentDictionary<string, SemaphoreSlim> Gates`) **plus** `SELECT … FOR UPDATE` on Npgsql. Occupancy is saved by the row lock. The start limiter is not. Redis/ASP.NET partitioned limiter is the production shape if you ever run two Pay replicas. Until then, **one replica** is the honest deploy (Hub `deploy/prod` already documents single API replica because workers are in-process). Pay has no background workers; the replica limit is the in-process dictionaries.

**As a memory bound:** keys are public tokens. Lists are pruned by time, **keys are not**. Every distinct token ever started stays in `Hits` until process recycle. A scrape of random tokens is a slow dictionary leak. Not a first-week dogfood problem. Name it.

**As IP abuse protection:** none. One client can 20/min/token across many tokens (whoami-less GET is free; start is per token). A botnet posting `/v1/webhooks/stripe/{orgId}` is unlimited (see §12).

**Verdict:** 20 is enough for **Consumer-0 one-person links on one replica**. It is not enough for an event. It is not a production abuse kit. Document `Pay:StartMaxPerMinute`. Do not raise the default to 200 to match the factory — that is how tests hide production. If you need a concert, raise the knob **and** run one replica, or put a real limiter in front (Caddy `rate_limit`, Cloudflare) keyed on IP **and** keep the per-token limiter as a second line.

`maxStarts > 0` as an off switch is a footgun in Production. Do not document `0` as “unlimited” without saying it is unlimited.

---

## 7. Logging: secrets in logs? Request body logging?

### 7.1 What the host actually logs

`Lazuar.Pay.csproj` has no Serilog, no NLog, no Seq. Default Microsoft logger. `appsettings.json`:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

Same in Development. `Microsoft.AspNetCore` at Warning means no request pipeline logs (no path, no status, no timing). `Default: Information` includes `Microsoft.EntityFrameworkCore` unless overridden — EF “Executed DbCommand” at Information **without** `EnableSensitiveDataLogging` (grep: none) prints SQL with parameter **placeholders**, not values. Ciphertext in `INSERT` is not in the log. Bearer is not in the log. Payer email as a parameter is not in the log unless someone turns sensitive logging on. Do not.

The **only** `ILogger` call in `apps/lazuar-pay/src/**/*.cs` is Program.cs migrate:

```csharp
app.Logger.LogError(ex, "pay-db schema mismatch; run task pay:db:migrate");
```

Grep `LogInformation` / `LogWarning` / `LogError` / `ILogger` under `apps/lazuar-pay/src`: that one hit. No request log. No “start began.” No “webhook accepted.” No org id. Production operators get stdout from the framework (host lifetime: listening on 8081) and this one error.

### 7.2 Request body logging

No `UseHttpLogging`, no `UseRequestLogging`, no middleware that buffers the body. Plane B and Plane A read `request.Body` into a `string raw` / `json` **in memory** for HMAC. They do not log it. Good.

Do **not** add ASP.NET `HttpLogging` with `RequestBody` on this host. Those strings are Stripe event payloads, Billplz callbacks, One HMAC bodies, and `PUT /gateway` secrets. Hub-shaped “log the body for debugging” is how BYOK leaks.

Unhandled exceptions: no `UseExceptionHandler`. Development may show the developer exception page (ASP.NET default when `IsDevelopment()` and no handler). Compose default **is Development**. A 500 on a public `/start` can dump a stack. Production image default is Production (no developer page). Compose undoes that. Another reason compose’s Development default is a host hole.

`catch (Stripe.StripeException)` in Start maps to 503 `"Stripe rejected the org key"` — message does not include the key. Billplz/CHIP similar. WrapKey catch returns `ex.Message` which is `"Pay:WrapKey is required"` / `"Pay:WrapKey must be 32 bytes base64"` — not the key. `last4` of the **secret** is stored and returned on GET gateway (intentional hint, not a log). Do not start logging `last4` to stdout.

### 7.3 Secrets that can still hit the log

| Secret | Path into logs | Live? |
|--------|----------------|-------|
| `ConnectionStrings__Pay` password | Npgsql exception on migrate `LogError(ex, …)` | **Yes, Development migrate failure.** |
| Laptop fallback `Password=postgres` | same | Yes. |
| `Pay__WrapKey` | not logged by C# | No, unless the operator’s process manager dumps env. |
| Bearer | forwarded to One; not logged | No, unless Microsoft.AspNetCore is dropped to Information **and** headers logging is enabled (it is Warning). |
| Vault plaintext | only in memory around `Protect` | `SecretBox` xml-doc: “Never log plaintext.” Code does not log. |
| Webhook raw body | in memory, not logged | No. |
| `Pay__OneWebhookSecret` | not logged | No. |
| Compose file `Password=postgres` | in git | **Yes, in the repo.** Not a log. Still a secret. |

**Verdict:** there is no request-body logging. There is almost no logging. The migrate `LogError(ex)` is the realistic leak (CS in Npgsql exceptions). Production with a managed Postgres and no Development migrate will rarely hit it. Do not add Serilog “to look finished.” If you add logs: console JSON, no headers, no bodies, no CS, request id, route, status, duration, org id **after** authz. That is a host-production missing feature, not a 002 bug.

---

## 8. Metrics, tracing, OpenTelemetry — present or absent

**Absent.** Grep `OpenTelemetry`, `AddOpenTelemetry`, `ActivitySource`, `Meter`, `prometheus`, `UseSerilog`, `HttpLogging` under `apps/lazuar-pay/**` source: no matches (only framework assemblies in `obj/` / `bin/` mentioning `Microsoft.AspNetCore.HttpLogging` as a shared framework ref, unused).

`Lazuar.Pay.csproj` PackageReferences: EF Design, Npgsql, Stripe.net. IsolationTests would not ban OTel; they ban MediatR / Hub modules. Adding OTel is allowed by isolation and still a product choice. 013 said: do not copy `BuildingBlocks.Infrastructure.Observability`. Console logs + a Postgres-true `/ready` are the Pay kit. Metrics (start 429 count, webhook 401 count, fulfill latency, One 503 count) are how you notice a rail outage without reading Stripe’s dashboard. They are **missing features**, not 002 regressions.

Tracing: no W3C `traceparent` handling. Fine for one process. The moment Pay sits behind Caddy and you debug a Billplz callback, you will want a request id in logs. There is no `X-Request-Id` middleware.

Prometheus `/metrics`: absent. Do not add it on `/v1` (public). If added, unauthenticated scrape on a private listen or with a scrape secret. Not this quarter’s dogfood blocker unless you already have Grafana.

Hub contrast (013): Serilog + `/health/metrics` + outbox lag. Museum. Do not import.

---

## 9. CI: Taskfile pay tests, honesty script, GitHub workflows. Vitest merchant/checkout in CI?

### 9.1 Taskfile `pay:*`

```yaml
pay:restore / pay:build / pay:test / pay:dev
pay:db:up        # compose without profile apps → Postgres 5435 only
pay:db:migrate   # dotnet ef database update, one context
pay:spec         # tsp compile + check-pay-openapi-honesty.mjs
pay:merchant     # pnpm --filter lazuar-pay-merchant dev
pay:checkout     # pnpm --filter lazuar-pay-checkout dev
```

`pay:test` is `dotnet test Lazuar.Pay.slnx`. It does **not** run vitest. It does not bake images. It does not `docker compose --profile apps`. `pay:spec` is the honesty gate locally.

`IsolationTests` still ban cathedral strings and Hub `@repo/api-types-ts` on the Vite `package.json` files. That runs inside `pay:test`. Good.

### 9.2 GitHub `ci.yml` `pay` job (live)

```yaml
pay:
  runs-on: ubuntu-latest
  steps:
    - checkout, setup-dotnet 10.0.x, setup-node 22, pnpm 11.5.2
    - pnpm install --frozen-lockfile
    - name: Test focused Pay host
      run: dotnet test apps/lazuar-pay/Lazuar.Pay.slnx --nologo --verbosity minimal
    - name: Build merchant and checkout
      run: |
        pnpm --filter lazuar-pay-merchant build
        pnpm --filter lazuar-pay-checkout build
    - name: Compile pay-spec
      run: pnpm --filter @repo/pay-spec exec tsp compile .
    - name: Pay OpenAPI ↔ Map* honesty
      run: node scripts/check-pay-openapi-honesty.mjs
```

What CI **does**:

- Hermetic host tests (InMemory; Testcontainers if Docker works, else Ignore).
- `tsc -b && vite build` for both SPAs (merchant `package.json` `"build": "tsc -b && vite build"`).
- TypeSpec compile + `check-pay-openapi-honesty.mjs`.

What CI **does not**:

- **`pnpm --filter lazuar-pay-merchant test` / checkout `vitest run`.** Both apps have `"test": "vitest run"` and `src/**/*.test.ts` (locks on `VITE_PAY_API_URL` / `VITE_CHECKOUT_ORIGIN` strings). Those tests never run in GitHub Actions. A broken lock test would not fail CI.
- Docker build of the three Pay images.
- `docker buildx bake pay`.
- `docker compose -f apps/lazuar-pay/docker-compose.pay.yml` smoke.
- Postgres service for Pay (Hub `dotnet` job **does** have `postgres:16-alpine` for `lazuar_mvp`).
- Setting `VITE_PAY_API_URL` on the checkout **build** (see §4.4: Vite build can succeed with undefined env; runtime throw; Dockerfile is the real gate and CI does not use it).
- Hub `contracts` job still runs Hub `task gen` + Hub honesty. Pay honesty is the **pay** job. Two honesty scripts. Keep them apart.

`turbo.json` has a generic `test` task `dependsOn: ["build"]`. CI does not `pnpm turbo test`. `mprocs` is Hub.

### 9.3 GHCR workflow is Hub-only

`.github/workflows/ghcr.yml`: matrix `lazuar-hub-api|portal|ops|superadmin|developers`. Deploy rsyncs `deploy/prod/` to `/root/lazuar-hub-prod/` and runs `lazuar-hub-remote-deploy.sh`. Concurrency group `lazuar-hub-cd`. **No `lazuar-pay*` image is built or pushed.** `docker buildx bake pay` is a local/operator command. Staging of Pay is not CD.

Push paths include `apps/**`, so a Pay-only PR to `main` still **rebuilds Hub images** and deploys Hub. That is waste and risk (Hub deploy on a Pay change). Not this slice to redesign Hub CD; name it so Pay CD is not “add a matrix row to the Hub workflow that SSH-deploys hub.lazuar.com.” Pay needs its **own** bake+push (and later its own VPS/Caddy). Mixing them is how 8081 lands behind `hub.lazuar.com/api`.

### 9.4 Honesty script (host-relevant)

`scripts/check-pay-openapi-honesty.mjs`: OpenAPI ⊆ Map*; Map* ⊆ OpenAPI ∪ `{ GET /health, GET /ready }`. Unversioned `/ready` is **allowed to be missing from the spec**. `/v1/health` must be in both. This is a **CI lock that production probes will not drift** from the binary without a spec change — except `/ready`, which is deliberately host-only. If you add `/metrics`, either allowlist it or spec it. Prefer allowlist (do not sell scrape to integrators).

---

## 10. Migrations, backup, wrap key rotation, pending model changes

### 10.1 Migration inventory

| File | Designer? | Role |
|------|-----------|------|
| `20260821152601_Initial.cs` | yes | First schema |
| `20260824120000_FourAdaptersHostedRails.cs` | **no** | WebhookCiphertext, PublicMerchantId, Environment, ActiveProvider, … |
| `20260825120000_PaymentLinkPayers.cs` | **no** | SlotKey + unique filter |
| `20260828001217_FulfillmentUniques.cs` | yes | unique charge/document |
| `20260828001728_BackfillNullCheckoutProvider.cs` | yes | data backfill |
| `20260828093000_OrgOneWebhookCiphertext.cs` | **no** | `org_settings.OneWebhookCiphertext` — HEAD subject |
| `PayDbContextModelSnapshot.cs` | — | includes `OneWebhookCiphertext`; SlotKey unique filter |

Three hand-written migrations lack Designers. `dotnet ef migrations add` later still works off the snapshot; `dotnet ef migrations script --idempotent` works off `Up`. `migrations has-pending-model-changes` compares snapshot to `OnModelCreating`. Snapshot **has** `OneWebhookCiphertext` and the filtered unique index. Live `PayDbContext` matches those. **No pending model change observed** for columns/indexes this slice cares about. Provider-conditional index (`if Npgsql … HasFilter`) vs snapshot always Npgsql is the usual EF pattern.

Missing Designers are a **tooling seam** (019 G11). Generate them the next time you touch migrations. Do not block production on pretty Designers.

Production apply path: **not** at boot (`IsDevelopment` only). Operator: `task pay:db:migrate` against the Production CS, or `dotnet ef database update` in CI before rollout, or a one-shot migrate container using the **same** image with a different entrypoint. None of those files exist. A second replica starting during migrate is EF’s problem (lock `__EFMigrationsHistory`). Single replica + migrate-before-up is the Hub prod lesson and applies here.

### 10.2 Backup

None. No `pg_dump` job, no WAL shipping, no Neon PITR named in a Pay `deploy/` file (Hub `deploy/prod/env.example` is Neon for **Hub** `lazuar_mvp` / nine schemas — do not point Pay at that database). Compose Pay DB has **no volume**, so backup would dump emptiness after a recreate.

What to back up when money is real:

- Postgres `lazuar_pay` (checkouts, receipts, journal, vault **ciphertext**, One webhook ciphertext).
- `Pay__WrapKey` (in the secret manager, not in the DB dump alone — a dump without the wrap key is opaque; a wrap key without the dump is useless).
- DataProtection key ring **if** you ever persist one (today unused).

Hub `deploy/prod/README.md` is not a Pay backup runbook. Do not pretend it is.

### 10.3 Wrap key rotation

`SecretBox` is AES-GCM, 12-byte nonce + 16-byte tag + ciphertext, key = 32-byte base64 `Pay:WrapKey`. Empty + Testing → SHA256 of a **git-constant** `"lazuar-pay-dev-wrap-key"`. Empty + Development/Production → throw. Wrong length → throw.

There is **no key id** in the wrapped blob. Rotation = (1) deploy a new WrapKey, (2) every `Unprotect` of old rows fails, (3) every PSP webhook and every start that needs the vault 503s, (4) One per-org `whsec_` unwrap fails (Plane A pause dies), (5) there is no rewrap command. `LoadKey` reads config each call, so a **dual-key** (current + previous) could be added without a process concept change. It is not there.

Treat rotation as a **missing feature**. Operational workaround: schedule downtime, decrypt-with-old re-encrypt-with-new in a one-off `dotnet` tool you have not written, then swap env. Do not log plaintext while doing it.

Testing SHA256 fallback must **never** wrap a shared `lazuar_pay` that Development also mounts. Factory is Isolated InMemory or a throwaway Testcontainers DB. Laptop Development must set `Pay__WrapKey`. Compose empty WrapKey is how dogfood vault PUT 503s.

### 10.4 Data in the image / process

- Checkout durability: Postgres (store is scoped EF). Good.
- Occupancy gates: in-process + `FOR UPDATE`. Two replicas need the SQL lock (it exists) and must not rely on the SemaphoreSlim (it will not coordinate).
- Start limiter: in-process only (§6).
- `AddDataProtection()` key ring: ephemeral. Unused.
- `mail_outbox`, `audit_events`, `subscriptions` tables exist. No mail sender host. No backup of them as a product. 07’s problem if they become money.

---

## 11. TLS, cookies, HSTS, forwarded headers — none vs required behind reverse proxy

Grep `UseHttpsRedirection`, `UseHsts`, `ForwardedHeaders`, `UseForwardedHeaders`, `SameSite`, `SecurePolicy`, `Cookie` under `apps/lazuar-pay/src/**/*.cs`: **no matches.**

### 11.1 TLS in the process: none (and should stay none)

Pay listens HTTP 8081. Dockerfile `ASPNETCORE_URLS=http://+:8081`. No Kestrel cert, no `app.UseHttpsRedirection()`, no `UseHsts()`. 013’s lock was: TLS at **Caddy**, HTTP on the internal network. Hub `deploy/prod/Caddyfile` does that for Hub. **Pay has no Caddyfile.** First-party production HTTPS does not exist as a file in this repo.

Required behind a reverse proxy:

| Header / knob | Pay process today | Need |
|---------------|-------------------|------|
| TLS terminate | none | Caddy/Cloudflare/nginx in front. Not Kestrel. |
| HSTS | none | On the **proxy**. Adding HSTS in Kestrel on HTTP 8081 is wrong. |
| `X-Forwarded-For` / `X-Forwarded-Proto` | unread | Only needed if the process **generates** absolute URLs from `Request.Scheme` or does IP rate limits. **It does not.** `CheckoutUrls.Base` is config. Billplz callback is `Pay:PublicBaseUrl`. Start limiter keys on **token**, not IP. Forwarded headers are **not** required for URL generation today. |
| `UseForwardedHeaders` | absent | If you later log client IP or IP-limit, configure KnownProxies. Until then, skipping is safer (avoids spoofed proto). |
| Cookies | Pay API sets none. Merchant OIDC: `sessionStorage` (`oidcConfig.ts`: “Tokens in sessionStorage — not cookies”). Checkout: `localStorage`/`sessionStorage` slot key. | Do not add Pay cookies to “fix” slot keys. |
| `Secure` / `SameSite` | N/A | If a cookie appears, it is 08/05, and it must be Secure+None or Lax on HTTPS origins. |

`AllowedHosts: "*"` — acceptable behind a proxy that already filtered Host. Lock it to the public hostname if Pay is ever reachable without a proxy.

Billplz `TryPublicBase` **requires https and non-loopback**. That is the process enforcing TLS **on the callback URL string**, not serving TLS. Stripe/CHIP dashboards need the same public https; the host does not validate those.

### 11.2 What “required behind reverse proxy” means for this SHA

You **cannot** put this image on the public internet on :8081 without a proxy and call it production. You **can** put it on a private network behind Caddy with a Pay Caddyfile that does not exist yet. The missing artifact is **`deploy/pay/`** (Caddyfile + compose + env.example), not `UseHttpsRedirection` in `Program.cs`. Copying Hub `deploy/prod/Caddyfile` and adding `reverse_proxy pay:8081` next to ops at `/` is the refuse.

SPA `serve` is also HTTP. The proxy must front **three** origins (or path-map three apps). Path-mapping merchant/checkout onto one host is a product choice (08). Subdomains `pay.example`, `merchant.example`, `checkout.example` match CORS-as-origins better. Neither is in repo.

---

## 12. Rate limits besides start: webhook, mint, whoami

Live rate limit call sites in focused Pay: **one** (`PublicPayLimiter.TryAcquire` in Start). Hub has several in-process limiters (`PublicAuthRateLimiter`, `PortalMagicLinkRateLimiter`, …). Do not copy them. Name the Pay doors that are naked:

| Door | Auth | Limiter | Abuse story |
|------|------|---------|-------------|
| `POST /v1/pay/{token}/start` | none | 20/min/**token**, in-process | §6. |
| `GET /v1/pay/{token}` | none | **none** | Cheap. Occupancy expire-on-GET takes a lock. A scrape burns DB. |
| `POST /v1/webhooks/{provider}/{orgId}` | PSP HMAC after reading **full body** | **none** | Attacker can POST megabyte bodies to a real orgId. Signature fail still spent CPU+alloc. Stripe retries are honest; a flood is not. Unique event table grows only on success path after parse — confirm 07 — but 401s still allocate `raw`. |
| `POST /v1/one/webhooks` | HMAC after peek+resolve | **none** | Same, plus peek-org select. |
| `POST /v1/checkouts`, `POST /v1/payment-links`, catalog POST | writer Bearer | **none** | Stolen staff JWT can mint unbounded. One should revoke; Pay will not slow it. |
| `GET /v1/whoami` | Bearer | **none** | Pay is an amplifier: each whoami is One `/me`. A scripted 401 storm is One’s problem and Pay’s. MemberGate 064 maps One 429 → Pay 503 (05/10), which makes a whoami flood look like “identity down.” |
| `PUT /v1/orgs/{id}/gateway` | writer | **none** | Wrap/unwrap CPU. Not the first flood. |
| `GET /health` | none | none | Correct for probes. Do not rate-limit liveness from the probe network. |

`AddRateLimiter` is absent (013 table). Hand-rolled start limiter is the only exception. Production-enough for Consumer-0: **put Cloudflare/Caddy in front** for IP limits on `/v1/webhooks` and `/v1/pay`, keep the per-token start limiter, do not build Redis this week. Missing in-process webhook/whoami limits are **host holes** if Pay is a public IP without a proxy. They are **integrator-irrelevant** (a stranger’s server-side `lzr_sk_` mint is 02; that door does not exist).

`Pay:StartMaxPerMinute=0` disables the only limiter. Production env.example (when it exists) must not ship 0.

---

## 13. How to solve production-host holes vs integrator holes. Ranked. Refuse: retarget Hub compose onto 8081.

### 13.1 Split

**Host-production holes** (this slice; first-party One + merchant + checkout on HTTPS):

Things an operator of *this* stack hits even if no second app ever appears.

**Integrator holes** (other 020 papers; do not “fix host” with them):

- No M2M / `lzr_sk_` on Pay (02).
- No outbound `payment.completed` (03).
- Plane A/B product dialect leftovers (04).
- Writer overlay vs One admin (05).
- `/v1` stranger-shaped errors/idempotency/versioning (01).
- Sample app outside this repo (09).
- Occupancy/fulfill leftover (07).

A beautiful Caddyfile does not mint a machine key. A machine key does not backup `lazuar_pay`. Keep the punch-lists apart.

### 13.2 Ranked host-production holes (how to solve — analysis, not a patch)

**P0 — will lose money or lie “healthy” in the first real deploy**

1. **`/ready` must honour `CanConnectAsync`’s bool.** Return 503 `{ status: not_ready }` when `false`. Add a test that is not InMemory-success-only (Testcontainers down → 503, or a fake `CanConnect`). Point Docker **readiness** (not liveness) at `/ready` once true. Keep `/health` as liveness. Do not probe org ready.

2. **Production must not silently use laptop CS / laptop `One:BaseUrl`.** Empty `ConnectionStrings:Pay` outside Development/Testing should fail boot (or `/ready` stay 503 and whoami 503 is already true for One — still fail boot so operators do not discover it at first payment). Remove `http://localhost:8080/api/v1` from **base** `appsettings.json` or override it in a real `appsettings.Production.json` that does not default loopback. Laptop defaults belong in `appsettings.Development.json` only (CS already does; One does not).

3. **Compose `--profile apps` must not undo the image’s Production env as the *documented* prod path.** Either: (a) profile `apps` is explicitly “laptop containers,” documented, and a **separate** `docker-compose.pay.prod.yml` has Production, HTTPS origins, WrapKey required, named volume, no published 5435; or (b) fail `pay` container boot when WrapKey/CORS/CS/One URL look like laptop while `ASPNETCORE_ENVIRONMENT=Production`. Do not retarget Hub compose.

4. **Postgres durability.** Named volume on `pay-db` for dogfood. Real Postgres + backup for production. Do not take a live card against a volume-less compose.

5. **WrapKey fail-boot outside Testing** (`ValidateOnStart` or a Program.cs check after Build, before Map). PUT-503 is better than 500 (017 closed that). Boot-fail is better than a running host that cannot vault. Rotate later (§10.3) is P1.

**P1 — first-party HTTPS dogfood still cannot be a URL**

6. **`deploy/pay/`** (or equivalent): Caddyfile for three origins or one hostname with three path maps, compose that is **not** Hub, env.example with WrapKey / CorsOrigins / CheckoutBaseUrl / PublicBaseUrl / One__BaseUrl / ConnectionStrings__Pay / OneWebhookSecret-or-per-org runbook. TLS at Caddy. HTTP 8081 internal. **Refuse** editing `deploy/prod/Caddyfile` to `reverse_proxy` Pay while `/` is ops.

7. **GHCR bake `pay` as its own workflow** (or a matrix that **does not** SSH Hub). Push `ghcr.io/proxeon/lazuar-pay{,-merchant,-checkout}`. Fix `_common` labels so Pay is not “Lazuar Hub CaaS.” Do not add Pay to `ghcr.yml`’s Hub deploy job.

8. **Compose laptop defaults that lie to Billplz / CORS.** `Pay__PublicBaseUrl=http://localhost:8081` is invalid for Billplz. Empty is better than a rejected default (start 400 `callback base not public` is at least true). CORS defaults in compose should not be shippable as “prod.” `extra_hosts` for `host.docker.internal` if One stays on the host.

9. **CI: run vitest** for merchant and checkout (`pnpm --filter lazuar-pay-merchant test` and checkout). **CI: `test -n "$VITE_PAY_API_URL"`** (or `vite build` with an explicit dummy https origin) so checkout CI matches the Dockerfile gate. Optional: `docker build` the three Dockerfiles with dummy https ARGs.

10. **Start limiter honesty.** Document default 20. Add `Retry-After`. Do not raise to 200. One replica until the limiter is shared. Do not disable with 0 in any prod env.example.

11. **Migrate Production out of band.** A documented `migrate` one-shot (same image, `dotnet Lazuar.Pay.dll` is the wrong entry — use `dotnet ef` in SDK image or `Database.Migrate()` behind an explicit env `PAY_MIGRATE=1` that exits). Development catch-and-continue can stay. Do not auto-migrate Production at boot (Hub’s nine-context boot migrate is the museum).

**P2 — production kit that can wait until after first HTTPS dogfood**

12. Console JSON logs, no bodies, no CS, request id. Fix migrate `LogError(ex)` so Npgsql does not print Password= (log `ex.GetType().Name` + message redacted).

13. Wrap dual-key + rewrap tool. Until then, wrap key is a pet.

14. Webhook/whoami IP rate limit **at the proxy**. In-process only if there is no proxy.

15. `.dockerignore`. Bake `linux/amd64` documented. `serve` → Caddy static optional.

16. Delete unused `AddDataProtection()` or persist keys. Do not leave a trap.

17. Generate missing migration Designers. Snapshot is already current.

18. OTel/metrics. After you have a URL and a backup.

19. `Pay:StartMaxPerMinute` in `.env.example`. Merchant Dockerfile `test -n` on `VITE_ZITADEL_CLIENT_ID` for first-party images.

### 13.3 Integrator holes this paper refuses to treat as host work

- M2M Bearer that is not a human JWT.
- Outbound webhooks to the merchant’s app.
- Versioned public `/v1` cleanliness, SDK, second-app sample.
- “Make root compose start 8081 so the monorepo looks unified.”

Those can be true and still leave first-party dogfood down because `/ready` is 200 on a dead DB.

### 13.4 Refuse

**Do not retarget Hub compose onto 8081.** Live refuse is already written in four places; keep all four:

- `docker-compose.yml` line 1–3.
- `docker-compose.pay.yml` line 6.
- `apps/lazuar-pay/README.md` line 40: “Root `docker-compose.yml` is Hub museum (8080). … Do not set ops/portal `VITE_API_URL` to 8081.”
- Issue 080 suggested fix.

Also refuse:

- Adding Pay as a fifth service in `deploy/prod/docker-compose.yml` `name: lazuar-hub` with Caddy `/api` flipped to 8081 while `/` serves ops.
- Setting `lazuar-ops` `VITE_API_URL` to Pay.
- `AllowAnyOrigin`.
- Development SHA256 wrap key as a Production/Development default.
- Process `Pay__StripeWebhookSecret` in Development/Production.
- `UseHttpsRedirection` on a container that only speaks HTTP on 8081.
- Copying Hub Serilog/KeyVault/MediatR/nine DbContexts to “look production.”
- Health probes that call One.
- Treating 080 `status: resolved` as “Pay is production-ready.”

### 13.5 What 002 actually closed on the host (so 10 does not re-open them as bugs)

| 002 issue | Live |
|-----------|------|
| 017 WrapKey docs + PUT 500 | Docs match; PUT 503. Boot still does not validate. |
| 018 CS substring replace | Gone; empty CS still laptop default. |
| 021 MigrateAsync crash + tests on Development Npgsql | Caught; tests Testing/InMemory. |
| 024 `.env.example` Dev `whsec_` | Testing-only comment. |
| 049 CORS laptop-only | Config + fail-boot Production/Staging. Compose default laptop. |
| 066 CORS tests only `/health` | Public pay GET/POST/OPTIONS tested. |
| 076 `/ready` untested | InMemory 200 tested. Bool still ignored. |
| 080 no image / bake / Hub retarget | Images + bake group `pay`; Hub compose museum. Laptop defaults remain. |

---

## 14. Files vs 013 / 019 (disagreement named)

013-03 at `6f866ff0` said: no Dockerfile, no EF, no `/ready`, no rate limiter, no OTel, CheckoutStore in-memory. **Live disagrees:** Dockerfile exists, EF exists, `/ready` exists, a hand-rolled start limiter exists, CheckoutStore is Postgres. **Still agrees:** no OTel, no HSTS, no forwarded headers, no UserSecrets, no `appsettings.Production.json`, no Serilog, Hub compose is Hub.

019-01 at `9f04ad58` said: CORS hardcoded, compose DB-only, MigrateAsync uncaught, WrapKey docs lie, no start limiter. **Live disagrees:** CORS config, compose profile apps, migrate caught, WrapKey docs honest, start limiter default 20. **Still agrees:** no OTel, laptop One URL in base appsettings, Production empty CS fallback, `/ready` not a real fail-closed probe (019 described CanConnect; it did not notice the discarded bool — this paper does).

080 at `9f04ad58` said no Pay image. **Live disagrees.** 080 spirit (URL a stranger can pay) **still agrees it is missing.**

---

## 15. Short answers to the slice checklist

1. **Program.cs:** listen via launchSettings/Dockerfile 8081, not `app.Run(url)`. `MigrateAsync` Development-only, try/catch, log. CORS via `PayCors.Add`. Testing skips Npgsql. MapHealth, MapWhoami, MapOrgReady, MapCheckouts, MapPaymentLinks, MapCatalog, MapPublicPay, MapGateways, MapWebhooks, MapPaymentQueries, MapOneWebhooks.

2. **Compose/images/bake:** Pay Dockerfiles exist; bake group `pay` exists; root compose is Hub museum (keep). 080 letter closed; laptop defaults remain. **Refuse Hub retarget.**

3. **`.env.example` vs Production:** WrapKey / CorsOrigins / CheckoutBaseUrl / PublicBaseUrl / One BaseUrl / CS / OneWebhookSecret are documented. Only CORS fail-boots. WrapKey and CheckoutBaseUrl fail first use. CS and One BaseUrl **silently laptop**. Stripe process secret Testing-only (honest). StartMaxPerMinute undocumented.

4. **Health:** `/health` + `/v1/health` liveness, never One. `/ready` intended Postgres `CanConnect` but **ignores the bool**. Org ready is a member money door.

5. **StartMaxPerMinute:** Production default **20**; factory **200**. Enough for one-person one-replica links. Not enough for events or replicas. No Retry-After.

6. **Logging:** no request-body logging. Almost no logs. Migrate `LogError(ex)` can print CS passwords.

7. **Metrics/tracing/OTel:** **absent.**

8. **CI:** `pay:test` + honesty in the `pay` job. Merchant/checkout **build**, **not vitest**. No Pay image bake. GHCR is Hub.

9. **Migrations:** one context; Development auto; Production out-of-band missing. No backup. No wrap rotation. Snapshot matches HEAD (OneWebhookCiphertext); three Designers missing.

10. **TLS/cookies/HSTS/forwarded headers:** **none** in process. Required at a reverse proxy that **does not exist for Pay**. Do not add Kestrel HTTPS. Do not copy Hub Caddy onto 8081 next to ops.

11. **Other rate limits:** **none** on webhook, mint, whoami.

12. **How to solve:** §13.2 ranked P0–P2. Host holes ≠ integrator holes. **Refuse:** retarget Hub compose onto 8081.

---

*End of 06. Coordinates 2026-08-28 / `6d730d15`. Analysis only.*
