# 01 — Pay host seams after 017 layout + 018 vault/capacity

**Type:** Uncondensed evaluation of the **live** focused Pay host (`apps/lazuar-pay`) on this SHA. **Not** an implementation. **Not** a patch. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md). **Not** merchant Vite, checkout Vite, per-rail HTTP vs Hub, webhook-parse internals in depth, TypeSpec full honesty paper, or a ranked **cross-cut** P0 list (those belong to other 019 agents).

Live files are authority. [014-evals](../014-evals/01-new-pay-host.md), [016-adapters-check](../016-adapters-check/01-new-host-seams.md), and [018-evals](../018-evals/001-evals.md) are background. Where they disagree with this tree, this tree wins; the disagreement is named with evidence.

---

## Coordinates

| Field | Value |
|-------|--------|
| Title | Pay host seams after 017 folder-by-job layout + 018 vault / Test rail / payment-link capacity |
| Date | 2026-08-26 |
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `feat/018-merchant-shell` (`.git/HEAD` → `refs/heads/feat/018-merchant-shell`) |
| HEAD | `9f04ad58c578ab8df0a4e9a302a116940243d548` (`9f04ad58`) |
| HEAD subject | `fix(pay-ui): match receipts table to pay-link chrome` |
| Host | `apps/lazuar-pay/src/Lazuar.Pay` (`net10.0`, listen `http://localhost:8081` when launchSettings applies) |
| Tests | `apps/lazuar-pay/tests/Lazuar.Pay.Tests` |
| Contract (this slice only) | `packages/pay-spec/main.tsp` — only where it contradicts **these** live doors |
| Type | Analysis. How to solve is analysis, not a patch. |

Binding this host already lives under, not flipped here:

- New Pay is Consumer-0 of lazuar-one. Do not rebuild `Modules/One`. Do not add a project reference into `apps/lazuar-api`.
- IsolationTests stay red on cathedral strings.
- Steal HTTP **judgment** from Hub; Hub is museum. This paper does not re-judge Stripe/CHIP/Billplz/Xendit/Razorpay HTTP.
- Receipt ≠ tax invoice. SST/LHDN stay off the pay path.
- Buyers are not One humans. `POST /v1/pay/{token}/start` has no Bearer.

017/018 **host** commits that this paper re-reads as live C#, not as a git-log paraphrase:

- `3183de5e` — folder layout by job, not a `Gateways/` dump (017).
- `82e387b7` — vault processors independently; bind rail at mint.
- `22469d61` — local Test processor, no secrets.
- `84a3ee24` — keep local Postgres password when a configured CS already has `Password=`.
- `42a1761f` — `Database.MigrateAsync()` on Development start.
- `401e7e3c` — how many people can pay a pay link (`MaxPayers` / occupancy).

Those subjects are coordinates. Every claim below is from files opened on `9f04ad58`.

---

## Files opened

**This slice, live host**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/.env.example`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/docker-compose.pay.yml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/global.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/package.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/Lazuar.Pay.slnx`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260821152601_Initial.Designer.cs` (header / existence)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/PayDbContextModelSnapshot.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneAuthz.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneCallResult.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeResponse.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/HostedSession.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestWebhook.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs` (success/cancel URL + rail bind at start; not HTTP vs Hub)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/MoneyMath.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs` (product label on merchant reads; catalog decorative)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` (dispatch switch + TX envelope only)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/src/Lazuar.Pay/Webhooks/PspParseResult.cs`

**This slice, tests / infra**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Lazuar.Pay.Tests.csproj`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakePspHandler.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FulfillmentProbe.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` (charges-paused as a host seam)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` (InMemory TX mention only)

**Contract / tasks / CI (this slice)**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/main.tsp`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/README.md`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/tspconfig.yaml`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/.gitignore`
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/pay-spec/dist/openapi.yaml` (local emit; gitignored — not law)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/Taskfile.yml` (`pay:*` block)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/.github/workflows/ci.yml` (`pay` job)

**Background papers (not authority)**

- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/014-evals/01-new-pay-host.md` (opening)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/016-adapters-check/01-new-host-seams.md` (opening + 015 §3 quotes)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/017-catalog/00-global-psp-catalog.md` (opening; awareness catalog, not host law)
- `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/plans/018-evals/001-evals.md` (opening)

**Git coordinates only (not copied as findings)**

- `.git/HEAD`, `.git/refs/heads/feat/018-merchant-shell`, `.git/logs/HEAD` (tip + 017/018 subjects)

**Looked at and confirmed absent**

- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260824120000_FourAdaptersHostedRails.Designer.cs` — does not exist.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.Designer.cs` — does not exist.
- No `IEnumerable<IHostedRail>` in `apps/lazuar-pay/src`.
- No `ActiveProvider` **write** in `src/` (column + comment + tests that assert null only).

**Not opened as implementation sources (out of this slice)**

- `apps/lazuar-pay-merchant/**`, `apps/lazuar-pay-checkout/**`
- Hub `apps/lazuar-api/Modules/Payments/**`
- Per-rail `*Webhook.Parse` bodies beyond Test (webhook agent)
- TypeSpec full surface paper (only contradictions for checkouts / payment-links / gateways / health)

---

## What exists on this SHA (live doors, schema, composition)

### Process

One `Lazuar.Pay.csproj`, one `PayDbContext`, one test project, listen **8081**. README states the 017 law that `Program.cs` is the composition root and that a factory of rails is forbidden:

```12:27:apps/lazuar-pay/README.md
## Source layout

One `Lazuar.Pay.csproj`, one `PayDbContext`. Folders are jobs, not Hub modules. Namespaces follow folders. `Program.cs` is the composition root (`Map*`, DI). Do not add `IEnumerable<IHostedRail>`.

| Folder | Job |
|--------|-----|
| `Hosting/` | health/ready and problem JSON |
| `Identity/` | One HTTP client, whoami, org ready, One webhooks |
| `Credentials/` | PUT/GET `/v1/orgs/{id}/gateway`, list `GET /v1/orgs/{id}/gateways` |
| `Rails/` | one folder per PSP (`CreateHostedUrl` + webhook parse) |
| `Webhooks/` | shared Plane B pipeline (verify → unique event → fulfill TX) |
| `PublicPay/` | buyer GET/start (no Bearer) |
| `Money/` | fulfill + Official Receipt; `Queries/` merchant reads |
| `Catalog/`, `Checkouts/`, `Secrets/`, `Data/` | products, merchant mint, wrap, EF |
```

The live tree matches that table. There is no `Gateways/` folder and no `namespace Lazuar.Pay.Gateways`. Identity lives under `Identity/` (`namespace Lazuar.Pay.Identity` / `Identity.Client` / `Identity.OneWebhooks`). IsolationTests bans the old namespaces (quoted in Tests).

`launchSettings.json` binds 8081 and Development:

```1:13:apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:8081",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

`appsettings.json` has One’s base URL only. No `ConnectionStrings`, no `Pay:WrapKey`, no `Pay:CheckoutBaseUrl`:

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

`appsettings.Development.json` has Postgres on **5435** (with password) and the buyer-origin default:

```7:13:apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json
  "ConnectionStrings": {
    "Pay": "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres"
  },
  "Pay": {
    "CheckoutBaseUrl": "http://localhost:5179"
  }
```

There is still no `Pay:WrapKey` in either JSON file. Compose for the DB:

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
```

The host **does not load `.env` itself**. `Program.cs` is `WebApplication.CreateBuilder(args)` with no DotNetEnv package (`Lazuar.Pay.csproj` references EF Design, Npgsql, Stripe.net only). `.env.example` is a human export template. `task pay:dev` is `dotnet watch run` in `apps/lazuar-pay`.

### Composition root — DI, no `IEnumerable<IHostedRail>`, switch not factory

```37:92:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
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
    if (string.IsNullOrWhiteSpace(payCs)
        || payCs.IndexOf("Password=", StringComparison.OrdinalIgnoreCase) < 0)
    {
        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }

    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
// ... CORS 5178/5179/4178/4179 ...
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
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
```

Rails are **six concrete scoped types**, not `AddScoped<IHostedRail, …>` and not `IEnumerable<IHostedRail>`. Grep of `apps/lazuar-pay/src` for `IEnumerable` is empty. The interface exists and is small:

```5:10:apps/lazuar-pay/src/Lazuar.Pay/Rails/IHostedRail.cs
public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
```

Public start dispatches with a **switch of known names** (Test included):

```157:166:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        IHostedRail rail = name switch
        {
            PayProviders.Stripe => stripe,
            PayProviders.Chip => chip,
            PayProviders.Billplz => billplz,
            PayProviders.Xendit => xendit,
            PayProviders.Razorpay => razorpay,
            PayProviders.Test => test,
            _ => throw new InvalidOperationException("rail not configured")
        };
```

Webhook parse is the same kind of switch (`WebhookEndpoints.cs` 70–79). Adding a sixth PSP is still “folder + two switch arms + `PayProviders` + tests”, which is what the README says. That is 017 composition, still true on this SHA.

Testing environment **skips Npgsql** (`!IsEnvironment("Testing")`). `PayApiFactory` registers InMemory and `EnsureCreated()` instead of `MigrateAsync`. Development **always** migrates (findings below).

### Live HTTP doors (this slice)

| Door | Authz | What it does |
|------|--------|----------------|
| `GET /health`, `GET /v1/health` | none | `{ status: ok }` |
| `GET /ready` | none | `CanConnectAsync`; 503 `{ status: not_ready }` |
| `GET /v1/whoami` | Bearer → One `/me` | Pay projection of One |
| `GET /v1/orgs/{orgId}/ready` | member | dummy `{ ready: true }` after authz |
| `POST /v1/checkouts` | **writer** | one-off checkout; **requires `provider`**; vault row unless Test |
| `GET /v1/checkouts/{id}` | member of that row’s org | 404 before One if missing |
| `GET /v1/orgs/{orgId}/checkouts` | member | all checkouts for org, newest first, including payment-link **children** |
| `POST /v1/payment-links` | **writer** | shared URL; `max_payers` default 1; `unlimited` → null cap |
| `GET /v1/orgs/{orgId}/payment-links` | member | occupancy computed from children |
| `POST /v1/orgs/{orgId}/products` | writer | name + price row; currency MYR only |
| `GET /v1/orgs/{orgId}/products` | member | products + prices |
| `PUT /v1/orgs/{orgId}/gateway` | writer | upsert **one** `(OrgId, Provider)` vault row; does **not** write `ActiveProvider` |
| `GET /v1/orgs/{orgId}/gateway` | member | `?provider=` one row, else **list** |
| `GET /v1/orgs/{orgId}/gateways` | member | `{ org_id, processors: [...] }` including Test when allowed |
| `GET /v1/pay/{token}` | none | payment-link **first**, else checkout |
| `POST /v1/pay/{token}/start` | none | mint-or-resume child if link; else start one-off; second start returns stored URL |
| `POST /v1/webhooks/{provider}/{orgId}` | PSP signature (Test: none) | Plane B |
| `POST /v1/one/webhooks` | HMAC | pause/resume charges |
| `GET /v1/orgs/{orgId}/payments`, `/receipts`, `/receipts/{id}` | member | merchant reads |

JSON is snake_case globally (`Program.cs` 25–29).

### Schema (one context, public)

`PayDbContext` still has the Bar B tables **plus** `payment_links` and checkout `PaymentLinkId` / `SlotKey`. Unique occupancy key is Postgres-only:

```36:48:apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs
        model.Entity<CheckoutRow>(e =>
        {
            e.ToTable("checkouts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicToken).IsUnique();
            e.HasIndex(x => x.OrgId);
            e.HasIndex(x => x.PaymentLinkId);
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                e.HasIndex(x => new { x.PaymentLinkId, x.SlotKey })
                    .IsUnique()
                    .HasFilter("\"SlotKey\" IS NOT NULL");
            }
```

`OrgSettingsRow.ActiveProvider` is an unused leftover. The column is still in the snapshot. The C# comment is the 018 law:

```7:12:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
    /// <summary>Unused. Tax is out of this program. Column kept; do not read on the pay path.</summary>
    public bool? SstRegistered { get; set; }
    /// <summary>Unused. Vault save does not pick a default rail. Column kept; do not read on the pay path.</summary>
    public string? ActiveProvider { get; set; }
```

Grep of `apps/lazuar-pay` for `ActiveProvider` hits: `Rows.cs`, the FourAdapters migration, the snapshot, and **two GatewayTests assertions that it stays null**. Nothing in `src/` assigns it.

`PaymentLinkRow`:

```36:49:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
/// <summary>Shared pay-link URL. MaxPayers null is unlimited. Each payer is a child checkout.</summary>
public sealed class PaymentLinkRow
{
    public required string Id { get; set; }
    public required string OrgId { get; set; }
    public required string PublicToken { get; set; }
    public required string Provider { get; set; }
    public string? ProductId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    /// <summary>Null means unlimited payers. 1 is one person. N is a cap.</summary>
    public int? MaxPayers { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Three migrations exist:

1. `20260821152601_Initial` — PascalCase columns, `checkouts` without Provider / PaymentLinkId / SlotKey, `gateway_credentials` without webhook/public/environment, no `payment_links`. Has `Initial.Designer.cs`.
2. `20260824120000_FourAdaptersHostedRails` — `WebhookCiphertext`, `PublicMerchantId`, `Environment`, `org_settings.ActiveProvider`, `checkouts.Provider` + `ProviderSessionId`. **No Designer.cs.**
3. `20260825120000_PaymentLinkPayers` — `payment_links` table, `checkouts.PaymentLinkId` + `SlotKey`, unique filtered index `IX_checkouts_PaymentLinkId_SlotKey`. **No Designer.cs.** Snapshot includes this model.

### Independent vault + bind at mint (not one `active_provider`)

`PUT /v1/orgs/{orgId}/gateway` upserts the composite key `(OrgId, Provider)` and creates `OrgSettings` if missing, **without** setting `ActiveProvider`:

```99:140:apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs
        var wrapped = box.Protect(secret);
        var wrappedWh = box.Protect(webhookSecret);
        var row = await db.GatewayCredentials.FindAsync([orgId, provider], ct);
        if (row is null)
        {
            row = new GatewayCredentialRow { /* OrgId, Provider, ciphertexts, last4, environment */ };
            db.GatewayCredentials.Add(row);
        }
        else
        {
            row.Ciphertext = wrapped;
            // ... overwrite that provider only
        }

        if (await db.OrgSettings.FindAsync([orgId], ct) is null)
        {
            db.OrgSettings.Add(new OrgSettingsRow { OrgId = orgId });
        }
        // audit gateway.credentials.upsert
        await db.SaveChangesAsync(ct);
```

Test processor is refused on PUT (`"test processor does not take secrets"`). List synthesizes Test as `configured: true` when `PayProviders.AllowsTest(env)`.

Mint **requires** an explicit provider. Checkout create:

```53:73:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        if (!PayProviders.TryNormalize(body.Provider, out var provider))
        {
            return PayErrors.Status(400, "Bad Request", "unknown provider");
        }

        if (PayProviders.IsTest(provider))
        {
            if (!PayProviders.AllowsTest(env))
            {
                return PayErrors.Status(400, "Bad Request", "test processor is not enabled");
            }
        }
        else
        {
            var cred = await db.GatewayCredentials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrgId == orgId && x.Provider == provider, cancellationToken);
            if (cred is null)
            {
                return PayErrors.Status(400, "Bad Request", "rail not configured");
            }
        }
```

Payment-link create is the same check (`PaymentLinkEndpoints.cs` 51–71) and persists `row.Provider = provider`. Public start uses `row.Provider ?? link?.Provider`. There is no read of `OrgSettings.ActiveProvider` on the pay path.

`PayProviders.AllowsTest` is **not** Development-only:

```16:24:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();
```

Staging is not Production → Test is listed, mintable, startable, and webhook-able.

### Two mint doors, one public token namespace

**Door A — one-off checkout.** `POST /v1/checkouts` inserts a `CheckoutRow` with its own `PublicToken`, `Status = "open"`, optional `SuccessUrl` / `CancelUrl`, `Interval = "one_off"`. Buyer pays `GET/POST /v1/pay/{checkout.public_token}`.

**Door B — payment link.** `POST /v1/payment-links` inserts a `PaymentLinkRow` with its own `PublicToken`. No child yet. Buyer `GET /v1/pay/{link.public_token}` sees the **link** (capacity). `POST /v1/pay/{link.public_token}/start` with `slot_key` mints or resumes a **child checkout**.

Public GET looks at `payment_links` first, then checkouts (`PublicPayEndpoints.cs` 34–47). A theoretical token collision prefers the link.

README’s live curl still demonstrates door A (`POST /v1/checkouts`), then says “Mint a pay link with an explicit `provider`” in prose (README 55–65). Both doors are live.

### Occupancy

```1:13:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkOccupancy.cs
internal static class PaymentLinkOccupancy
{
    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;
}
```

`open` **and** `paid` occupy a seat. `expired` is named in start (409) but **never written** anywhere in `src/` (grep: only those two 409 branches). There is no expiry job. An abandoned `open` child holds the seat forever.

Create defaults: `Unlimited` → `MaxPayers = null`; else `MaxPayers ?? 1`; `max_payers < 1` → 400.

Test rail **pays on start** (no PSP HTTP, fulfill in-process):

```176:186:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            if (PayProviders.IsTest(name))
            {
                db.PspWebhookEvents.Add(new PspWebhookEventRow { /* EventId = hosted.ProviderSessionId */ });
                await fulfillment.FulfillPaidAsync(row.Id, name, hosted.ProviderSessionId, ct);
            }
            else
            {
                await db.SaveChangesAsync(ct);
            }
```

`TestHosted.CreateHostedUrlAsync` returns `CheckoutUrls.Success(...)` with provider session `test:{checkout.Id}`. For Test, landing on `?status=verifying` is already paid. For CHIP/Stripe/etc., start occupies (`open`) and paid waits for Plane B. **Started ≠ paid** except Test.

Same-slot resume: `MintOrResume` finds `(PaymentLinkId, SlotKey)` and returns the existing open row. `Start` then sees `PspRedirectUrl` and does not call the processor again (`PublicPayEndpoints.cs` 151–155). `PaymentLinkTests.Same_slot_start_twice_does_not_take_two_seats` locks that for CHIP (`Psp.SendCount == 1`, `taken_count == 1`).

Capacity check is **count then insert** with no `BEGIN` / `SELECT FOR UPDATE` / serializable isolation (quoted in Bugs).

### Catalog is a label store

`POST /v1/orgs/{orgId}/products` writes `ProductRow` + `PriceRow`. Currency must be MYR (`"Bar B currency is MYR"`). Interval is stored as a string, default `one_off`.

Checkout and payment-link mint accept `product_id` as a **trimmed string**. They do not load the product, do not copy `PriceRow.Amount` / currency / interval, do not check `Product.OrgId`. Amount always comes from the mint body. Checkout interval is hard-coded `"one_off"`. List endpoints join product **name** for `label` only (`CheckoutEndpoints` 141–163, `PaymentLinkEndpoints` 137–152, `PaymentQueryEndpoints` 32–41). That join is `Where(p => productIds.Contains(p.Id))` **without** `OrgId`.

### Writer vs member

```45:71:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(...)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null) return denied;

        Bearer.TryGet(request, out var authorization);
        var who = await one.GetWhoamiAsync(...);
        var role = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId)?.Role;
        if (role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
        return null;
    }
```

Member is One `POST tenants/{orgId}/authz/check` with `relation: "member"`. Writer is **a second hop** to `/me` and a string compare on `tenants[].role`. `is_platform_admin` is not a bypass. Tenant `status` is not checked. There is no writer authz relation.

Mint/PUT/create-product = writer. List/get = member. Public pay = no Bearer. README 69: “`POST /v1/checkouts` requires writer.” Payment-link create also requires writer; that sentence is missing from README but true in code.

### CORS

Eight localhost origins: merchant `5178` / preview `4178`, checkout `5179` / preview `4179`, each as `localhost` and `127.0.0.1`. Not ops `:3003`, not portal `:3004`, not Caddy hostnames. `AllowAnyHeader` + `AllowAnyMethod`. No `AllowCredentials`. `CorsTests` lock 5178, 5179, 4179 allowed; 3003 and 3004 denied.

### Wrap key, CheckoutBaseUrl, PublicBaseUrl

`SecretBox.LoadKey`: empty `Pay:WrapKey` throws **unless** environment name is exactly `"Testing"`, in which case the wrap key is `SHA256("lazuar-pay-dev-wrap-key")` (quoted in Bugs). That is the git-default wrap key. It is not in `appsettings`. Production **and Development** require a 32-byte base64 key.

`CheckoutUrls.Base`: config `Pay:CheckoutBaseUrl`, else Testing fallback `http://localhost:5179`, else throw. Development JSON supplies `http://localhost:5179`. Payment-link children **ignore** merchant success URLs; they get `baseUrl + "/c/" + link.PublicToken + "?status=verifying"`. One-off checkouts still persist `body.SuccessUrl` and Stripe uses `CheckoutUrls.Success` which prefers that merchant URL.

`Pay:PublicBaseUrl` is Billplz’s public https callback (README 67; `BillplzHosted` reads it). Not the buyer return origin. Webhook agent owns depth.

### Success URL vs paid

README is still accurate:

```67:67:apps/lazuar-pay/README.md
… A second `POST /v1/pay/{token}/start` on an open checkout returns the stored hosted URL (no second processor session). Success URL is not paid; `:5179` polls `?status=verifying`.
```

Live exception: Test rail fulfills in the start request **and** uses the verifying URL as the “hosted” redirect. For Test, verifying is already paid. For the four real PSPs, success/verifying is not paid until a verified webhook.

### Idempotency

Checkout create: `Idempotency-Key` header, else `body.idempotency_key`. `CheckoutStore.CreateAsync` looks up `(OrgId, Key)` and returns the stored checkout. Replay still goes through `CheckoutEndpoints.Create` which returns **201**. No request-hash. Payment-link create has **no** idempotency key. Public start is idempotent per checkout via stored `PspRedirectUrl`, and per link via `slot_key`.

### InMemory vs real TX around fulfill (mention only)

`WebhookEndpoints` wraps insert-event + `FulfillPaidAsync` in `BeginTransactionAsync` (lines 143–154). `PayApiFactory`:

```28:31:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs
    /// <summary>
    /// InMemory BeginTransaction is a no-op. H25/G12 proof uses FulfillmentProbe,
    /// which throws before Fulfillment.SaveChanges so the event row is not committed.
    /// </summary>
```

Hermetic tests therefore cannot prove rollback of a real Postgres transaction. Test-rail start fulfill is **not** inside that webhook TX; it is `FulfillPaidAsync`’s own `SaveChanges`. Webhook agent owns parse/verify depth. This paper only records the host envelope.

---

## 017/018 host delta (re-verified, not copied from git log)

Compare **this SHA’s files** to what [016/01](../016-adapters-check/01-new-host-seams.md) froze on `c621ceba` as 015 §3 law. 016 is background. Live wins.

### 017 — folder-by-job (still true)

| 016-era layout (016 paper) | Live `9f04ad58` |
|----------------------------|-----------------|
| `Gateways/` for PUT/GET + hosted types | `Credentials/GatewayEndpoints.cs`; rails under `Rails/{Psp}/` |
| `One/` for whoami / member / One webhooks | `Identity/` (`Client/`, `OneWebhooks/`, `WhoamiEndpoints`, `OrgReadyEndpoints`) |
| Maps and errors inline or mixed | `Hosting/HealthEndpoints`, `Hosting/PayErrors` |
| IsolationTests banned Hub adapters | Still bans them, **plus** `IEnumerable<IHostedRail>`, `namespace Lazuar.Pay.Gateways`, `namespace Lazuar.Pay.One;` |

README’s folder table matches `list_dir` of `src/Lazuar.Pay`. Namespaces follow folders. `Program.cs` remains the only composition root. **Do not add `IEnumerable<IHostedRail>`** is both README and IsolationTests.

016’s “switch of five known names” grew a sixth arm (`Test`) and a sixth DI line. Still a switch, still not a factory.

### 018 — independent vault (016 §3.2 / §3.4 overruled)

016 quoted 015 law: *“One active rail per org”*, *“PUT gateway sets `active_provider`. Public start uses it.”*, *“`POST /v1/checkouts`: store `Provider` only at start, not at create.”*

Live disagrees, all three:

1. `PUT` does not assign `ActiveProvider`. GatewayTests `Put_and_get_does_not_echo_secret` and `List_returns_all_five_and_put_does_not_default_pay_links` assert `db.OrgSettings.Single().ActiveProvider, Is.Null` after two PUTs (stripe + chip) and `GatewayCredentials.Count() == 2`.
2. `GET /v1/orgs/{id}/gateways` returns **all** listed processors (5, or 6 with Test), each with `configured` true/false. Bare `GET /gateway` (no query) **is the list** (`GatewayEndpoints.Get` 158–160).
3. `POST /v1/checkouts` **requires** `provider` and stores it on the row at mint (`CheckoutSession.Provider = provider`). `Create_without_provider_is_400`. Payment links persist `Provider` on the **link** row; children copy `link.Provider` in `MintOrResume`.

README 65: “Saving a vault does not pick a default. Mint a pay link with an explicit `provider` that already has keys.” That sentence matches live and contradicts 016.

`ActiveProvider` remains a column so the FourAdapters migration is not dropped. It is not a door.

### 018 — Test rail

`Rails/Test/TestHosted.cs` + `TestWebhook.cs`. No vault row. `AllowsTest = !IsProduction()`. Start fulfills. `TestRailTests.Mint_and_start_pays_without_keys`. PUT `provider=test` is 400. List still shows Test as configured in Testing/Development.

### 018 — payment-link capacity

New table, new merchant door, new public mint-or-resume, filtered unique `(PaymentLinkId, SlotKey)`, occupancy helper. Migration `20260825120000_PaymentLinkPayers`. Tests in `PaymentLinkTests`. pay-spec `main.tsp` has **zero** `payment-links` / `max_payers` / `slot_key` strings (grep).

### 018 — Development migrate + password keep

`MigrateAsync` on `IsDevelopment()` (Program.cs 74–78). Connection string is kept if it already contains `Password=`; otherwise the **entire** string is replaced with localhost/postgres/postgres (Program.cs 49–54). `.env.example` still documents `ConnectionStrings__Pay="…Password=postgres"`. The host does not parse `.env`; the keep is “do not clobber a CS that already has a password.”

### What 014/016 still get right on this SHA

- Listen 8081, One on 8080, CORS not ops/portal.
- One `PayDbContext`, Postgres 5435, IsolationTests ban MediatR / BuildingBlocks / `lazuar-api` refs / org/user/member tables.
- Writer on mint (016 already required this). Member on get/list.
- Wrap-rails capability string `hosted_link`.
- Success URL is not paid (except Test, which 016 did not have).
- `SecretBox` Testing fallback vs required outside Testing (014/016 already named the SHA256 default).

### What 014/016 get wrong if quoted as current

- 016: one active rail / PUT sets `active_provider` / provider stored only at start — **false** on `9f04ad58`.
- 016: `GET /gateway` returns the active rail — live returns a **list** unless `?provider=`.
- 014: CORS “four localhost literals” — live is **eight** (preview ports + 127.0.0.1).
- 014: README “in-memory checkout” — already false then; still false. Current README is honest about Postgres and pay links.
- 018-evals (`plans/018-evals/001-evals.md`) is a **product** paper (kernel vs escrow vs WhatsApp SME). It is not a host-seam inventory. It correctly says the host is a hosted cashier, not a machine-key kernel. That is still true (no `lzr_sk_`, no outbound `payment.completed`). Out of this slice except as background.

---

## Bugs (each: evidence, impact, how to solve)

### B1. Payment-link capacity is check-then-act; two slots can overfill (P0)

**Evidence.** `MintOrResume` counts occupying children, then inserts, with no transaction, no row lock on `payment_links`, and no unique constraint on “Nth seat”:

```236:264:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var taken = await db.Checkouts.CountAsync(
            x => x.PaymentLinkId == link.Id && (x.Status == "open" || x.Status == "paid"),
            ct);
        if (PaymentLinkOccupancy.IsFull(link.MaxPayers, taken))
        {
            return (null, PayErrors.Status(409, "Conflict", "This pay link is full"));
        }
        // ... new CheckoutRow { Status = "open", SlotKey = slot, ... }
        db.Checkouts.Add(row);
        await db.SaveChangesAsync(ct);
```

The only unique index is `(PaymentLinkId, SlotKey) WHERE SlotKey IS NOT NULL`. Two **different** slot keys both seeing `taken = 0` on `max_payers = 1` both insert. Postgres will accept both. Then two CHIP/Stripe sessions exist, two webhooks can fulfill, two Official Receipts, cap of one.

`PaymentLinkTests.Two_people_can_pay_a_link_of_two` is sequential, not concurrent. InMemory does not even install the slot unique index (`PayDbContext` 43–48, `ProviderName` contains Npgsql only).

**Impact.** Capped links are not a real cap under concurrency. This is money: extra hosted sessions, extra fulfill, extra `RCPT-`. Test rail makes it worse (B4) because start **is** fulfill.

**How to solve.** Serialize occupancy on the **parent row**. In one transaction: `SELECT … FROM payment_links WHERE id = $id FOR UPDATE` (or `UPDATE payment_links SET …` with a stored `taken` column and `WHERE taken < max`), then insert the child, then commit. Alternatively a seat table `UNIQUE (payment_link_id, seat_n)` with `seat_n` in `1..max`. Catch unique violations and return 409, never 500. Add a hermetic **Postgres** concurrency test (InMemory cannot). Do not “fix” this only in the Vite slot_key generator — two browsers are enough.

### B2. Same-slot race is an unhandled unique violation (P1)

**Evidence.** Same method: `FirstOrDefault` by `(PaymentLinkId, SlotKey)`, then insert. The unique index exists **only on Npgsql**. Concurrent same-slot starts: both miss, one insert wins, the other `SaveChanges` throws `DbUpdateException` with no catch in `MintOrResume` or `Start` (Start’s catch is `InvalidOperationException` and `StripeException` only, lines 194–202).

**Impact.** Buyer 500 instead of resume. On InMemory, both rows can persist (no index) → two seats, `Same_slot_start_twice` would not catch a parallel run.

**How to solve.** Catch the unique violation, re-load the existing row, resume (same as the hit path). Put the unique index on **all** providers that tests use, or stop using InMemory for occupancy. The B1 parent lock also serializes this.

### B3. `open` occupies forever; `expired` is dead; started ≠ paid for real rails (P1)

**Evidence.** Occupancy counts `open` or `paid`. Nothing in `src/` writes `status = "expired"` (grep: only 409 if already expired). No timeout worker. CHIP start persists `PspRedirectUrl` and leaves `open` (`Start` non-Test `SaveChanges`). A buyer who never pays still holds the seat. `GetLink` when full and `max_payers == 1 && paid >= 1` shows the paid row; when full with **zero** paid it returns `status: "full"` (`PublicPayEndpoints.cs` 66–75). Real buyers see full while money never arrived.

**Impact.** One abandoned CHIP tab fills a `max_payers = 1` link. Merchant list shows `taken_count = 1`, `paid_count = 0`, `status = full`. Test rail hides this because start pays (B4).

**How to solve.** Product choice, then code: (a) occupy only `paid` (then two people can start a max=1 link and both reach the PSP — usually wrong), or (b) occupy `open` **with a TTL** (expire `open` children, free the seat, ignore late webhooks or treat them as 409), or (c) occupy on start but let the merchant / buyer release. Whatever you pick, write `expired` for real and test CHIP (not Test) “start then walk away.” Do not document started as paid.

### B4. Test rail start is unsigned fulfill; `AllowsTest` is every non-Production env (P1, P0 if Staging is a real env)

**Evidence.** `PayProviders.AllowsTest` is `!env.IsProduction()` (quoted above). `TestWebhook.Parse` has **no** secret; it accepts any JSON with optional `id` / `checkout_id`. `WebhookEndpoints` skips vault for Test when `AllowsTest`. Start path fulfills without Plane B verify (quoted B1 section). Anyone who can `POST /v1/pay/{token}/start` with a new `slot_key` on a Test link mints a paid checkout, journal, and `RCPT-`. Unlimited Test links are unbounded fake receipts. Capped Test links: B1 + B7 burn seats **and** mint receipts.

**Impact.** Intended for local dogfood (`TestRailTests`). Staging / any `ASPNETCORE_ENVIRONMENT` other than `Production` or `Testing` exposes the same door. Production correctly 400s Test mint (`AllowsTest` false).

**How to solve.** Narrow `AllowsTest` to `IsDevelopment() || IsEnvironment("Testing")` (or an explicit `Pay:EnableTestProcessor`). If Staging must dogfood, require a process secret on `POST /v1/webhooks/test/{orgId}` and **do not** auto-fulfill on start (make Test go through the same webhook TX as others, with a shared secret). Cap Test fulfill per org. Never enable Test in Production (already true).

### B5. Development WrapKey: docs lie, first vault PUT throws, host still starts (P1)

**Evidence.** `SecretBox.LoadKey` throws `Pay:WrapKey is required` unless environment is `"Testing"` (lines 38–44). `SecretBoxTests` covers Production-missing (throws) and Testing-empty (SHA256 default). There is **no** Development test. `appsettings.json` / `appsettings.Development.json` have no WrapKey. `.env.example`:

```8:9:apps/lazuar-pay/.env.example
# 32-byte base64 wrap key for BYOK. Dev has a fallback; production must set this.
# Pay__WrapKey=
```

README 67 is the opposite (and matches C#): “`Pay__WrapKey` is required outside Testing.”

`Program.cs` never calls `LoadKey` at boot. `task pay:dev` is Development. Health works. First `PUT /v1/orgs/{id}/gateway` calls `box.Protect` → unhandled `InvalidOperationException` → 500. There is no `ValidateOnStart`.

The git-default wrap key is only:

```46:46:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
```

Anyone can derive it. That is acceptable **inside Testing**. It is not a Development fallback, despite `.env.example`.

**Impact.** Local dogfood of BYOK follows `.env.example`, fails on vault save. Production missing WrapKey fails the same way on first PUT (fail-closed is correct; fail-at-boot would be better). Wrapping with the Testing default and then running Development against the same DB cannot Unprotect.

**How to solve.** Pick one story and make files agree. Recommended: keep fail-closed outside Testing; **generate or require** WrapKey in Development (`dotnet user-secrets`, or a **local-only** `appsettings.Development.json` that is gitignored, or print a clear 503 problem JSON from PUT instead of 500). Delete “Dev has a fallback” from `.env.example`. Optionally `IValidateOptions` / `ValidateOnStart` so `task pay:dev` dies before the first merchant click. Do **not** ship the SHA256 string as a Development default into a shared `lazuar_pay` — if you do, treat those ciphertexts as compromised.

### B6. Connection-string “password keep” replaces the whole CS when `Password=` is absent (P1)

**Evidence.**

```49:54:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
    var payCs = builder.Configuration.GetConnectionString("Pay");
    if (string.IsNullOrWhiteSpace(payCs)
        || payCs.IndexOf("Password=", StringComparison.OrdinalIgnoreCase) < 0)
    {
        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }
```

A CS that uses `Pwd=`, `postgres://`, or trust/peer auth is treated as “no password” and **thrown away**, including `Host=`. The keep only preserves strings that already contain the substring `Password=`. The host does not load `.env`; if a human exports `ConnectionStrings__Pay` without that substring, they silently talk to localhost/postgres/postgres.

**Impact.** Wrong database, or a surprising local default, instead of a connect error to the intended host. The 018 intent (“keep the password that is already in the CS”) is implemented as a blunt all-or-nothing replace.

**How to solve.** If CS is non-empty, use it. If it fails to connect, 503 `/ready` and log. If you want a laptop default, apply it **only** when CS is null/whitespace — not when `Password=` is missing. Accept `Pwd=`. Do not rewrite `Host`.

### B7. Public `slot_key` is a client-supplied seat id; capped links can be griefed (P1)

**Evidence.** `NormalizeSlotKey` accepts any trimmed 8–128 character string. `MintOrResume` requires it on link start. There is no rate limit, no cookie binding, no signed slot. The public token **is** the capability. `PaymentLinkTests.Start_link_without_slot_key_is_400` only. A client that knows the pay URL can `POST /start` with `slot-aaaa-01`, `slot-aaaa-02`, … until `IsFull`.

**Impact.** For `max_payers = 1` (the default), one unsolicited start fills the link (B3 if CHIP, B4 if Test). Unlimited links: unbounded child checkouts and, on Test, unbounded receipts.

**How to solve.** Server-mint the slot (GET link returns a one-time `slot_key` cookie / hidden field bound to a reservation row created under the B1 lock). Or require a merchant-generated payer token. Rate-limit `POST /v1/pay/{token}/start` per token/IP. For Test, also B4.

### B8. Checkout idempotency is racy, body-blind, and always 201 (P1)

**Evidence.** `CheckoutStore.CreateAsync` (lines 9–53): lookup key; if key exists and checkout exists, return it; else insert checkout **and** key in one `SaveChanges`. No transaction around lookup+insert. Two concurrent `Idempotency-Key: k1` both miss, second hits PK `(OrgId, Key)` unhandled → 500. If the key row exists but the checkout was deleted, the code falls through and tries to insert a **duplicate key**. Replay returns 201 from `CheckoutEndpoints.Create` line 99 (`statusCode: 201`) even when `CreateAsync` returned the old row. The key is not hashed to `{amount, currency, provider, org}`. `Create_idempotent_on_key` is sequential and uses the same body.

Payment-link create has **no** idempotency at all.

**Impact.** Double-click mint can 500. Same key with a different amount silently returns the first checkout (classic Stripe footgun if you do not store the fingerprint). Merchant UI that treats 201 as “new” will lie on replay.

**How to solve.** Unique `(org, key)` already exists — catch duplicate, re-read, return the original with **200** if the fingerprint matches, **409** if it does not. Store a hash of the canonical body. Add the same header to `POST /v1/payment-links`. Test concurrent replay against Postgres.

### B9. `MigrateAsync` on Development start can crash the host; Cors/Health tests boot that host (P1)

**Evidence.** Program.cs 74–78: `if (IsDevelopment()) MigrateAsync()`. No try/catch. Failures that kill `task pay:dev`:

- Postgres down (5435 not up).
- `__EFMigrationsHistory` empty but tables exist (`EnsureCreated` leftover, or a restored dump) → `CreateTable` / `AddColumn` “already exists”.
- History thinks Initial is applied but FourAdapters/PaymentLinkPayers columns were added by hand → `AddColumn` fails.
- History ahead of code, or partial apply.
- Dirty `SlotKey` duplicates before `IX_checkouts_PaymentLinkId_SlotKey` (less likely on a clean 018 DB; real on a hand-edited one).

`CorsTests` and `HealthTests.Health_returns_ok` / `V1_health_returns_ok` use `new WebApplicationFactory<Program>()` **without** `UseEnvironment("Testing")`. ASP.NET Core’s factory defaults to **Development**. That path registers Npgsql and runs `MigrateAsync` against `appsettings.Development.json`’s `localhost:5435`.

CI `pay` job (`.github/workflows/ci.yml` 96–112) runs `dotnet test apps/lazuar-pay/Lazuar.Pay.slnx` with **no Postgres service**.

**Impact.** `task pay:test` / CI either (a) fail when 5435 is down, or (b) **apply pending migrations to the dogfood DB** when 5435 is up — including `PaymentLinkPayers` as a side effect of a CORS test. That is the opposite of `PayApiFactory`’s isolated InMemory database.

**How to solve.** Point Cors/Health tests at `PayApiFactory` (or `UseEnvironment("Testing")` plus InMemory). Never `MigrateAsync` from a test that is not about migrations. For the host: keep auto-migrate as a **Development convenience**, but catch and log a clear “pay-db schema mismatch; run `task pay:db:migrate`” instead of crashing, **or** document that `pay:dev` requires `pay:db:up` and a clean history. Add an explicit migration test against Testcontainers Postgres if you care about PaymentLinkPayers on a real engine. Do not `EnsureCreated` on the laptop DB.

### B10. `CheckoutUrls.Base` throw in `MintOrResume` is not caught (P1)

**Evidence.** `MintOrResume` calls `CheckoutUrls.Base(config, env)` **before** the `Start` try/catch around `CreateHostedUrlAsync`. `CheckoutUrls.Base` throws `Pay:CheckoutBaseUrl is required` outside Testing when config is empty. Production without `Pay__CheckoutBaseUrl` → unhandled 500 on first **payment-link** start. One-off checkouts with merchant `success_url` set never hit `Base` until a rail that calls `CheckoutUrls.Success` with a blank checkout success URL.

Development JSON has the localhost default, so laptop payment-links work. Production must remember the env var.

**How to solve.** Validate `Pay:CheckoutBaseUrl` at boot outside Testing (same as WrapKey). In `MintOrResume`, map the throw to 503 problem JSON like Start already does for “callback base”. Do not hard-code `http://localhost:5179` in Production.

### B11. Catalog `product_id` is not a foreign key; amount is not the catalog (P1 product, P2 tenancy)

**Evidence.** Mint assigns `ProductId = body.ProductId.Trim()` with no `Products` lookup (`CheckoutEndpoints.cs` 87, `PaymentLinkEndpoints.cs` 93). Amount is `body.Amount`. List label:

```146:150:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var names = productIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
```

No `p.OrgId == orgId`. A guessed/leaked product id from another org prints that name on this org’s list. Catalog create rejects non-MYR; checkout mint accepts any currency string (`ToUpperInvariant` only). Catalog `interval` is stored and never copied (`Checkout` always `one_off`).

**Impact.** Merchants can mint “Seat RM10” against a RM99 product. SST/Bar B MYR is only a catalog-create check. Cross-org name leak requires knowing a GUID.

**How to solve.** If catalog is real: load `(orgId, productId)`, copy amount/currency/interval, 404 if missing. If catalog is a label: stop taking `product_id` as a money input, still filter names by `OrgId`, and say so in README. Either way, close the honesty gap (Gap G1). Tests today only `Create_product_as_owner` / `Member_cannot_create_product`.

### B12. Writer role is a second source of truth and ignores `status` / platform admin (P2, P1 if One drifts)

**Evidence.** Member = OpenFGA-style `authz/check` `relation=member`. Writer = `/me` `tenants[].role` in `{owner, admin}`. `MemberGate` does not read `WhoamiTenant.Status`. `is_platform_admin` is mapped (`OneMeMapper`) and unused in the gate. OrgReady is member-only and always `{ ready: true }` after the check (`OrgReadyEndpoints.cs` 25).

**Impact.** A user who is `member` in authz and `owner` in `/me` (or the reverse) gets a different answer than One’s graph. Suspended tenant with stale `/me` can still PUT keys if authz still allows member. Platform admin without a tenant row cannot mint (probably intended).

**How to solve.** One writer relation (`admin` / `owner`) on the same `authz/check` hop, **or** treat `/me` as the only source and drop the extra call. Check `status == active`. Decide whether platform admin is a Pay writer (Refuse says Pay is not an IdP — do not invent a Pay admin). Tests: `Member_cannot_create_checkout`, `Member_cannot_put_gateway`, `Member_cannot_create_product`. **Missing:** member cannot create **payment-link**.

### B13. GET `/v1/orgs/{orgId}/checkouts` mixes one-off mints and occupancy children (P2)

**Evidence.** List is `Where(x => x.OrgId == orgId)` with no `PaymentLinkId == null` filter (`CheckoutEndpoints.cs` 137–140). Every `MintOrResume` child appears as a checkout with its own `public_token`. Merchant “checkouts” and “payment-links” views double-count the same money.

**Impact.** Wrong totals in any client that sums both lists (merchant Vite is out of this slice; the host door is still wrong for a kernel client).

**How to solve.** Filter children out of the checkout list (`PaymentLinkId == null`), or mark `{ kind: "one_off" | "link_child", payment_link_id }` and document it in pay-spec. Lock it with a test that mints a link, starts one slot, and asserts list shape.

### B14. Child checkout public tokens are a second pay URL (P2)

**Evidence.** Children get a 64-hex `PublicToken`. Public GET tries payment_links first, then checkouts. `POST /v1/pay/{childToken}/start` takes the checkout branch: **no** `slot_key`, no occupancy re-check (seat already taken). Occupancy is not re-validated on that path.

**Impact.** Bookmarking the child token bypasses the link’s GET `full` view. Usually OK (resume). Combined with B1, extra children each have a working URL.

**How to solve.** Either do not issue child public tokens (pay only via link token + slot), or treat child tokens as aliases that still load the parent occupancy. Document the namespace: link tokens and checkout tokens share `/v1/pay/{token}`.

---

## Gaps (each: intended vs live, how to close)

### G1. Catalog is decorative on the money path

**Intended (011 / README folder table):** `Catalog/` is products. Merchant mints from a product.

**Live:** products persist; mint money is the JSON `amount`. `product_id` is a label. Catalog tests do not mint a checkout from a product. pay-spec `Product` is `{ id, org_id, name }` — no prices — while live list returns `prices: [{ id, amount, currency, interval }]`.

**How to close.** Either wire amount/currency/interval from `PriceRow` at mint (and test it), or README/spec: “catalog is a name tag; price is always on the mint body.” Stop claiming Bar B MYR as a pay-path invariant while checkouts accept `USD`.

### G2. Two mint doors, spec knows one, README curl is the old one

**Intended (018):** shared pay link with capacity is the merchant object; one-off checkout may remain as the kernel object.

**Live:** both `POST /v1/checkouts` and `POST /v1/payment-links` work. `packages/pay-spec/main.tsp` has Checkouts + PublicPay, **no** PaymentLinks interface, no `slot_key` on `StartPayRequest`, no `provider` on `CreateCheckoutRequest`:

```38:72:packages/pay-spec/main.tsp
model CreateCheckoutRequest {
  org_id: string;
  amount: decimal;
  currency?: string;
  success_url?: string;
  cancel_url?: string;
  idempotency_key?: string;
}
// ...
model StartPayRequest {
  name?: string;
  email?: string;
}
```

Live create without provider is 400 (`unknown provider`). Live link start without `slot_key` is 400. TypeSpec create is documented as 200-ish default; live returns 201. Dist `openapi.yaml` is gitignored and even staler (checkout “fixture”, “Requires Bearer + member”, **no Gateways paths** in the copy opened on this machine).

**How to close.** Grow `main.tsp` when the door exists (`packages/pay-spec/README.md` line 13). Add PaymentLinks, `provider`, `product_id`, `slot_key`, occupancy fields, list routes, 201, writer vs member. Do not treat `dist/openapi.yaml` as law. README curl: show `POST /v1/payment-links` as the 018 mint, keep checkouts as the one-off/kernel door if you still want two.

### G3. `GET /gateway` shape vs list vs TypeSpec

**Intended (016):** GET returns the **active** rail’s `GatewayView`. **Intended (018):** there is no active rail; list everything.

**Live:** `GET /v1/orgs/{id}/gateways` is the list. `GET /v1/orgs/{id}/gateway` without query **calls List** (same JSON `{ org_id, processors }`). `?provider=stripe` returns a single `GatewayView`. TypeSpec:

```173:181:packages/pay-spec/main.tsp
interface Gateways {
  @put @route("/orgs/{orgId}/gateway") put(...): GatewayView;
  @get @route("/orgs/{orgId}/gateway") get(@path orgId: string): GatewayView;
}
```

No `gateways` plural, no `?provider=`, no Test, no `key_id`/`key_secret`. Live PUT allows Razorpay split fields. Live list length is 6 in Testing (`GatewayTests.List_returns_all_five_and_put_does_not_default_pay_links` — name says five, assertion is 6 because Test).

**How to close.** Spec the plural list as the 018 door. Keep singular GET only with required `provider` query (or delete it). Tests: rename “five” to “listed processors.” PUT body: document `key_id`/`key_secret`.

### G4. Health doors vs spec vs `/ready`

**Live:** `GET /health` and `GET /v1/health` both `{ status: ok }` without One (`HealthEndpoints.cs` 9–10). `GET /ready` hits the DB. TypeSpec Health is only `@route("/v1")` + `@route("/health")` → `/v1/health`. Dist/CI compile will not mention `/health` or `/ready`.

**How to close.** Spec both liveness routes or drop one. Spec `/ready`. Keep `/health` skipping One (already tested in `HealthTests.Health_does_not_call_one` via PayApiFactory).

### G5. CheckoutBaseUrl is a laptop hardcoded default, not a production story

**Intended:** buyer return origin is config, not Billplz callback (`README` 67, `CheckoutUrls.cs`).

**Live:** Development JSON hard-codes `http://localhost:5179`. Testing factory sets `http://pay-checkout.test.example`. Production must set `Pay__CheckoutBaseUrl` or throw (B10). CORS allows 5178/5179/4178/4179 only — a deployed checkout origin is not in that list.

**How to close.** CORS origins from config (`Pay:CorsOrigins`). CheckoutBaseUrl from the same production config. Preview ports can stay Development-only. Do not expand CORS to ops/portal (tests correctly deny 3003/3004).

### G6. IsolationTests cathedral list vs 018 objects

**Intended:** stay Consumer-0; no Hub types; no rail factory.

**Live (good):**

```5:17:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
    static readonly string[] BannedSrc =
    [
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api",
        "application_fee", "TransferData", "transfer_data",
        "ChipWebhookRegistrar", "PublicDnsFallback",
        "Lhdn", "MyInvois", "UBL", "XAdES", "Irbm",
        "IEnumerable<IHostedRail>",
        "namespace Lazuar.Pay.Gateways",
        "namespace Lazuar.Pay.One;"
    ];
```

Scans `src/**/*.cs` and both csproj. Bans `ToTable("organizations"|"users"|"members")`. Vite `package.json` must not reference `@repo/api-types-ts`.

**Gaps:** does not scan `tests/**/*.cs` for Hub adapters; does not ban **writes** to `ActiveProvider`; does not require `PayProviders.All` to match DI registrations; does not mention PaymentLinks; the test name `Vite_apps_do_not_use_hub_types` only checks package.json. README may say “Hub” (it does); IsolationTests does not grep that word in src, which is correct.

**How to close.** Add a grep that `ActiveProvider` is not assigned in `src/`. Optionally assert `Program.cs` still has no `IEnumerable<IHostedRail>`. Do not ban the word Hub in README.

### G7. pay-spec vs live for this slice (inventory)

| Topic | live | `main.tsp` |
|-------|------|------------|
| `POST /v1/checkouts` authz | writer | comment says writer (dist says member — ignore dist) |
| `provider` on create | required | **missing** → live 400 |
| `product_id` | optional label | missing |
| create status | 201 | default 200 |
| `GET /v1/orgs/{id}/checkouts` | exists | **missing** |
| `POST /v1/payment-links` + list | exists | **missing** |
| `slot_key`, occupancy fields on PublicPay | exist | **missing** |
| `GET /v1/orgs/{id}/gateways` | exists | **missing** |
| `GET /gateway` no query | list | `GatewayView` |
| Test processor | listed non-prod | not in allow-list comments |
| `/health` (no v1), `/ready` | exist | **missing** |
| `/v1/health` | exists | exists |
| catalog body/prices | amount required, prices on list | create has no body; Product has no prices |
| payments/receipts | exist | **missing** (Money/Queries; edge of slice) |

**How to close.** Same as G2/G3/G4: grow `main.tsp` to the live doors of **this** host, then compile. Do not import `packages/api-spec`.

### G8. No merchant GET-by-id for payment links; no pagination

**Live:** list only, unbounded `ToListAsync` for checkouts, links, products, charges, documents. Fine for dogfood. Not a kernel.

**How to close.** `GET /v1/payment-links/{id}` member-gated. Cursor/limit when a second app exists. Out of 018 if the Vite only uses list.

### G9. Leftover columns and unused status

`SstRegistered`, `ActiveProvider` kept on purpose (comments). `expired` named but never set. `OneMeResponse.ActiveRole` is not mapped. Harmless residue.

**How to close.** Do not spend a migration to drop columns this program. Either implement expiry (B3) or delete the `"expired"` branches so readers do not think a worker exists.

### G10. `.env` is documentation; WrapKey/CheckoutBaseUrl/OneWebhookSecret are commented

Laptop path is `appsettings.Development.json` + compose password `postgres`. `.env.example` comments WrapKey, StripeWebhookSecret, PublicBaseUrl, CheckoutBaseUrl, OneWebhookSecret. README says WrapKey is required outside Testing. Developers who only copy `.env.example` and export it still lack WrapKey (B5) and One webhook HMAC (503 `"One webhook secret missing"` — `OneWebhookEndpoints.cs` 24–28).

**How to close.** A Development README block: `pay:db:up`, export WrapKey, export OneWebhookSecret if you want pause-on-suspend. Or user-secrets. Do not teach a WrapKey fallback that C# does not implement.

### G11. FourAdapters / PaymentLinkPayers have no Designer.cs

EF still applies `Up()`. Snapshot matches PaymentLinkPayers. Next `dotnet ef migrations add` should work from the snapshot. `dotnet ef migrations script` from Initial still has Up() methods. Hygiene gap, not a runtime bug, unless someone uses Designer-based tooling that expects every migration to have one.

**How to close.** Generate Designers when you next touch migrations, or leave them if `task pay:db:migrate` is the only tool.

---

## Tests that lock this slice vs missing

### What is locked (hermetic `PayApiFactory` unless noted)

**Isolation / Consumer-0**

- Host and test csproj do not contain `lazuar-api`, `Modules.`, `BuildingBlocks`, `MediatR`, `Lazuar.Api`.
- `src/**/*.cs` does not contain the BannedSrc cathedral list including `IEnumerable<IHostedRail>` and old namespaces.
- No `organizations` / `users` / `members` tables.
- No csproj path to `apps/lazuar-api`.
- Merchant/checkout `package.json` do not depend on `@repo/api-types-ts`.

**Health / CORS**

- `HealthTests.Health_does_not_call_one` (PayApiFactory).
- `CorsTests`: 5178, 5179, 4179 allowed; 3003 ops and 3004 portal **denied**.
- `HealthTests.Health_returns_ok` / `V1_health_returns_ok` use **non-hermetic** `WebApplicationFactory` (B9).

**Vault / independent processors**

- Member cannot PUT; PUT requires `webhook_secret`; secret not echoed; `capability = hosted_link`; `ActiveProvider` stays null after PUT.
- List length 6 in Testing; two PUTs → two credential rows; Test PUT 400; Chip brand id required; Billplz collection id required; Razorpay colon secret required; unknown provider 400; member can GET metadata.

**Checkout mint (door A)**

- 401 without Bearer (no One call).
- Create+get; other org 403; get other org’s id 403; get missing 404 **without** One.
- Idempotent sequential key.
- Currency default MYR; no provider 400; unknown provider 400; unconfigured rail 400; Test without vault 201; amount 0 → 400; member cannot create; list newest first; list other org 403.

**Payment links (door B)**

- Create default `max_payers = 1`, `unlimited = false`, `remaining = 1`.
- Unlimited → null max/remaining on public GET.
- `max_payers = 0` → 400.
- Create without Bearer 401.
- List newest first with remaining.
- List other org 403.
- Two sequential Test starts on max=2, third 409 full; GET with occupying slot shows `paid` (because Test fulfills); other slot sees `full`.
- Same CHIP slot twice → one PSP HTTP, `taken_count = 1`.
- Unlimited three Test payers, `paid_count = 3`, remaining null.
- max=1 Test start then GET without slot → `paid` (GetLink special case).
- Start without slot_key 400.
- Public GET does not need Bearer (One send count does not rise on the second GET).

**Public one-off**

- GET without Bearer; missing 404; start twice same CHIP URL without second PSP HTTP; `started` + `redirect_url` after start; start paid 409; start paused 403 even with stored URL; `email_required` true CHIP / false Stripe; start without rail 503.

**Test rail**

- Mint+start pays, verifying URL, Official Receipt, no PSP HTTP.
- Unsigned JSON webhook can pay an **open** Test checkout (`Webhook_pays_open_test_checkout`).

**Catalog**

- Owner 201; member 403. **Nothing else.**

**Wrap key**

- Production missing throws; Testing empty wraps. **No Development case. No boot validation.**

**Writer/member (Identity)**

- Whoami maps `/me`; 401 skips One; One 401/timeout/500 mapping.
- OrgReady member relation on **path** org; 403 allowed=false; 401 skips One.
- One webhook HMAC pause/resume (charges-paused seam).

**Fulfill envelope (mention)**

- `FillTests.Fulfill_throw_returns_5xx_event_not_committed_retry_pays` uses InMemory no-op transactions + probe throw **before** SaveChanges.

### Missing tests for this slice

- Concurrent occupancy (two different slots, max=1) against **Postgres**.
- Concurrent same `slot_key`.
- CHIP (not Test) abandoned `open` occupies; GET without slot is `full` with `paid_count = 0`.
- Expiry of `open` (cannot lock what does not exist).
- Member cannot `POST /v1/payment-links`.
- Idempotent checkout with **different body** same key; concurrent idempotency; payment-link idempotency.
- Mint with `product_id` copies price — or asserts it does **not** (lock G1).
- Mint `product_id` of another org does not leak the name (if you filter).
- `GET /v1/orgs/{id}/checkouts` after a link start: children included or not (lock B13).
- Development missing WrapKey on PUT (problem JSON, not 500) and/or boot fail.
- Cors/Health do not call `MigrateAsync` / do not use Development+Npgsql.
- `AllowsTest` false in a Production-environment factory (mint test 400, list length 5, webhook test 400).
- `MigrateAsync` against a drifted schema (documented fail).
- pay-spec compile vs live doors (honesty job; this slice only notes the contradiction).
- Rate-limit / slot griefing.
- `CheckoutBaseUrl` missing on payment-link start → 503 not 500.

---

## Ranked findings for this slice (P0/P1/P2)

Rank is **this host-seam slice only**, not a cross-cut 019 list.

### P0

1. **B1 — capped payment links can over-capacity under concurrent start.** Check-then-act insert; unique index is per slot, not per cap. Sequential tests are green. Real rails will mint extra hosted sessions and extra receipts.

### P1

2. **B3 — `open` holds the seat forever; started ≠ paid** for CHIP/Stripe/Billplz/Xendit/Razorpay. Default cap is 1. One abandoned tab is a full link.
3. **B4 — Test auto-fulfill + `AllowsTest = !IsProduction()`.** Staging-shaped environments get unsigned Test webhooks and start-is-paid. Combined with B1/B7, fake receipts.
4. **B5 — WrapKey required in Development; `.env.example` says fallback; PUT 500s.** Blocks 018 BYOK dogfood. Git-default SHA256 key is Testing-only (correct), docs are not.
5. **B9 — Development `MigrateAsync` + Cors/Health `WebApplicationFactory`.** Tests can migrate or fail on the laptop DB; CI pay job has no Postgres.
6. **B2 — same-slot unique violation → 500** on Npgsql; InMemory cannot see it.
7. **B6 — missing `Password=` replaces the entire connection string**, including Host.
8. **B7 — client `slot_key` griefing** on a public URL.
9. **B8 — checkout idempotency race / body-blind / 201 replay; payment-links have no key.**
10. **B10 — payment-link start 500 if CheckoutBaseUrl missing** (uncaught throw).
11. **B11 / G1 — catalog does not price the mint.** Honesty + wrong amount if the UI assumes it does.
12. **G2 / G3 / G7 — pay-spec does not know payment-links, `provider` on create, `slot_key`, gateways list, Test.** Live 400s what the spec allows.

### P2

13. **B12 — writer = `/me` role, member = authz; no payment-link member-403 test.**
14. **B13 / B14 — checkout list mixes children; child tokens are extra pay URLs.**
15. **G5 — CORS and CheckoutBaseUrl are laptop literals.**
16. **G6 — IsolationTests do not lock “do not write ActiveProvider” or occupancy.**
17. **G8 — no GET payment-link by id; unbounded lists.**
18. **G9 / G11 — leftover columns, dead `expired`, missing migration Designers.**
19. **G4 — `/health` vs `/v1/health` vs `/ready` vs spec.**
20. **GatewayTests name “five” vs assertion 6 (Test).**

---

## Refuse for this slice

- Do not flip [011/11](../011-new-lazuar-pay/11-checklist.md) cells from this paper.
- Do not add a project reference into `apps/lazuar-api`. Do not copy `Modules/One`, MediatR, BuildingBlocks, `IPaymentGatewayAdapter`, or `PaymentGatewayFactory`.
- Do not add `IEnumerable<IHostedRail>`. A sixth rail is a folder, two switch arms, `PayProviders`, tests. IsolationTests must stay red on the factory string.
- Do not revive `ActiveProvider` as the mint default. 018 live law is independent vault + explicit `provider` at mint. Leave the column.
- Do not drop `SstRegistered` in a drive-by migration. Do not read it on the pay path. Do not title Tax Invoice. Do not compute SST.
- Do not treat Hub as a source of occupancy/vault design. Hub was one `GatewayType` per tenant; this host **left that** on 018. Steal HTTP judgment only if a **host-seam** bug needs a PSP fact (none of B1–B14 required Hub source).
- Do not load TypeSpec `dist/openapi.yaml` as law. It is gitignored and stale. Grow `main.tsp`.
- Do not implement machine keys, outbound `payment.completed`, escrow, or LHDN in this slice. 018-evals already named those as other products.
- Do not “fix” occupancy only in the merchant Vite. The race is in `MintOrResume`.
- Do not enable Test in Production. Do not document a Development WrapKey fallback unless `SecretBox` actually has one.
- Do not expand CORS to `localhost:3003` / `3004`. `CorsTests` exist so that does not regress.
- This paper does not patch C#. How-to-solve above is analysis.

---

## Appendix: quoted evidence

### A. Composition: maps, DI, password keep, migrate, CORS

See Program.cs 37–92 in “What exists”. Full CORS origin list:

```59:72:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179",
                "http://localhost:4178",
                "http://127.0.0.1:4178",
                "http://localhost:4179",
                "http://127.0.0.1:4179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

### B. Independent vault — PUT does not write ActiveProvider; tests lock null

```82:83:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        Assert.That(db.AuditEvents.Any(a => a.Action == "gateway.credentials.upsert" && a.OrgId == "t1"));
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
```

```170:174:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
        Assert.That(db.GatewayCredentials.Count(), Is.EqualTo(2));
```

Bare GET `/gateway` is the list (length 6 in Testing):

```164:168:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs
        using var bare = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway");
        bare.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var bareGot = await client.SendAsync(bare);
        using var bareDoc = JsonDocument.Parse(await bareGot.Content.ReadAsStringAsync());
        Assert.That(bareDoc.RootElement.GetProperty("processors").GetArrayLength(), Is.EqualTo(6));
```

### C. Bind at mint — no provider is 400; Test skips vault

```161:172:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs
    public async Task Create_without_provider_is_400()
    {
        // ... PUT stripe keys, POST amount only ...
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unknown provider"));
    }
```

```202:213:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs
    public async Task Create_test_without_vault_is_201()
    {
        // POST provider=test, no PUT ...
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), ...);
        Assert.That(doc.RootElement.GetProperty("provider").GetString(), Is.EqualTo("test"));
    }
```

016 law “store Provider only at start” is false here: create stores `Provider = provider` (`CheckoutEndpoints.cs` 82–86).

### D. Occupancy helper + sequential cap test (does not lock B1)

```16:29:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        if (body.Unlimited) maxPayers = null;
        else {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1) return 400 max_payers;
        }
```

```121:145:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs
    public async Task Two_people_can_pay_a_link_of_two()
    {
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 2);
        var a = await PayTest.StartPay(client, token, "slot-aaa-1"); // Test rail → paid
        var b = await PayTest.StartPay(client, token, "slot-bbb-2");
        var c = await PayTest.StartPay(client, token, "slot-ccc-3");
        Assert.That(c.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), ...);
        // GET slot-aaa-1 status paid; GET slot-ccc-3 status full remaining 0
    }
```

### E. Test rail start is paid; verifying URL

```12:38:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Rails/Test/TestRailTests.cs
    public async Task Mint_and_start_pays_without_keys()
    {
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");
        // POST start { name: Ada } — no email
        Assert.That(startDoc.RootElement.GetProperty("redirect_url").GetString(), Does.Contain("status=verifying"));
        var get = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(pay.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(db.Checkouts.Single(x => x.Id == checkoutId).Status, Is.EqualTo("paid"));
        Assert.That(db.Documents.Single().Title, Is.EqualTo("Official Receipt"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }
```

```11:20:apps/lazuar-pay/src/Lazuar.Pay/Rails/Test/TestHosted.cs
    public Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (!PayProviders.AllowsTest(env))
            throw new InvalidOperationException("rail not configured");
        return Task.FromResult(new HostedSession(
            CheckoutUrls.Success(checkout, config, env),
            "test:" + checkout.Id));
    }
```

### F. Public start second call does not re-hit PSP

```39:78:apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs
    public async Task Start_twice_returns_same_url_without_second_psp_http()
    {
        // CHIP vault + checkout, two POST /start
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1)); // after first
        // second:
        Assert.That(secondDoc.RootElement.GetProperty("redirect_url").GetString(), Is.EqualTo(url));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
    }
```

Comment in production code still names the remaining PSP-then-SaveChanges hole for the **first** start:

```170:171:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
            // PSP HTTP then persist. A SaveChanges failure after the processor
            // already created a session may mint a second session on retry.
```

### G. Success / CheckoutBaseUrl

```8:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
    public static string Success(CheckoutRow checkout, IConfiguration config, IHostEnvironment env) =>
        string.IsNullOrWhiteSpace(checkout.SuccessUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken + "?status=verifying"
            : checkout.SuccessUrl;
    // Base: Pay:CheckoutBaseUrl, else Testing → http://localhost:5179, else throw
```

Payment-link children **force** the verifying URL on the **link** token (`MintOrResume` 258–259), so merchant `success_url` on door A does not apply to door B.

### H. WrapKey Testing default vs required

```19:34:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Secrets/SecretBoxTests.cs
    public void Production_missing_wrap_key_throws() { /* Protect → InvalidOperationException contains Pay:WrapKey */ }
    public void Testing_allows_dev_wrap_key() { /* empty config, Testing env, Unprotect == Protect */ }
```

### I. Writer vs member on mint

```228:246:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs
    public async Task Member_cannot_create_checkout()
    {
        factory.One.Responder = /* /me role member, authz allowed true */;
        var response = await client.SendAsync(/* POST /v1/checkouts */);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
```

No equivalent for `POST /v1/payment-links`.

### J. 016 law this SHA overrules (quoted as background, not live)

```108:108:plans/016-adapters-check/01-new-host-seams.md
`org_settings.active_provider` (or equivalent): **one** name the org charges with. PUT gateway sets it. Public start uses it. Buyer page does not offer a dropdown of four PSPs.
```

```143:143:plans/016-adapters-check/01-new-host-seams.md
`POST /v1/checkouts`: `RequireWriterAsync` (closes the member-can-charge hole). Store `Provider` only at start, not at create (merchant may switch rails before the buyer pays).
```

Writer-on-create is still live. One-active-rail and store-provider-at-start-only are not.

### K. CI pay job has no database

```96:112:.github/workflows/ci.yml
  pay:
    runs-on: ubuntu-latest
    steps:
      ...
      - name: Test focused Pay host
        run: dotnet test apps/lazuar-pay/Lazuar.Pay.slnx --nologo --verbosity minimal
```

No `services: postgres`, no `5435`. Cors/Health Development factory vs this job is B9.

### L. Migration PaymentLinkPayers unique index (Postgres)

```63:75:apps/lazuar-pay/src/Lazuar.Pay/Data/Migrations/20260825120000_PaymentLinkPayers.cs
        migrationBuilder.CreateIndex(
            name: "IX_checkouts_PaymentLinkId_SlotKey",
            schema: "public",
            table: "checkouts",
            columns: ["PaymentLinkId", "SlotKey"],
            unique: true,
            filter: "\"SlotKey\" IS NOT NULL");
```

This prevents B2 on a real engine. It does **not** prevent B1.

### M. Fulfillment same-handler (host envelope; webhook agent owns depth)

```13:36:apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs
        var checkout = await db.Checkouts.FirstOrDefaultAsync(x => x.Id == checkoutId, ct);
        if (checkout is null) return;
        if (checkout.Amount <= 0) return;
        if (checkout.Status != "open") return;
        var settings = await db.OrgSettings.FindAsync([checkout.OrgId], ct);
        if (settings?.ChargesPaused == true) throw new ChargesPausedException();
        checkout.Status = "paid";
        // charge, optional payer, optional subscription if interval mo/yr,
        // cash D + revenue C, RCPT sequence, audit checkout.paid, SaveChanges
```

Checkout mint always sets `Interval = "one_off"`, so the subscription branch is dead on both mint doors unless something else writes `mo`/`yr`. Catalog interval is not that something.

---

*End of 01 — Pay host seams after 017 layout + 018 vault/capacity. Analysis only. Live files on `9f04ad58` are authority.*
