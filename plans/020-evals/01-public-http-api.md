# 01 — Public HTTP API: can a stranger call Pay `/v1` as a product?

**Type:** Uncondensed evaluation. **Not** an implementation. **Not** a patch. **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`. **Not** a copy of Hub `Modules/One`.

**Slice:** Clean public HTTP API for other apps integrating with focused Pay. Question: can a stranger (another product, not Pay merchant Vite) call Pay `/v1` as a product? What is live, what is cashier-shaped, what is missing (versioning, errors, idempotency, pagination, filters, stable JSON, auth scheme on each door)?

Live files on this SHA are authority. [012-one-to-pay](../012-one-to-pay/README.md), [013-prods](../013-prods/README.md), [006-sample](../006-sample/README.md), [011-new-lazuar-pay/08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md), and [019-evals](../019-evals/README.md) are historical / product papers. Where they disagree with live files, live files win; the disagreement is named with evidence.

---

## Coordinates

| Field | Value |
|-------|--------|
| Title | Public HTTP API for a second app against focused Pay |
| Date | 2026-08-28 |
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `fix/002-pay-host-bugs` (`.git/HEAD` → `refs/heads/fix/002-pay-host-bugs`) |
| HEAD | `6d730d155c871465c35c192cf7730bfd270b47fa` (`6d730d15`) |
| HEAD subject | `fix(pay): store per-org One webhook secrets` |
| Host | `apps/lazuar-pay/src/Lazuar.Pay` (`net10.0`, listen `http://localhost:8081`) |
| Tests | `apps/lazuar-pay/tests/Lazuar.Pay.Tests` |
| Contract | `packages/pay-spec/main.tsp` → `packages/pay-spec/dist/openapi.yaml` via `task pay:spec` |
| Honesty | `scripts/check-pay-openapi-honesty.mjs` (CI job `pay` after `tsp compile`) |
| Type | Analysis. How to solve is analysis, not a patch. |
| Sibling One | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` (read-only, not copied) |
| 019 comparison SHA | `9f04ad58` — `fix(pay-ui): match receipts table to pay-link chrome` (`plans/019-evals/08-contracts-spec-honesty.md`) |

Counts on this SHA, taken from live `Map*` scrape + honesty run, not from 019:

| Surface | Operations |
|---------|------------|
| Live `MapGet`/`MapPost`/`MapPut` under `apps/lazuar-pay/src` | **24** (22 under `/v1`, plus unversioned `GET /health` and `GET /ready`) |
| `MapDelete` / `MapPatch` | **0** |
| `packages/pay-spec/main.tsp` | **22** ops (all `/v1`; no unversioned probes) |
| On-disk `packages/pay-spec/dist/openapi.yaml` after honesty | **22** spec ops |
| Honesty `IMPL_ONLY` | `GET /health`, `GET /ready` |
| Honesty result this SHA | `Pay OpenAPI honesty: 22 spec ops, 24 Map* (2 host-only probes).` exit 0 |

---

## Assigned slice / out of scope

**This file owns:** the HTTP door. Every `Map*` under `apps/lazuar-pay/src`. Auth scheme **as it appears on each door** (none / Bearer member / Bearer writer / HMAC / PSP-signature). Request/response JSON actually returned. Status codes actually returned. Error envelope. Idempotency. Versioning. Pagination / filters. Checkout GET vs org-scoped lists. Public buyer GET/POST `/v1/pay/{token}`. pay-spec honesty vs live. Diff vs 019/08 on `9f04ad58`. Hub 006 second-app **judgment** (API key header, M2M checkout, webhook verify recipe). Smallest hatch so another app can mint a checkout and poll paid without cloning merchant SPA. Ranked holes in **this** slice.

**Other 020 files own (named so this paper does not steal them):**

| File | Owns; this paper only points |
|------|------------------------------|
| [02-machine-keys-m2m.md](./02-machine-keys-m2m.md) | Whether One `lzr_sk_` presented as Pay Bearer is a product, scopes, homemade Pay keys |
| [03-outbound-webhooks.md](./03-outbound-webhooks.md) | Plane C `payment.completed`, signing, retries, outbox |
| [04-inbound-webhooks.md](./04-inbound-webhooks.md) | Plane A One→Pay and Plane B PSP→Pay internals beyond “this is a MapPost with HMAC” |
| [05-identity-authz-tenancy.md](./05-identity-authz-tenancy.md) | MemberGate vs writer overlay, One coupling vs standalone Pay |
| [06-host-production.md](./06-host-production.md) | Compose, images, WrapKey, obs, boot CORS |
| [07-money-remaining.md](./07-money-remaining.md) | Occupancy leftover, refunds, disputes, subscriptions |
| [08-headless-vs-spa.md](./08-headless-vs-spa.md) | Merchant/checkout as clients of `/v1` vs API-only integrator |
| [09-spec-docs-sample.md](./09-spec-docs-sample.md) | Docs site, second-app sample package, SDK |
| [10-honesty-production-bar.md](./10-honesty-production-bar.md) | Cross-cut ranked bar |
| [00-evaluation.md](./00-evaluation.md) | Parent verdict after 01–10 |

**Refuse in this paper:** MediatR, Hub `@repo/api-types-ts`, cathedral SDK, copying `Modules/One`, adding a project reference into `apps/lazuar-api`, inventing `/v2`, shrinking live doors to fit a comment, treating Hub `apps/lazuar-api` as a live Pay surface.

Standing law this report must not weaken:

- One Pay binary, one Pay database. Bezos is the **door** (`/v1`); Linux is the **room** (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path.
- Steal HTTP **judgment** from Hub; Hub `apps/lazuar-api` / ops :3003 / portal :3004 stay museum.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`).

---

## Direct answer (then the evidence, not instead of it)

A stranger **cannot** call focused Pay `/v1` as a product the way Hub’s second-app could.

What **is** live: 24 Minimal API maps on one binary, 22 of them under `/v1`, JSON snake_case on success paths that pass `OneClient.Json`, a `{ status, title, detail }` problem object on most 4xx/5xx, Bearer + One `authz/check member` (and a whoami role overlay for writers) on every staff door, no Bearer on buyer `/v1/pay/{token}` GET/POST, HMAC on One inbound, PSP-signature on Plane B. `POST /v1/checkouts` exists, returns **201** on mint and **200** on idempotent replay, honors `Idempotency-Key` / body `idempotency_key`, and `GET /v1/checkouts/{id}` returns the session including `status`. Public `GET /v1/pay/{token}` returns `status` including `paid` with no Bearer. Honesty scrape is green: 22 spec ops match 22 versioned Maps; unversioned `/health` and `/ready` stay host-only.

What is **cashier-shaped**: the mint doors a human SPA uses (`POST /v1/payment-links`, `POST /v1/orgs/{orgId}/products`, vault PUT) require a **human One access_token** of role `owner` or `admin`. CORS in Development is the laptop Vite list (`:5178` merchant, `:5179` checkout, preview 4178/4179). Production CORS is an allow-list you must set; a second-app origin is **not** there by default. There is no API-key header, no Pay-issued `lzr_sk_`, no `payments.checkouts:write` scope, no outbound `payment.completed`, no pagination, no filters, no `/v2`, no deprecation header, no RFC7807 `type`/`instance`/`application/problem+json`. Payment-links have **no** idempotency. Lists dump the whole org table.

A second app that wants “mint a checkout, send the buyer somewhere, learn when it is paid” without cloning `:5178` **can** use the live doors **if and only if** it already has a writer human JWT (or, later, whatever 02 decides about One keys) **and** it polls. Hub’s hatch (Bearer `sk_test_…` + POST integrations checkout + signed `payment.completed`) is not on 8081.

That is a **missing feat**, not a live lie of the Maps. 002 closed the hosted-cashier contract holes (path honesty, 201, gateway alias, receipt GET, Bearer-before-lookup, idempotent **200** replay, org-ready not dummy). It did not make `/v1` a sold product.

---

## Files actually opened

**Composition + every `Map*`**

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneAuthz.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/CreatePaymentLinkRequest.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/BuyerEmail.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayLimiter.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/README.md`

**Spec / honesty / CI**

- `packages/pay-spec/main.tsp`
- `packages/pay-spec/dist/openapi.yaml` (workspace leftover compiled 2026-08-28; honesty run against it)
- `packages/pay-spec/tspconfig.yaml`
- `packages/pay-spec/README.md`
- `scripts/check-pay-openapi-honesty.mjs`
- `Taskfile.yml` (`pay:spec`)
- `.github/workflows/ci.yml` job `pay`

**Tests that lock door status codes**

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/HealthTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`

**SPA clients (who the doors are for today)**

- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/http.ts`
- `apps/lazuar-pay-checkout/src/App.tsx`

**Historical papers (disagreement named, not treated as live inventory)**

- `plans/019-evals/08-contracts-spec-honesty.md` (SHA `9f04ad58`, 22 live / 13 tsp)
- `plans/019-evals/01-pay-host-seams.md` (shape of uncondensed report)
- `plans/011-new-lazuar-pay/08-bezos-door.md`
- `plans/012-one-to-pay/08-machine-keys.md`
- `plans/013-prods/01-production-ready-bar.md`
- `plans/006-sample/README.md`
- `plans/006-sample/04-checkout-create-contract.md`
- `plans/006-sample/05-webhook-verify-nextjs.md`
- `plans/006-sample/06-provision-and-env.md`
- `issues/002/README.md` (001–080 marked resolved on this branch)
- `plans/020-evals/README.md`

Commands run (read-only): `git log --oneline 9f04ad58..HEAD`, `git rev-parse HEAD`, `node scripts/check-pay-openapi-honesty.mjs`.

---

## Composition: every Map* is registered here

```74:84:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
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

Wire JSON for the host:

```25:29:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

`OneClient.Json` is the same policy, used on almost every success `Results.Json(..., OneClient.Json)`:

```12:16:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
```

No `MapDelete`. No `MapPatch`. No `/api/v1` prefix. One’s prefix is **One’s** (`http://localhost:8080/api/v1`); Pay’s prefix is **`/v1`** on 8081. IsolationTests ban Hub modules / MediatR / `@repo/api-types-ts` and do **not** assert pay-spec ↔ Map* honesty (that is now `check-pay-openapi-honesty.mjs`).

---

## Full door inventory

Auth column uses the live gate, not TypeSpec English. “Bearer member” = `MemberGate.RequireMemberAsync` (Bearer required, then One `POST tenants/{orgId}/authz/check` relation `member`). “Bearer writer” = member **and** whoami tenant role `owner` or `admin`. HMAC = `X-Lazuar-Signature` (+ optional `X-Lazuar-Timestamp`). PSP = provider-specific verify, not Bearer.

| # | Method | Path | Auth | Who it is for | Request | Success body | Status codes actually returned |
|---|--------|------|------|---------------|---------|--------------|--------------------------------|
| 1 | GET | `/health` | none | orchestrator / k8s | — | `{ status: "ok" }` | 200 |
| 2 | GET | `/v1/health` | none | same, versioned twin | — | `{ status: "ok" }` | 200 |
| 3 | GET | `/ready` | none | orchestrator (Postgres `CanConnect`) | — | `{ status: "ready" }` / `{ status: "not_ready" }` | 200 / 503 |
| 4 | GET | `/v1/whoami` | Bearer (no member check; Pay projects One `GET /me`) | staff SPA | header `X-Lazuar-Tenant-Id` optional hint | `WhoamiResponse` | 200 / 401 / 403 / 503 |
| 5 | GET | `/v1/orgs/{orgId}/ready` | Bearer member | staff SPA (unused by merchant today) | path org | `{ org_id, ready }` | 200 / 401 / 403 / 400 / 429 / 503 |
| 6 | POST | `/v1/checkouts` | Bearer **writer** on `body.org_id` | **second-app mint hatch** (SPA does **not** call it) | `CreateCheckoutRequest`; header `Idempotency-Key` wins over body | `CheckoutSession` | 201 mint / **200 replay** / 400 / 401 / 403 / 409 / 503 |
| 7 | GET | `/v1/checkouts/{id}` | Bearer first, then member of **session.org_id** | second-app poll / staff | path id | `CheckoutSession` | 200 / 401 / 404 (cross-org) / 403 if suspended |
| 8 | GET | `/v1/orgs/{orgId}/checkouts` | Bearer member | staff (SPA unused) | path org | array of list items, **excludes** payment-link children | 200 / 401 / 403 |
| 9 | POST | `/v1/payment-links` | Bearer **writer** | **staff SPA mint** | `CreatePaymentLinkRequest` | `PaymentLinkView` | **201 only** / 400 / 401 / 403 / 404 product |
| 10 | GET | `/v1/orgs/{orgId}/payment-links` | Bearer member | staff SPA | path org | `PaymentLinkView[]` | 200 / 401 / 403 |
| 11 | POST | `/v1/orgs/{orgId}/products` | Bearer **writer** | staff SPA sidecar for labels | `CreateProductRequest` | created product + price | **201** / 400 / 401 / 403 |
| 12 | GET | `/v1/orgs/{orgId}/products` | Bearer member | staff (SPA unused) | path org | products with `prices[]` | 200 / 401 / 403 |
| 13 | GET | `/v1/pay/{token}` | **none** | buyer SPA **or** second-app hosted checkout | query `slot_key?` | `PublicPay` / link view | 200 / 404 |
| 14 | POST | `/v1/pay/{token}/start` | **none**; rate-limited per token | buyer SPA **or** second-app hosted checkout | `{ name?, email?, slot_key? }` | `{ redirect_url }` | 200 / 400 / 403 / 404 / 409 / 429 / 503 |
| 15 | PUT | `/v1/orgs/{orgId}/gateway` | Bearer **writer** | staff SPA vault | `PutGatewayRequest` | `GatewayJson` metadata, **no secret echo** | 200 / 400 / 401 / 403 / 503 WrapKey |
| 16 | GET | `/v1/orgs/{orgId}/gateway` | Bearer member | staff (SPA unused) | query `provider` **required** | singular `GatewayView` or `{ configured: false }` | 200 / 400 missing provider / 401 / 403 |
| 17 | GET | `/v1/orgs/{orgId}/gateways` | Bearer member | staff SPA list | path org | `{ org_id, processors[] }` | 200 / 401 / 403 |
| 18 | POST | `/v1/webhooks/{provider}/{orgId}` | **PSP signature**, not Bearer | PSP (Plane B) | raw body + provider headers/query | `{ ok: true }` / `{ duplicate: true }` / `{ ignored }` | 200 / 400 / 409 paused / 500 / 503 |
| 19 | GET | `/v1/orgs/{orgId}/payments` | Bearer member | staff SPA | path org | charge list | 200 / 401 / 403 |
| 20 | GET | `/v1/orgs/{orgId}/receipts` | Bearer member | staff SPA | path org | document list | 200 / 401 / 403 |
| 21 | GET | `/v1/orgs/{orgId}/receipts/{id}` | Bearer member | unused SPA; tested | path org+id | **same field set as list** | 200 / 404 / 401 / 403 other org |
| 22 | POST | `/v1/one/webhooks` | HMAC | One (Plane A) | JSON event + `X-Lazuar-Signature` | `{ ok: true }` / `{ duplicate: true }` | 200 / 400 / 401 / 503 |
| 23 | PUT | `/v1/orgs/{orgId}/one-webhook` | Bearer **writer** | staff (stores per-org `whsec_`) | `{ webhook_secret }` | `{ org_id, webhook_configured }` | 200 / 400 / 401 / 403 / 503 |
| 24 | GET | `/v1/orgs/{orgId}/one-webhook` | Bearer member | staff | path org | `{ org_id, webhook_configured }` | 200 / 401 / 403 |

There is **no** door for: API keys, refunds, disputes, subscriptions, outbound merchant webhooks, pagination, search, cancel checkout, expire checkout, delete product, rotate key metadata without PUT whole secret.

---

## Auth scheme on each door (live)

### Bearer parse

```3:20:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs
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

The token is **opaque to Pay**. Pay never hashes it, never looks for `lzr_sk_` prefix, never reads a Pay `api_keys` table (there is none). It forwards the Authorization header to One. Whether a One machine key would survive `GET /me` + `authz/check` is **02’s** slice. On **this** SHA the public contract is: staff doors want a Bearer that One accepts as a human (or whatever One accepts). There is no `X-Api-Key`, no `sk_test_` Pay mint, no scope string.

### Member

```8:46:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireMemberAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return PayErrors.Status(400, "Bad Request", "org_id is required");
        }
        // ...
        return result.StatusCode switch
        {
            401 => PayErrors.Status(401, "Unauthorized", "Identity provider rejected the token"),
            403 => PayErrors.Status(403, "Forbidden", SuspendedDetail(result.Detail) ?? "Not a member of this org"),
            400 => PayErrors.Status(400, "Bad Request", string.IsNullOrWhiteSpace(result.Detail)
                ? "Identity provider rejected the request"
                : result.Detail),
            429 => PayErrors.Status(429, "Too Many Requests", "Identity provider rate limited"),
            200 => PayErrors.Status(403, "Forbidden", "Not a member of this org"),
            _ => PayErrors.Status(503, "Service Unavailable", "Identity provider failed")
        };
    }
```

### Writer overlay

```60:97:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync(...)
    {
        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        // ...
        if (tenant.Role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
        return null;
    }
```

Writer is **not** OpenFGA `admin`. It is whoami tenant `role`. 05 owns whether that is the right overlay. For this slice: mint doors (checkouts, payment-links, products, vault PUT, one-webhook PUT) are writer; list/get doors are member; whoami is Bearer-only; public pay and both webhook planes are not Bearer.

TypeSpec has **no** `@useAuth` and generated OpenAPI has **no** `securitySchemes` (`rg security` on `dist/openapi.yaml` is empty). Auth is English comments plus live C#. A generated client cannot 401 itself.

---

## Quoted handlers (every door)

### Doors 1–3 — health / ready

```9:22:apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs
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
```

Not `PayProblem`. Not RFC7807. Anonymous `{ status }`. `/ready` is **Postgres liveness**, not org-ready. Tests now lock it (`HealthTests.Unversioned_ready_returns_200_on_inmemory`). Honesty allowlists both unversioned probes. No SPA client.

### Door 4 — `GET /v1/whoami`

```10:42:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs
        app.MapGet("/v1/whoami", Handle);
    // ...
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        return Map(result);
    // 401 One rejected; 403 One forbade; 503 unreachable / failed
```

Response type:

```3:20:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs
public sealed class WhoamiResponse
{
    public required string UserId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public string? ActiveOrgId { get; init; }
    public IReadOnlyList<WhoamiTenant> Tenants { get; init; } = [];
}
```

Wire names via snake_case: `user_id`, `is_platform_admin`, `active_org_id`, `name`. Mapper copies `Name` from One (`OneMeMapper.cs:34`). Merchant `getWhoami` is the only SPA caller. A second app that is not a staff SPA does not need this door to mint money — it needs it only if it is pretending to be the merchant shell.

### Door 5 — `GET /v1/orgs/{orgId}/ready`

```14:38:apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs
        app.MapGet("/v1/orgs/{orgId}/ready", Handle);
    // RequireMemberAsync then:
        var settings = await db.OrgSettings.FindAsync([orgId], cancellationToken);
        var hasVault = await db.GatewayCredentials.AnyAsync(x => x.OrgId == orgId, cancellationToken);
        var ready = IsReady(settings?.ChargesPaused == true, hasVault, PayProviders.AllowsTest(env));
        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = ready }, OneClient.Json);

    internal static bool IsReady(bool chargesPaused, bool hasVault, bool allowsTest) =>
        !chargesPaused && (hasVault || allowsTest);
```

**Not** dummy `ready: true`. Tests lock `ready: false` when charges paused (`OrgReadyTests.Ready_false_when_charges_paused`) and `IsReady` without vault when test is off. 019/08 and 013 called this dummy. Live files win. Merchant SPA still does **not** call it (whoami tenants instead).

### Door 6 — `POST /v1/checkouts` (the mint hatch)

```14:109:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        app.MapPost("/v1/checkouts", Create);
        // writer on body.org_id
        // paused → 403 "Org charges are paused"
        // amount <= 0 → 400
        // unknown provider → 400
        // test only when AllowsTest; else vault row required
        var idempotency = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotency))
        {
            idempotency = body.IdempotencyKey;
        }
        // ... mint session, store.CreateAsync
        catch (IdempotencyConflictException)
        {
            return PayErrors.Status(409, "Conflict", "idempotency key reused with a different body");
        }
        var created = session.Id == mintedId;
        return Results.Json(session, OneClient.Json, statusCode: created ? 201 : 200);
```

Body:

```3:13:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CreateCheckoutRequest.cs
public sealed class CreateCheckoutRequest
{
    public string? OrgId { get; set; }
    public string? Provider { get; set; }
    public string? ProductId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? IdempotencyKey { get; set; }
}
```

Session on the wire:

```3:21:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutSession.cs
public sealed class CheckoutSession
{
    public required string Id { get; init; }
    public required string OrgId { get; init; }
    public string? Provider { get; init; }
    public string? ProductId { get; init; }
    public string? PaymentLinkId { get; init; }
    public string? SlotKey { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public string? PublicToken { get; init; }
    public string? Interval { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? PayerName { get; init; }
    public string? PayerEmail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

**No `checkout_url`.** Hub’s integration create returned a processor URL in one hop. Focused Pay mints an **open session** with `public_token`. The buyer hop is `GET/POST /v1/pay/{public_token}` or the hosted page `:5179/c/{token}`. A second app that copies Hub’s “POST then redirect to `checkout_url`” will not find that field.

Tests (`CheckoutTests`):

- no Bearer → 401, One not called
- create → **201**, snake_case `org_id`, `MYR` default, `status=open`, `provider`
- replay same `Idempotency-Key` → **200**, same id
- different amount same key → **409**
- other org create → **403**
- member role → **403** `"Writer role required"` path
- no provider → 400 `"unknown provider"`
- unconfigured rail → 400 `"rail not configured"`
- test without vault → 201 (Testing env)

Host README still teaches this curl as the mint example (`apps/lazuar-pay/README.md:57-62`). Merchant SPA does **not** POST it.

### Door 7 — `GET /v1/checkouts/{id}`

```112:142:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        if (!Bearer.TryGet(request, out _))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        var session = await store.GetAsync(id, cancellationToken);
        if (session is null)
        {
            return PayErrors.Status(404, "Not Found", "Checkout not found");
        }

        var denied = await MemberGate.RequireMemberAsync(request, one, session.OrgId, cancellationToken);
        if (denied is not null)
        {
            if (PayErrors.TryForbiddenDetail(denied, out var detail)
                && detail.IndexOf("suspend", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return PayErrors.Status(404, "Not Found", "Checkout not found");
            }
            return denied;
        }
        return Results.Json(session, OneClient.Json);
```

**Bearer before lookup.** Unknown id with no Bearer is **401**, not 404 (`CheckoutTests.Get_without_bearer_is_401_for_unknown`). Known id, other org, is **404** `"Checkout not found"` (`Get_other_org_session_is_404`), not 403, unless the 403 detail contains `"suspend"`. This is the poll door for a second app that minted via POST `/v1/checkouts`. SPA does not call it.

### Door 8 — `GET /v1/orgs/{orgId}/checkouts`

```158:184:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs
        var rows = await db.Checkouts.AsNoTracking()
            .Where(x => x.OrgId == orgId && x.PaymentLinkId == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Json(rows.Select(r => new
        {
            id = r.Id,
            org_id = r.OrgId,
            provider = r.Provider,
            amount = r.Amount,
            currency = r.Currency,
            status = r.Status,
            public_token = r.PublicToken,
            created_at = r.CreatedAt,
            label = r.ProductId is not null && names.TryGetValue(r.ProductId, out var name) ? name : null
        }), OneClient.Json);
```

**No pagination. No cursor. No `status=` filter. No `total`.** Newest-first dump. **Omits payment-link children** (`PaymentLinkId == null`). Test `List_omits_payment_link_children` locks length 1 after seeding a one-off plus a started link child. Other org list is **403** (member check on path org **before** lookup — existence of the other org’s rows is not leaked).

TypeSpec comment still says the opposite (named under remaining drift).

### Door 9 — `POST /v1/payment-links`

Writer on `body.org_id`. Same pause / amount / provider / vault rules as checkout create. Capacity:

```73:85:apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs
        if (body.Unlimited)
        {
            maxPayers = null;
        }
        else
        {
            maxPayers = body.MaxPayers ?? 1;
            if (maxPayers < 1)
            {
                return PayErrors.Status(400, "Bad Request", "max_payers must be at least 1");
            }
        }
```

Always `SaveChanges` a new row. Always **201**. **No `Idempotency-Key`.** Grep of payment-link tests for `Idempotency` is empty. TypeSpec documents the gap:

```325:331:packages/pay-spec/main.tsp
  /** Merchant mint door. Requires Bearer + writer. No Idempotency-Key. */
  @post
  @route("/payment-links")
  create(@body body: CreatePaymentLinkRequest): {
    @statusCode statusCode: 201;
    @body body: PaymentLink;
  };
```

This is what `:5178` CheckoutsPage POSTs. Double-click mints two links. Kernel hole for the **used** mint door.

### Door 10 — `GET /v1/orgs/{orgId}/payment-links`

Member. Full table, newest-first, occupancy computed from child checkouts (`open`+`paid` count as taken). Same: no page, no filter, no total envelope — a JSON **array**, not `{ items, total }`.

### Doors 11–12 — catalog

Create is writer, **201**, MYR-only (`"Bar B currency is MYR"`), amount > 0, name required. List is member, includes `prices[]` via anonymous `{ x.Id, x.Amount, x.Currency, x.Interval }` serialized with `OneClient.Json` → `id`/`amount`/`currency`/`interval`. No pagination. No GET-by-id. No PATCH. No Idempotency-Key. Merchant POSTs create as a label sidecar; does not GET the list.

### Doors 13–14 — public buyer

```23:24:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        app.MapGet("/v1/pay/{token}", Get);
        app.MapPost("/v1/pay/{token}/start", Start);
```

No Bearer. Token is a **payment-link** public token or a **checkout** public token. GET binds `slot_key` as a query argument. Start body:

```458:463:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
public sealed class StartPayRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? SlotKey { get; set; }
}
```

Rate limit is **start only**, in-process, per token:

```127:131:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs
        var maxStarts = config.GetValue("Pay:StartMaxPerMinute", 20);
        if (maxStarts > 0 && !PublicPayLimiter.TryAcquire(token, maxStarts, 60))
        {
            return PayErrors.Status(429, "Too Many Requests", "Too many start attempts");
        }
```

`PublicPayLimiter` is a `ConcurrentDictionary` of timestamps — one process, lost on restart, not Redis. GET is unlimited. Tests: `Public_get_does_not_need_bearer`, `Public_missing_is_404`, `Start_twice_returns_same_url_without_second_psp_http`, `PaymentLinkTests.Start_rate_limit_is_429`.

Start on a **payment-link** token requires `slot_key` length 8–128 else 400 `"slot_key is required"`. Standalone checkout tokens ignore it. Email required when `PayProviders.RequiresEmail` (not Stripe, not Test) and email is empty or `customer@example.com`.

Success `{ redirect_url }`. Not `{ checkout_url }`. Not the Hub envelope.

**CORS:** default Development origins are merchant+checkout Vite only:

```9:18:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs
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
```

Production/Staging **throw** if `Pay:CorsOrigins` is empty. Tests lock: `:5179` GET/POST/OPTIONS on `/v1/pay/*` allow; ops `:3003` denied; configured origin **replaces** the laptop list (so `:5179` disappears if you set only `https://checkout.example`). A second app that hosts its own checkout **from a browser** must be on that allow-list. A second app that starts pay **server-side** does not need CORS.

**Fit for a second app that hosts its own checkout vs must use :5179:** the HTTP doors are enough to render amount/status and POST start, **if** CORS includes the origin **or** the start is server-side. The hosted page is a client of those doors, not a required hop. `:5179` is convenience chrome (slot_key in localStorage, verifying poll). `Pay:CheckoutBaseUrl` still points PSP return at the hosted page unless the mint set `success_url`/`cancel_url` on `POST /v1/checkouts`. Payment-link children **always** write success/cancel from `CheckoutUrls.Base` (`…/c/{linkToken}?status=verifying`). A second app using **payment-links** as Hub used M2M checkouts will dump buyers back on `:5179` unless it also changes that config. Using **`POST /v1/checkouts` with its own `success_url`** is the hatch that does not require `:5179`.

### Doors 15–17 — vault

PUT writer. GET singular **requires** `?provider=`; missing query is **400** `"provider is required"` (`GatewayTests.Get_singular_without_provider_is_400`). List is `{ org_id, processors }` with 5 rails or 6 including `test` outside Production. SPA uses **list**. PUT test → 400 `"test processor does not take secrets"`. Secrets never echo (`GatewayTests` asserts body does not contain `sk_test_dummy`).

019 said empty-query singular **aliased the list envelope**. That collision is **closed**. Spec comment matches: “Singular requires `provider`. Missing query is 400, not the list envelope.”

### Door 18 — Plane B PSP

```23:24:apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs
        app.MapPost("/v1/webhooks/{provider}/{orgId}", Handle);
```

Not for a second app. 200 bodies `{ ok: true }` / `{ duplicate: true }` / `{ ignored: reason }`. Duplicate uses `Results.Ok(new { duplicate = true })` — still snake-stable because the property is already `duplicate`. 04 owns parse internals.

### Doors 19–21 — money reads

Member. Payments = charges joined to checkouts. Receipts = documents; `number` is `"PENDING"` if null; `status` is `pending`|`issued`. GET by id is **org-scoped** (`d.Id == id && d.OrgId == orgId`) so other-org id in this org’s path is 404; other-org **path** is 403 before lookup (`Get_receipt_other_org_is_403`). GET-by-id **matches list fields** (`Get_receipt_by_id_matches_list_fields`). 019 said the detail payload was narrower and untested. Closed.

No `?status=`, no date range, no cursor, no `total`.

### Doors 22–24 — Plane A One + per-org secret

POST `/v1/one/webhooks` is One→Pay, not Pay→app. HMAC:

```34:38:apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs
        var provided = request.Headers["X-Lazuar-Signature"].ToString().Trim();
        var timestamp = request.Headers["X-Lazuar-Timestamp"].ToString().Trim();
        if (!OneWebhookSignature.TryVerify(secret, json, provided, timestamp))
        {
            return PayErrors.Status(401, "Unauthorized", "Invalid HMAC");
        }
```

Verifier accepts combined `t=…,v1=…` **or** split `X-Lazuar-Signature: v1=…` + `X-Lazuar-Timestamp` (One’s product dialect). Missing secret → 503. This is **not** a recipe a second app uses to verify Pay→app events, because Pay does not send those events (03). PUT/GET `/v1/orgs/{orgId}/one-webhook` store/read whether this shop’s One `whsec_` is on file (HEAD commit). Staff door, not second-app.

---

## Error envelope: PayProblem vs Results.Ok

```1:13:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs
internal sealed class PayProblem
{
    public int Status { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
}

internal static class PayErrors
{
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new PayProblem { Status = status, Title = title, Detail = detail }, statusCode: status);
```

Wire keys after naming policy: `status`, `title`, `detail`. Merchant scrapes `detail` only:

```1:8:apps/lazuar-pay-merchant/src/lib/http.ts
export async function problemDetail(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (body.detail && body.detail.trim()) return body.detail
  } catch {
    /* ignore */
  }
  return fallback
}
```

**Not RFC7807.** Missing: `type` URI, `instance`, `traceId`, extensions. Content-Type is `application/json` from `Results.Json`, not `application/problem+json`. No `PayProblem` model in TypeSpec. Generated clients have no 4xx schema.

**Not every error uses PayProblem:**

| Path | Body | Envelope |
|------|------|----------|
| Most 4xx/5xx in MemberGate / mint / public start / vault / Plane A/B verify fail | `{ status, title, detail }` | PayProblem |
| GET `/health`, `/v1/health` | `{ status: "ok" }` | anonymous `Results.Ok` |
| GET `/ready` 200 | `{ status: "ready" }` | anonymous |
| GET `/ready` 503 | `{ status: "not_ready" }` | anonymous **without** title/detail |
| Plane B duplicate | `{ duplicate: true }` | anonymous `Results.Ok` |
| Plane B ignored | `{ ignored: reason }` | anonymous + `OneClient.Json` |
| Plane B / Plane A success | `{ ok: true }` | anonymous |
| Plane A duplicate | `{ duplicate: true }` | anonymous `Results.Ok` |

A stranger writing a client must special-case 200-variant webhook bodies and `/ready` 503. Staff 4xx is the three-field object. That is **stable enough for the SPA** (`detail` is the contract the UI uses) and **not** a problem+json product.

---

## Idempotency

Honored on **one** mint door.

| Door | Header | Body field | Replay | Conflict | Fingerprint |
|------|--------|------------|--------|----------|-------------|
| `POST /v1/checkouts` | `Idempotency-Key` wins | `idempotency_key` | **200** same session | **409** different fingerprint | `amount` + `currency` + `provider` only (`CheckoutStore.SameFingerprint`) |
| `POST /v1/payment-links` | none | none | always new **201** | n/a | n/a |
| `POST /v1/orgs/{orgId}/products` | none | none | always new **201** | n/a | n/a |
| `PUT` vault / one-webhook | none (upsert by PK) | n/a | 200 overwrite | n/a | org+provider / org |
| `POST /v1/pay/{token}/start` | none | none | stored `PspRedirectUrl` returned, no second PSP HTTP | 409 not open / full | checkout row, not a client key |
| Plane B / Plane A | event id uniqueness | n/a | `{ duplicate: true }` 200 | n/a | delivery/event id |

Store:

```9:89:apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingKey = await db.IdempotencyKeys.FindAsync([session.OrgId, idempotencyKey], ct);
            // SameFingerprint → return mapped existing; else IdempotencyConflictException
        }
        // insert checkout + idempotency_keys; DbUpdateException race reloads
    static bool SameFingerprint(CheckoutRow existing, CheckoutSession session) =>
        existing.Amount == session.Amount
        && string.Equals(existing.Currency, session.Currency, StringComparison.OrdinalIgnoreCase)
        && string.Equals(existing.Provider, session.Provider, StringComparison.OrdinalIgnoreCase);
```

Unique key is `(OrgId, Key)` (`PayDbContext.cs:59-62`). Header wins over body (`CheckoutEndpoints.cs:76-80`) — same judgment as Hub 006/04.

**Missing on payment-links: yes.** Spec even says so. SPA POSTs payment-links and never sends the header. 019: “the header exists on the unused mint door and is absent on the used mint doors.” Still true. What 002 closed: replay is **200 not 201**; race on unique key reloads; fingerprint is no longer body-blind (but it is still **narrow** — `success_url` / `cancel_url` / `product_id` changes do **not** 409). Hub fingerprinted URLs, description, email, metadata. Focused Pay will silently replay a checkout toward the first success URL if the second app retries with a new `success_url` and the same key. Named as remaining hole, not 002 reopen of the race.

Tests lock 200 replay (`CheckoutTests.Create_idempotent_on_key` lines 164-168): first Created, second OK, third Conflict.

---

## Versioning

Only `/v1` as a product prefix. Twins:

- `GET /health` and `GET /v1/health` — same `{ status: "ok" }`. The versioned one is in TypeSpec; the unversioned one is host-only on purpose (`IMPL_ONLY` in the honesty script).
- `GET /ready` is unversioned only. There is no `GET /v1/ready`. Org-ready is `GET /v1/orgs/{orgId}/ready` (different resource).

No `/v2`. No `Sunset` / `Deprecation` headers. TypeSpec `@info version: "0.1.0"`. OpenAPI `info.version: 0.1.0`. No changelog in pay-spec. Pay’s `/v1` is not One’s `/api/v1`. CHIP’s `/api/v1` and Xendit’s `/v2/invoices` are **egress** URLs, not Pay doors.

**Deprecation story: none.** Growing a field is additive JSON (clients ignore extras — checkout SPA ignores capacity fields). Removing or renaming is ungoverned. A stranger integrating against `0.1.0` has no compatibility promise.

Honesty script comment: “Unversioned process probes. Keep host-only; do not grow pay-spec for them.” That is the only versioning policy written down.

---

## List doors: pagination, filters, cursors, totals

Grep of `apps/lazuar-pay/src` for `Skip(`, `Take(`, `cursor`, `page_size`, `pageSize`, `pagination`: **no matches**.

| List | Shape | Order | Filter | Total | Cursor |
|------|-------|-------|--------|-------|--------|
| GET `/v1/orgs/{orgId}/checkouts` | array | `CreatedAt` desc | **hardcoded** `PaymentLinkId == null` | no | no |
| GET `/v1/orgs/{orgId}/payment-links` | array | `CreatedAt` desc | org only | no | no |
| GET `/v1/orgs/{orgId}/products` | array | none (table order) | org only | no | no |
| GET `/v1/orgs/{orgId}/gateways` | `{ org_id, processors }` | `PayProviders.Listed` | none (all rails) | n/a (fixed 5/6) | n/a |
| GET `/v1/orgs/{orgId}/payments` | array | checkout `CreatedAt` desc in memory | org only | no | no |
| GET `/v1/orgs/{orgId}/receipts` | array | `CreatedAt` desc | org only | no | no |
| GET `/v1/whoami` | object with `tenants[]` | One’s order | none | n/a | n/a |

A second app that polls “all paid this hour” must download the org’s entire payments array and filter client-side. Hub’s list doors (out of this slice except as judgment) had page/page_size on keys. Focused Pay has **none**. Missing feat. Not a live lie.

---

## Checkout GET by id vs org-scoped lists. Cross-org 403 vs 404

Two different privacy stories, both live and tested.

**Unscoped GET `/v1/checkouts/{id}`**

1. No Bearer → **401** (even if the id does not exist). Closes 002 issue 062 existence oracle for anonymous callers.
2. Bearer, unknown id → **404**, One not called (`Get_unknown_is_404`, `SendCount == 0`).
3. Bearer, other org’s session, caller is not a member → **404** `"Checkout not found"` unless One 403 detail contains `"suspend"` (then 403 with that detail).
4. Member of the session’s org → 200 full `CheckoutSession`.

**Org-scoped lists** (`/v1/orgs/{orgId}/checkouts`, payment-links, products, payments, receipts, gateways, ready, one-webhook GET)

1. No Bearer → 401.
2. Bearer, not a member of **path** org → **403** `"Not a member of this org"` (or suspended detail). The list handler never looks up the other org’s rows (`List_other_org_is_403`, `Get_receipt_other_org_is_403`).
3. Member → 200 of **that** org only.

Receipt GET-by-id is org-scoped (403 on wrong path org; 404 if id missing **in this org**). Checkout GET-by-id is **not** org-scoped in the URL; membership is after fetch. A second app that stores `checkout_id` from mint can poll without remembering `org_id`. A second app that only has a token polls **public** GET `/v1/pay/{token}` (no Bearer, 404 if missing — that **is** an existence oracle for a secret 64-hex token, which is the point of the token).

---

## Public buyer doors — CORS, rate limit, no Bearer

Evidence already quoted. Fit:

| Question | Live answer |
|----------|-------------|
| Need Bearer? | No. Tests lock it. Buyers are not One humans. |
| Need `:5179`? | No for HTTP. Yes if you want the first-party chrome and if payment-link success URLs were minted with `Pay:CheckoutBaseUrl` (default `:5179`). |
| CORS for a stranger origin? | Not in Development defaults. Production must set `Pay:CorsOrigins`. Extra origin **replaces** laptop list. Server-side fetch does not care. |
| Rate limit GET? | No. |
| Rate limit POST start? | Yes, per token, default 20/min, in-process dictionary. |
| Can a second app host checkout? | Yes, as a client of GET + POST start, if CORS or server-side. Must send `slot_key` for payment-link tokens. Must handle `email_required`. Must poll GET for `status=paid` because there is no Plane C. |
| Can it skip start and only poll? | Poll GET works. Paid happens after PSP webhook (or Test inline fulfill on start). |

---

## pay-spec vs live Map* (honesty)

Script:

```25:26:scripts/check-pay-openapi-honesty.mjs
/** Unversioned process probes. Keep host-only; do not grow pay-spec for them. */
const IMPL_ONLY = new Set(["GET /health", "GET /ready"]);
```

It asserts OpenAPI ⊆ Map*, Map* ⊆ OpenAPI ∪ `IMPL_ONLY`, and a **short** field list: `CreateCheckoutRequest.provider`, `StartPayRequest.slot_key`, `WhoamiResponse.name`, `CreateProductRequest` schema exists, `WebhookDuplicate.duplicate`, `WebhookIgnored.ignored`, a `'201'` status somewhere.

Run on this SHA:

```
Pay OpenAPI honesty: 22 spec ops, 24 Map* (2 host-only probes).
```

CI job `pay` compiles tsp then runs the script (`.github/workflows/ci.yml:117-120`). `task pay:spec` does the same. 019 said there was **no** Pay honesty scrape and Hub `check-openapi-minimal-honesty.mjs` could not see Pay. Closed.

`packages/pay-spec/README.md` now says: grow tsp when a `/v1` door exists; unversioned probes stay host-only; honesty is the Pay script, not Hub `task gen`.

### Remaining drift (honesty-green does not mean comment-honest)

Honesty is **path-level** plus seven field/status checks. It does **not** fail on:

1. **TypeSpec comment on checkout list still says “Mixes one-off mints and payment-link children.”** Host filters `PaymentLinkId == null`. OpenAPI description copies the comment (`dist/openapi.yaml:114`). Live test `List_omits_payment_link_children`. **Spec comment lie.** Path is present so honesty is green.

2. **No `@useAuth` / `securitySchemes`.** English “Requires Bearer + writer” on some ops. Generated OpenAPI cannot express 401/403.

3. **No `PayProblem` / 4xx responses in OpenAPI.** Dist only documents 200/201 on success. Host returns 400/401/403/404/409/429/503.

4. **Idempotency fingerprint** not in spec (header is).

5. **No pagination query params** — honest omission of something that does not exist.

6. **Checkout list hardcoded filter** not in the TypeSpec model (no query to omit).

7. **`Get` singular gateway description** still says writer on PUT; GET is member. Fine, but OpenAPI has no 400 for missing `provider` even though tsp comment says it.

8. **Dist is gitignored.** CI compiles a fresh yaml then honesty-checks it. A developer reading a stale leftover without compiling can still be lied to. Workspace leftover on this machine **did** pass honesty (compiled 2026-08-28, 22 paths including payment-links, gateways, money, one-webhook, 201 on checkouts/products/payment-links). Process remaining: gitignored + leftover is still the worst of both unless 09 decides to commit yaml.

9. **Whoami `name`** is now in tsp (019 M21 closed).

10. **Start `slot_key`** is now in tsp model + query on GET (019 M1/M2 closed at source).

Host-only probes **stay out of spec** by design. Do not add `GET /health` or `GET /ready` to tsp to “make the count 24.”

---

## Compare 019-evals/08 (`9f04ad58`, 22 live / 13 tsp) to this SHA (`6d730d15`)

019 counts: live 22 Map* (19 `/v1` + 2 unversioned health/ready — they counted 22 including both health twins and `/ready`). Tsp **13**. Dist **11** and stale.

This SHA: live **24** Map* (added `PUT/GET /v1/orgs/{orgId}/one-webhook`; `/v1` count 22). Tsp **22**. Dist **22** when compiled. Honesty green.

### What 002 closed (API-contract slice; 001–080 marked resolved on the branch)

| 002 # | 019 finding | Live on `6d730d15` |
|-------|-------------|---------------------|
| 020 | Checkout idempotency racy, body-blind, always 201 | Race reload + fingerprint amount/currency/provider; replay **200**; 409 on amount change. Tests lock it. Fingerprint still omits URLs. |
| 031 | GET org checkouts mixes occupancy children | `PaymentLinkId == null`. Test omits children. **Tsp comment not updated.** |
| 062 | GET checkout 404s before Bearer (existence oracle) | Bearer first. 401 without token for unknown **and** known. |
| 066 | CORS tests do not prove `/v1/pay/*` or OPTIONS | `CorsTests.Public_pay_get/post/options_*` exist. |
| 067 | `dist/openapi.yaml` stale vs tsp | Honesty after compile in CI/`task pay:spec`. Path-level green. |
| 068 | GET `/gateway` without provider returns list envelope | **400** `"provider is required"`. Test + tsp comment. |
| 069 | TypeSpec catalog create has no body | `CreateProductRequest` in tsp; honesty checks schema exists. |
| 070 | TypeSpec `CreateCheckoutRequest` omits `provider` | tsp has `provider`; honesty checks yaml field. |
| 071 | Start `slot_key` missing from tsp / dist no body | tsp `StartPayRequest.slot_key` + GET query; honesty checks field. |
| 072 | Spec 200 vs host 201 | tsp `@statusCode 201` plus 200 replay on checkouts. Dist has `'201'`. |
| 073 | Webhook spec `{ ok }` required; live duplicate/ignored | tsp union `PspWebhookResult` / `OneWebhookResult`. Honesty checks duplicate/ignored fields. |
| 074 | Whoami `name` on wire, not in tsp | tsp `name?: string`. Honesty checks. |
| 075 | GET receipt-by-id mapped, untested, unused, **narrower** | Tested; **same fields as list**; SPA still unused. |
| 076 | Unversioned `/ready` mapped and untested | `HealthTests.Unversioned_ready_returns_200_on_inmemory`. Still host-only in spec. |
| 078 | `/v1/orgs/{id}/ready` dummy `ready: true` | `IsReady` uses pause + vault/test. Tests lock false. |
| 049 | CORS laptop-only | `Pay:CorsOrigins`; Production throws if empty; extra origin replaces laptop list. |

002 also added **two Maps** 019 did not have: `PUT/GET /v1/orgs/{orgId}/one-webhook` (HEAD stores per-org One secrets). Path-level tsp grew with `9e5fa8e6` `fix(pay-spec): align TypeSpec with live Pay /v1 doors`.

### What remains (019 kernel + this slice)

019 §Kernel doors still missing is **still true** on this SHA:

- No Pay-native machine credential. No `MapPost` for keys. `Rows.cs` has no key table. TypeSpec has no `sk_` scheme. Staff Bearer is a human access_token forwarded to One.
- No outbound `payment.completed`. `Fulfillment` writes charge/journal/receipt in-process. IsolationTests still ban `GatewayPaymentCompletedIntegrationEvent`.
- Idempotency header still missing on the mint doors the SPA actually POSTs (payment-links, products).

019 gaps that honesty-green does **not** close: problem model, status enums, provider enum in OpenAPI (comments only), `@useAuth`, pagination, `@repo/pay-types-ts`, IsolationTests still do not open `main.tsp`.

019 “seven product doors missing from TypeSpec” is **false on this SHA**. Those paths are in tsp.

---

## Hub museum second-app (006) — steal judgment, not code

006 shipped a Next sample against **Hub** `apps/lazuar-api` on **8080** `/api/v1`. That host is museum. Do not copy `IntegrationEndpoints.cs`. Steal the **product shape**.

### What Hub exposed that focused Pay does not

| Hub (006/04, 006/05, 006/06) | Focused Pay live |
|------------------------------|------------------|
| `Authorization: Bearer sk_test_…` / `sk_live_…` (Hub-minted integration key) | Bearer is a **human** One access_token. No Pay `sk_`. One’s `lzr_sk_` is One’s mint (012/08); Pay does not advertise it as a Pay product on 8081. |
| Scope `payments.checkouts:write` / `:read` | No scope catalog. Writer = whoami `owner`\|`admin`. |
| `POST /api/v1/integrations/payments/checkouts` — amount, currency, description, customer_email, success_url, cancel_url, metadata, gateway_name | `POST /v1/checkouts` — org_id, provider **required**, amount, optional URLs, **no description, no customer_email at mint, no metadata bag** |
| Response `checkout_id` + **`checkout_url`** (processor or Hub hosted) | Response `id` + `public_token` + `status=open`. **No checkout_url.** Buyer hop is a second call. |
| `GET /api/v1/integrations/payments/checkouts/{id}` with key | `GET /v1/checkouts/{id}` with human member Bearer |
| Outbound `POST {your_url}` `X-Lazuar-Event: payment.completed` + `X-Lazuar-Signature: t=,v1=` envelope `{ id, event_type, created_at, data }` | **None.** Fulfillment is Linux-in-the-room. Second app must poll. |
| Provision `POST /api/v1/one/integrations/workspaces/provision` + `X-Lazuar-Provision-Key` returns `sk_test_` + `whsec_` once | **None** on Pay. Workspace create is One `POST /tenants` from merchant SPA. Vault is human PUT gateway. One `whsec_` is PUT one-webhook. |
| Sample verify recipe matching Hub `OutboundWebhookSignature` | Nothing to verify from Pay→app. Plane A verifier is for **One→Pay**. Do not tell a second app to POST `/v1/one/webhooks`. |

Hub error map was ProblemDetails + `IDEMPOTENCY_CONFLICT` codes. Pay is `{ status, title, detail }` with English `"idempotency key reused with a different body"`. Steal “header wins over body” (already live on checkouts). Steal “same key different fingerprint → 409”. Steal “poll GET if you must, but prefer signed webhook.” Steal “plain `fetch`, not `@repo/api-types-ts`” (IsolationTests already ban the Hub package).

Do **not** steal Hub path `/api/v1/integrations/…`, Hub `sk_test_` prefix (collides with Stripe BYOK), Hub MediatR command, Hub `@repo/api-types-ts`.

---

## Disagreements with 012 / 013 / 019 (named)

Live files win.

| Paper | Claim | Live `6d730d15` |
|-------|-------|-----------------|
| 012/04 (historical) | pay-spec is health-only | False. 22 `/v1` ops. 019 already said this; still true. |
| 012/08 | Merchant M2M into Pay `/v1` by presenting `lzr_sk_`; Pay introspects via One `GET /me` | **Not implemented as a product.** Bearer is forwarded; there is no documented “use a One key as Pay M2M” door, no scope check, no sample. 02 owns whether forwarding accidentally works. |
| 013/01 | Dummy `/v1/orgs/{id}/ready` checking `member` is “has the tenant,” not “cannot charge.” First-slice production-ready is the **hosted cashier** dogfood sentence, not second-app. | Org-ready now includes pause + vault/test. Production-ready **gate** in 013 still does not include M2M. This 020 program asks the second question 013 parked. |
| 013/01 | Bezos door: public `/v1` from day one | `/v1` exists. It is cashier-shaped. Bezos-the-door without a machine caller is a door with a human bouncer. |
| 011/08 | Anything you will sell is a versioned HTTP API. Own UI is a client of `/v1`. | Merchant and checkout **are** HTTP clients of `/v1` (good). A stranger cannot use the same mint the way Hub’s sample did (missing feat). |
| 019/08 | 22 live / 13 tsp; dist 11 stale; GET `/gateway` aliases list; receipt GET narrower; org-ready dummy; checkout GET 404 before Bearer; idempotent replay 201; no Pay honesty script | All path-level and those bugs **closed**. Kernel doors and payment-link idempotency **remain**. Tsp checkout-list comment is a **new remaining lie** after 031’s host fix. |
| 019/08 | Merchant does not call `POST /v1/checkouts` | Still true. README curl still teaches it. That is now the **second-app hatch**, not a README lie about the SPA. |
| issues/002 index | 001–080 resolved | API-relevant 020/031/062/066–076/078 closed in live C# as quoted. Kernel items were **never in 002**. |

---

## How to solve (analysis, not a patch)

Smallest hatch so another app can mint a checkout and poll paid **without cloning merchant SPA**. Sequence. Refuse cathedral.

### What already exists (do not rebuild)

1. `POST /v1/checkouts` with writer Bearer, provider, amount, optional `success_url`/`cancel_url`, `Idempotency-Key`.
2. Response `id` + `public_token` + `status`.
3. Buyer: redirect to `{Pay:CheckoutBaseUrl}/c/{public_token}` **or** call `GET/POST /v1/pay/{public_token}` from the app’s own page.
4. Poll `GET /v1/checkouts/{id}` (member Bearer) until `status=paid`, **or** poll public `GET /v1/pay/{token}` (no Bearer) until `status=paid`.
5. Snake_case JSON. Problem `detail` on 4xx.

That is already Bezos-the-door **for a human token**. A toy curl with Ada’s access_token can do it today. A production second app cannot, because it must not hold a human password grant.

### Sequence (smallest, in order)

**Hatch A — document the live human-token path (09, not a host change).** One page: POST checkouts, GET by id, public start, poll. State honestly: “requires owner/admin user JWT; not a product.” Do not generate an SDK. Do not import Hub types.

**Hatch B — M2M Bearer on the same Maps (02, not a new mint verb).** Accept One `lzr_sk_` the way 012/08 already described: forward Bearer to One `GET /me` + `authz/check member`, then keep the writer overlay **or** map a One scope if One grows `payments.checkouts:write`. **Do not** mint homemade `sk_test_` inside Pay (012: prefix collision with Stripe BYOK; IsolationTests / 011 refuse Hub credential tables). **Do not** add `/v1/orgs/{orgId}/keys` on Pay. Keys stay One’s. Pay remains one binary. This is the Bezos door with a machine bouncer.

**Hatch C — poll is enough for v1 of the hatch; Plane C is 03.** Do not block mint+poll on outbound webhooks. Tell the second app to poll GET checkout with backoff. Add `payment.completed` later so they can stop polling. Do not resurrect `GatewayPaymentCompletedIntegrationEvent`.

**Hatch D — CORS / success_url.** If the second app hosts checkout in a browser, add its origin to `Pay:CorsOrigins` (already a config, 06). If it is server-side, CORS is irrelevant. Prefer `POST /v1/checkouts` with **its** `success_url` so buyers do not land on `:5179`. Do not force payment-links on a second app (those bake `CheckoutUrls.Base`).

**Hatch E — idempotency on payment-links only if the second app is told to use links.** Prefer they use `POST /v1/checkouts` (already has the header). Adding Idempotency-Key to payment-links is P2 for SPA double-click, not the stranger hatch.

**Hatch F — errors.** Keep `{ status, title, detail }`. Optionally set `Content-Type: application/problem+json` and add `type` later; do not wait on RFC7807 purity. Add the model to tsp so generated clients parse `detail`. Do not invent Hub `IDEMPOTENCY_CONFLICT` code enums unless a client needs a machine code — English detail is the live contract.

**Hatch G — honesty.** Keep host-only probes out of spec. Fix the **one remaining tsp comment** on checkout list (host omits children). Do not grow tsp for pagination that does not exist. Do not add `/v2`.

### Refuse

- MediatR / `IEnumerable<IHostedRail>` / Hub `@repo/api-types-ts` / Kiota cathedral SDK from today’s tsp as a “product.”
- Copying Hub `/api/v1/integrations/payments/checkouts` path or `sk_test_` Pay mint.
- ProjectReference `apps/lazuar-api`.
- Making buyers One humans so the second app can “just use whoami.”
- Shrinking `POST /v1/checkouts` because the SPA does not call it. It is the hatch.
- Putting outbound webhooks in this slice’s “must ship before anyone can poll.”
- Committing to page/cursor on every list before a second app exists that dumps 10k rows.

### Sequence diagram (analysis)

```
second-app (server)                    Pay :8081                         One :8080                      buyer
    |                                      |                                 |                            |
    |  Authorization: Bearer lzr_sk_  (B)  |  GET /me + authz/check          |                            |
    |  POST /v1/checkouts                  |-------------------------------->|                            |
    |  Idempotency-Key: order:{id}         |                                 |                            |
    |<-- 201 { id, public_token, status }  |                                 |                            |
    |                                      |                                 |                            |
    |  302 Location: own page or :5179/c/{token} ------------------------------------------------------> |
    |                                      |                                 |   GET /v1/pay/{token}      |
    |                                      |<--------------------------------------------------------------|
    |                                      |                                 |   POST /v1/pay/{token}/start|
    |                                      |                                 |   (PSP hosted)             |
    |                                      |   Plane B webhook (PSP)         |                            |
    |                                      |   Fulfillment in-process        |                            |
    |  GET /v1/checkouts/{id}              |                                 |                            |
    |<-- 200 { status: paid }              |                                 |                            |
```

Step (B) is the missing product. Today it is a human JWT. Everything else is live.

---

## Ranked holes in THIS slice

Bug = live lie (host vs tests vs spec comment vs SPA that will mis-talk). Missing feat = not on 8081, needed for a stranger. Refuse = do not do.

| Rank | Kind | Sev | Hole |
|------|------|-----|------|
| 1 | missing feat | **P0** | No machine auth scheme on mint/poll. Stranger cannot call `POST /v1/checkouts` / `GET /v1/checkouts/{id}` as a product. Human writer JWT is cashier-shaped. **02 owns the key; this slice names the door is otherwise ready.** |
| 2 | missing feat | **P0** | No outbound paid event. Poll only. **03 owns dispatcher; this slice names GET poll is the only paid signal.** |
| 3 | missing feat | **P1** | `POST /v1/checkouts` response has no `checkout_url`. Second hop to public start or `:5179` is mandatory. Hub had one hop. Document it; optionally return `{ pay_url }` built from `CheckoutBaseUrl` + token (additive JSON, not a new Map). |
| 4 | missing feat | **P1** | Idempotency-Key **missing on payment-links** (and products). Spec admits it. SPA double-click duplicates. Stranger hatch should use checkouts anyway. |
| 5 | missing feat | **P1** | No pagination/filters/totals on any list. Fine for dogfood shops. Not a list product. |
| 6 | bug | **P1** | TypeSpec/OpenAPI **description** for `GET /v1/orgs/{orgId}/checkouts` still “Mixes one-off mints and payment-link children.” Host + test omit children. Honesty-green. Comment lie. |
| 7 | missing feat | **P1** | No `@useAuth` / 4xx schemas / `PayProblem` in tsp. Path honesty ≠ client-usable contract. |
| 8 | missing feat | **P1** | Error envelope is not RFC7807 (`type`/`instance`/`application/problem+json`). Stable `{ status, title, detail }` is enough for SPA; a stranger generated-client has nothing. Mixed `Results.Ok` anonymous on health/ready/webhook variants. |
| 9 | missing feat | **P1** | Checkout idempotency fingerprint omits `success_url` / `cancel_url` / `product_id`. Replay 200 can stick the first URLs. |
| 10 | missing feat | **P2** | No versioning/deprecation story beyond “there is `/v1` and `0.1.0`.” |
| 11 | missing feat | **P2** | GET `/v1/pay/{token}` unrate-limited; start limiter is in-process only. |
| 12 | missing feat | **P2** | CORS default does not include a second-app origin; Production list **replaces** laptop origins (easy to break `:5179` while adding a stranger). Config, not a new Map. |
| 13 | missing feat | **P2** | No metadata bag on checkout create (Hub had `order_id` in metadata for webhook). Poll+local DB must key by `Idempotency-Key` / returned `id`. |
| 14 | bug-shaped | **P2** | Dist yaml gitignored; honesty is CI-only. Local leftover can rot if someone skips `task pay:spec`. |
| 15 | refuse | — | MediatR, Hub `@repo/api-types-ts`, cathedral SDK, Pay-minted `sk_test_`, `/v2`, copying Hub integrations path, shrinking `POST /v1/checkouts`, adding unversioned probes to tsp, making buyers One humans. |

P0 in this slice is “stranger cannot authenticate as a product” and “stranger cannot be told paid except by poll.” The Maps for mint+poll **exist**. 002 made them honest for the cashier. 020’s remaining work is the bouncer and the receipt-of-paid, not a fifth checkout verb.

---

## Isolation / cathedral (must stay red)

```4:17:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
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

Vite apps must not depend on `@repo/api-types-ts`. How-to-solve above does not touch those strings.

---

## Appendix: honesty vs 019 path table (so the evidence is not summarized away)

019 tsp ops (13): health, whoami, org ready, POST checkouts, GET checkout, GET/POST public pay, POST/GET products, PUT/GET gateway, POST psp webhook, POST one webhook.

019 host-only extra (product doors missing from tsp): GET org checkouts, POST/GET payment-links, GET gateways, GET payments, GET receipts, GET receipt by id. Plus unversioned probes.

This SHA tsp **adds** (now present): GET org checkouts, POST/GET payment-links, GET gateways, GET payments, GET receipts, GET receipt by id, PUT/GET one-webhook. Unversioned probes still absent from tsp (correct).

019 dist 11 paths, fixture blurb, no Gateways, no start body. This SHA dist (compiled) 22 paths, description “Checkouts persist in Postgres; paid via verified PSP webhook,” Gateways tag, PaymentLinks tag, Money tag, start `requestBody` optional `StartPayRequest`, `'201'` on checkouts/products/payment-links.

019 POST checkouts documented as 200. This SHA tsp:

```297:309:packages/pay-spec/main.tsp
  /** Merchant creates a checkout. org_id is the One tenant id. Requires Bearer + writer. 201 on mint; 200 on idempotent replay. */
  @post
  @route("/checkouts")
  create(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateCheckoutRequest,
  ): {
    @statusCode statusCode: 201;
    @body body: CheckoutSession;
  } | {
    @statusCode statusCode: 200;
    @body body: CheckoutSession;
  };
```

Matches live `created ? 201 : 200`.

---

## Appendix: quoted CORS / limiter / providers (public contract adjacent)

Providers the host accepts:

```26:36:apps/lazuar-pay/src/Lazuar.Pay/Rails/PayProviders.cs
    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
    }
    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;
```

Listed processors add `test` only when `!IsProduction()` (`AllowsTest` = Development or Testing). Capability `"hosted_link"`. Unknown provider → 400 on mint and on Plane B.

Checkout base for payment-link children:

```18:32:apps/lazuar-pay/src/Lazuar.Pay/PublicPay/CheckoutUrls.cs
    public static string Base(IConfiguration config, IHostEnvironment env)
    {
        var raw = config["Pay:CheckoutBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }
        if (env.IsEnvironment("Testing"))
        {
            return "http://localhost:5179";
        }
        throw new InvalidOperationException("Pay:CheckoutBaseUrl is required");
    }
```

Not a Map*. Public start 400/503 depends on it. Not in TypeSpec (correct: config, not a path). A second app using checkout-create URLs still wants this set if it ever mints a **link**.

---

## Appendix: who calls which door today (cashier vs stranger)

Merchant `:5178` (`payApi.ts` + pages, 019 inventory still accurate except where 002 changed chrome):

- GET `/v1/whoami`
- GET `/v1/orgs/{id}/gateways`, PUT `/v1/orgs/{id}/gateway`
- GET `/v1/orgs/{id}/payment-links`, POST `/v1/payment-links`
- POST `/v1/orgs/{id}/products`
- GET `/v1/orgs/{id}/payments`, GET `/v1/orgs/{id}/receipts`

Merchant does **not** call: health, ready, org-ready, POST `/v1/checkouts`, GET `/v1/checkouts/{id}`, GET org checkouts, GET products, GET singular gateway, GET receipt by id, public pay, Plane A/B, one-webhook PUT/GET.

Checkout `:5179`: GET `/v1/pay/{token}?slot_key=`, POST `/v1/pay/{token}/start` with `{ name, email, slot_key }`. No Bearer.

Stranger product: **no first-party client in this repo.** Hub sample was `examples/hub-cashier-next` against museum 8080. 09 owns whether a focused-Pay example appears. This slice’s hatch is the Maps above, not a new Vite app.

---

End of 01. Live files on `6d730d15` are the product. 002 made `/v1` an honest cashier door. It did not make `/v1` a stranger product. The mint+poll Maps are the Bezos door; the bouncer is still a human.
