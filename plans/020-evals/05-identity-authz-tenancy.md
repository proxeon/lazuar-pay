# 05 — Identity, authz, tenancy: One coupling vs a Pay that other apps can use

**Family:** 020-evals  
**Paper:** 05 — Identity / authorization / tenancy. MemberGate, writer, One coupling vs a Pay a second app can swallow.  
**Date:** 28 August 2026  
**Type:** Uncondensed evaluation. **Not an implementation.** **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`. Live files on this SHA are authority. Do not copy `Modules/One`. Do not copy this file into `plans/019-evals`. Do not treat 019 as live.

| | |
|--|--|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `fix/002-pay-host-bugs` |
| HEAD (short) | `6d730d15` |
| Subject | `fix(pay): store per-org One webhook secrets` |
| Sibling One | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` — `/api/v1` on **8080**. Mints `lzr_sk_`. `GET /me`. `POST /tenants/{id}/authz/check`. Outbound tenant webhooks. `MembershipRoles` = `owner` \| `admin` \| `member`. Development `App:CorsOrigins` includes `:5178`. Production empty `App:CorsOrigins` fails boot. |
| Parent index | [README.md](./README.md) |
| Historical identity paper | [019-evals/07-identity-authz-cors.md](../019-evals/07-identity-authz-cors.md) on `9f04ad58` / `feat/018-merchant-shell` |

This paper asks a different question than 019/07. 019 asked whether the **hosted cashier** (merchant SPA + checkout SPA + One humans as staff) was honest about CORS, HMAC, suspend copy, and writer overlay. 002 closed those as **hosted-cashier bugs**. 020 asks whether Pay is a **kernel other products can swallow** without cloning this repo: secret key / M2M, outbound `payment.completed`, a clean `/v1`, docs/sample. This slice is the identity half of that kernel question.

**Standing law used as the ruler (not as evidence that the code matches):**

- One Pay binary, one Pay database. Bezos is the **door** (`/v1`); Linux is the **room** (in-process).
- Pay talks to One over HTTP. No PAT, no OpenFGA admin, no `SELECT` from One.
- Buyers are not One humans.
- Staff today **are** One humans. That is Consumer-0, not a law that every future Pay caller must be a One human.
- Path `{orgId}` + One `POST /tenants/{id}/authz/check` is authorization SoT for merchant routes. `X-Lazuar-Tenant-Id` is a hint only.
- One tenant id **is** Pay `org_id`. Same bytes. No Pay `organizations` / `users` / `members` tables.
- `VIEWER` is not a One tenant role. One product roles are `owner` \| `admin` \| `member`.
- IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`, `Modules.One`).
- Receipt ≠ tax invoice. SST / LHDN stay off the pay path. Out of this slice except as IsolationTests bans.

Kernel doors that are **out of 002** and **in 020**: M2M Bearer that is not a human JWT; a second-app origin that is not `:5178`/`:5179`; a Pay that boots a shop for an app that already has its own users. This paper names those as bugs, missing features, or refuse. Live files win.

---

## Coordinates

Focused Pay host is `apps/lazuar-pay` on **http://localhost:8081**. Merchant Vite is `apps/lazuar-pay-merchant` on **:5178** (`strictPort`). Checkout Vite is `apps/lazuar-pay-checkout` on **:5179**. Identity plane is sibling One: API **:8080**, product login **:5175**, Zitadel issuer **:8085**. Old Hub ops **:3003** and portal **:3004** are a different product and must stay off Pay CORS.

Pay does **not** run ASP.NET JWT middleware. There is no `AddAuthentication` / `AddJwtBearer` in `Program.cs`. Staff identity is: browser PKCE against Zitadel → JWT `access_token` in `sessionStorage` → `Authorization: Bearer` on Pay → Pay forwards that same header to One `GET /me` and `POST …/authz/check`. One says 200 or Pay maps the failure. That is the whole AuthN loop for **Consumer-0** (this merchant SPA). It is also the only AuthN loop the host knows. A second app that is not a One human SPA must still present a Bearer One will accept, or Pay 401s / 400s / 503s.

One webhook HMAC is a **separate** door: `POST /v1/one/webhooks`, no Bearer. 002 closed the dialect (One `v1=` + `X-Lazuar-Timestamp`; combined `t=,v1=` kept as compat). Per-org `whsec_` is now `PUT /v1/orgs/{orgId}/one-webhook` (writer) stored as `OrgSettings.OneWebhookCiphertext`. Process `Pay:OneWebhookSecret` is the one-shop fallback. This paper does not re-litigate Plane A except where identity (who may PUT the secret; who is paused) touches it.

Health never calls One. `/health` and `/v1/health` are `{ status: "ok" }`. Unversioned `/ready` is Postgres `CanConnect`. `/v1/orgs/{orgId}/ready` **does** call One (member) and then reads `charges_paused` + vault.

---

## Files opened

### Pay host — Identity

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Lazuar.Pay.csproj`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneAuthz.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneCallResult.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeMapper.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneMeResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiResponse.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/src/Lazuar.Pay/Properties/launchSettings.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/README.md`
- `apps/lazuar-pay/docker-compose.pay.yml`

### Pay host — gates on money / catalog / public / Plane A secret

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs`

### Pay host — tests

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`

### Merchant (`:5178`)

- `apps/lazuar-pay-merchant/src/auth/bearerToken.ts`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `apps/lazuar-pay-merchant/src/auth/oidcConfig.ts`
- `apps/lazuar-pay-merchant/src/auth/RequireAuth.tsx`
- `apps/lazuar-pay-merchant/src/lib/staffDisplay.ts`
- `apps/lazuar-pay-merchant/src/lib/roles.ts`
- `apps/lazuar-pay-merchant/src/lib/oneApi.ts`
- `apps/lazuar-pay-merchant/src/lib/payApi.ts`
- `apps/lazuar-pay-merchant/src/lib/homePath.ts`
- `apps/lazuar-pay-merchant/src/lib/workspaceStatus.ts`
- `apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx`
- `apps/lazuar-pay-merchant/src/pages/CallbackPage.tsx`
- `apps/lazuar-pay-merchant/src/pages/HomePage.tsx`
- `apps/lazuar-pay-merchant/src/App.tsx`
- `apps/lazuar-pay-merchant/src/main.tsx`
- `apps/lazuar-pay-merchant/src/locks.test.ts`
- `apps/lazuar-pay-merchant/scripts/register-spa.sh`
- `apps/lazuar-pay-merchant/package.json`
- `apps/lazuar-pay-merchant/vite.config.ts`
- `apps/lazuar-pay-merchant/README.md`

### Checkout (`:5179`) — anonymity proof

- `apps/lazuar-pay-checkout/src/App.tsx`
- `apps/lazuar-pay-checkout/src/main.tsx`
- `apps/lazuar-pay-checkout/src/locks.test.ts`
- `apps/lazuar-pay-checkout/package.json`
- `apps/lazuar-pay-checkout/vite.config.ts`

### Contract / isolation / historical (honesty of ticks, not authority)

- `packages/pay-spec/main.tsp`
- `plans/011-new-lazuar-pay/08-bezos-door.md`
- `plans/012-one-to-pay/06-tenant-org.md`
- `plans/012-one-to-pay/07-authz-roles.md`
- `plans/012-one-to-pay/08-machine-keys.md`
- `plans/019-evals/07-identity-authz-cors.md`
- `issues/002/README.md` (001–080 resolved as hosted-cashier bugs)
- `issues/002/030-p1-writer-is-me-role-overlay-not-authz-admin.md`
- `issues/002/049-p1-cors-allow-list-is-laptop-only.md`
- `issues/002/064-p2-one-400-429-on-authz-check-become-pay-503.md`
- `issues/002/065-p2-suspended-tenant-copy-says-not-a-member.md`
- `issues/002/078-p2-org-ready-is-still-dummy-ready-true.md`

### Sibling One (what Pay must speak — not copied into Pay)

- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Platform/MeEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzObjectRules.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/MembershipRoles.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Tenancy/TenantAccessService.cs`
- `lazuar-one/deploy/dev/openfga/model.fga`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.Development.json`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/appsettings.Production.json`
- `lazuar-one/apps/lazuar-api/tests/Lazuar.One.Api.Tests/Integration/TenantSuspendReactivateTests.cs`

**Not opened on purpose:** Hub `Modules/One/**`, rail HTTP adapters beyond the MemberGate call sites, occupancy algorithm internals, TypeSpec as a whole paper (only `WhoamiResponse` / `OrgReadyResponse`), 02-machine-keys-m2m.md (sibling 020 slice — this paper hands off the door, does not steal it).

---

## 1. What exists on this SHA (live)

### 1.1 Composition (`Program.cs`)

`OneOptions` binds from config section `One`. Typed `HttpClient<OneClient>` is registered. CORS is **no longer hardcoded** in `Program.cs`. Pipeline is `PayCors.Add(builder)` then `UseCors()` then the maps. Identity maps are `MapWhoami`, `MapOrgReady`, `MapOneWebhooks`. Money maps sit beside them. There is **no** cookie auth, **no** JWT bearer handler, **no** `lazuar_auth`.

```30:31:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
builder.Services.AddHttpClient<OneClient>();
```

```57:84:apps/lazuar-pay/src/Lazuar.Pay/Program.cs
PayCors.Add(builder);
var app = builder.Build();
// ...
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

Host csproj references EF + Npgsql + Stripe.net. No Identity package. No OpenFGA SDK. No project reference to `apps/lazuar-api` or sibling One.

`.env.example` is the operator contract:

```
# One HTTP façade (no PAT, no OpenFGA admin).
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5
```

Default timeout is five seconds (`OneOptions.TimeoutSeconds`). Tests override to two (`PayApiFactory`). Transport failure and timeout both surface as Pay **503** `"Identity provider unreachable"` on whoami and MemberGate. That is fail-closed for **new** merchant work. Captured money / Plane B does not wait on One (out of this slice except as the production implication: a second app's buyer path is not gated on One being up; a second app's **mint** path is).

Grep of `apps/lazuar-pay/src` for `ZITADEL_PAT`, `OpenFga`, `AddAuthentication`, `AddJwtBearer`, `lazuar_auth`: **empty**. Still true. Pay never holds a Zitadel PAT.

### 1.2 `OneOptions` + `OneClient`

```3:11:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs
public sealed class OneOptions
{
    public const string Section = "One";

    /// <summary>One API prefix, e.g. http://localhost:8080/api/v1. Client appends /me.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080/api/v1";

    public int TimeoutSeconds { get; set; } = 5;
}
```

`OneClient` constructor trims BaseUrl, appends `/`, defaulting to `http://localhost:8080/api/v1/` if blank. Timeout `<= 0` becomes 5. Two verbs:

1. `GetWhoamiAsync` — `GET me` with forwarded `Authorization` and optional `X-Lazuar-Tenant-Id`. Maps One snake_case `/me` through `OneMeMapper`. Missing `user_id` on a 200, or JSON that will not parse, becomes Pay-side **503** (fail closed). One 401/403/other non-200 pass as `StatusCode` + truncated `detail`.
2. `CheckMemberAsync` — `POST tenants/{orgId}/authz/check` with body `{ relation: "member", object: { type: "tenant", id: orgId } }`. **Omits `user_id`.** One infers the subject from a JWT. One **requires** `user_id` when the Bearer is an API key (`lzr_sk_`). That is the kernel door this slice hands to 02.

There is **no** `CheckWriterAsync`. There is no `relation: "admin"` POST. Writer is a second hop through `/me` (see §1.5).

`SendAsync` maps:

| Transport | `OneCallResult` |
|-----------|-----------------|
| `TaskCanceledException` | `TimedOut = true` (no status) |
| `HttpRequestException` | `TransportFailed = true` |
| HTTP 200 | `onOk` mapper |
| other HTTP | `StatusCode` + `Detail` from JSON `detail` / `title` / `message`, truncated to 200 chars |

JSON `detail` extraction is why MemberGate can now pass through `"Tenant is suspended."` and `"The value 't1' is not valid."` instead of a canned 503.

### 1.3 Bearer extraction (host)

`Bearer.TryGet` (`Identity/Client/Bearer.cs:5-20`) reads `Authorization`, requires a `Bearer ` prefix (case-insensitive), and rejects empty remainder. It does **not** look at cookies. It does **not** inspect JWT shape. Opaque `lzr_sk_…` would pass this gate and be forwarded to One. JWT-likeness is a **merchant SPA** rule (`pickApiBearerToken`), not a host rule. That split is correct: the host must accept whatever One accepts.

A second app that sends `Authorization: Bearer lzr_sk_…` therefore **reaches** `OneClient`. Whether One then 200s `/me` and 400s `authz/check` is One's product, not Pay's parser. Pay currently has **no** hermetic test that this prefix is forwarded. Grep of `apps/lazuar-pay` tests for `lzr_sk_`: **empty**. Grep of `apps/lazuar-pay/src` for `lzr_sk_`: **empty**. The host does not special-case the prefix. Special-casing would be wrong (do not mint a Pay key). Forwarding without a test is a kernel gap, not a cathedral.

### 1.4 `GET /v1/whoami` → One `GET /me`

`WhoamiEndpoints.Handle`: missing Bearer → 401 `"Missing bearer token"` and **does not call One** (`Whoami_without_authorization_is_401_and_skips_one`). With Bearer, it forwards `Authorization` and optional `X-Lazuar-Tenant-Id` to `OneClient.GetWhoamiAsync`.

`OneMeMapper.ToWhoami` maps One snake_case `/me` into Pay's `WhoamiResponse`:

| One `/me` | Pay `/v1/whoami` |
|-----------|------------------|
| `user_id` | `user_id` |
| `email` | `email` |
| `name` | `name` (now also in `packages/pay-spec` `WhoamiResponse` — 019 G9 / 002-074 closed) |
| `is_platform_admin` | `is_platform_admin` (forwarded, **unused** by merchant chrome; do not later treat as Pay superuser) |
| `active_tenant_id` | `active_org_id` |
| `active_role` | **dropped** |
| `tenants[].id/slug/name/role/status` | same, skip rows with empty `id` |
| `tenants[].permissions` | **dropped** |

Missing `user_id` on a 200 from One → mapper returns null → Pay **503**. Fail closed. Timeouts and transport failures → 503 `"Identity provider unreachable"`. One 401 → Pay 401 `"Identity provider rejected the token"`. One 403 → Pay 403 `"Identity provider forbade this caller"`. Other codes → 503 `"Identity provider failed"`.

**Whoami does not pass through 400 or 429.** 002-064 closed that mapping on **MemberGate** only. `WhoamiEndpoints.Map` is still 401 / 403 / else-503. A second app that presents a malformed token and gets One 400 will see Pay 503. Whoami 403 mapper is still **untested** (`WhoamiTests` has 401, timeout, 500; no 403 case; no `lzr_sk_` case).

This is Mode U: Pay does not introspect the JWT. One is the IdP resource server. A second app's machine key is Mode M on **One**; Pay is a replay proxy. Whoami for a live `lzr_sk_` against live One **would work** (`GetMeForApiKey` returns `user_id` = key id, 0–1 bound tenant, synthetic `admin` or `member` from scopes). MemberGate would not (see §3).

Live One `GET /me` for humans (`MeEndpoints.GetMe`):

- Rejects SCIM principals.
- Missing `sub` → 401.
- API key → `GetMeForApiKey` (bound tenant only; header hint ignored; `is_platform_admin` still computed — keys should not be platform admin; One's code still calls `access.IsPlatformAdmin(user)`).
- Human: optional domain/SSO JIT join when `email_verified == true` (**`/me` can write**). Pay chrome must not hammer whoami on every keystroke. Current merchant calls it on session start, callback, home, and org layout. That is acceptable for v1; a second app polling whoami in a hot loop would JIT-join as a side effect of Pay's BFF. Document that.
- Memberships ordered by tenant **name**. `active_tenant_id` only if `X-Lazuar-Tenant-Id` matches a membership.

Pay whoami is a BFF convenience. It is not a Pay org catalog. IsolationTests forbids Pay `organizations` / `users` / `members` tables. The list of shops a staff member can open **is** One's `tenants[]`.

### 1.5 `GET /v1/orgs/{orgId}/ready` — no longer dummy

019: after member, always `{ ready: true }`. 002-078 closed that. Live `OrgReadyEndpoints.Handle`:

1. `MemberGate.RequireMemberAsync`.
2. Load `OrgSettings` for `ChargesPaused`.
3. `GatewayCredentials.Any(orgId)`.
4. `ready = !chargesPaused && (hasVault || PayProviders.AllowsTest(env))`.

TypeSpec now says: “Member ping plus whether this shop can take money: not charges_paused, and a vault row or Test allowed.” Tests lock: member 200 ready true (Testing allows Test, no vault needed); `Ready_false_when_charges_paused`; `Ready_is_false_without_vault_when_test_is_off` (unit of `IsReady`). Merchant SPA **still never calls this route**. Org chrome uses `GET /v1/whoami` + `tenants.find`. Leaving the route is fine as a **kernel probe** if docs say what `ready` means. Do not teach a second app that `ready` means “PSP is healthy” or “catalog exists.” It means “member, not paused, and a vault row or Test is allowed in this environment.”

`Ready_checks_path_org_not_header` still posts `id: path-org` even when the hint is `header-org`. Path is SoT. Live One `AuthzObjectRules.ValidateObject` requires `object.id` to be a **UUID equal to the path tenant**. Hermetic tests use `"t1"`. Fake One never validates GUIDs. Against live One, `/v1/orgs/t1/ready` would 400 from One. 002-064 now maps that to Pay **400** with One's detail (`Ready_400_when_one_400` asserts `"The value 't1' is not valid."`). That is the right mapping. It is also why a second app must send **real One tenant UUIDs**, not `"t1"`, not a Pay-local slug.

`Ready_429_when_one_429` asserts 429 `"Identity provider rate limited"`. `Ready_403_passes_through_suspended_detail` asserts One 403 `"Tenant is suspended."` is **not** rewritten as `"Not a member of this org"`.

### 1.6 `MemberGate` vs One `authz/check` vs writer

**Member** (`MemberGate.cs:8-47`):

1. Bearer required (401 `"Missing bearer token"`).
2. Empty `orgId` → 400 `"org_id is required"`.
3. `OneClient.CheckMemberAsync` POSTs `{ relation: "member", object: { type: "tenant", id: orgId } }` to `tenants/{orgId}/authz/check`, forwarding the hint header.
4. One 200 + `allowed: true` → pass.
5. Timeout / transport → 503 `"Identity provider unreachable"`.
6. Status switch:

| One | Pay |
|-----|-----|
| 401 | 401 `"Identity provider rejected the token"` |
| 403 | 403 `SuspendedDetail(detail)` or `"Not a member of this org"` |
| 400 | 400 One `detail` or `"Identity provider rejected the request"` |
| 429 | 429 `"Identity provider rate limited"` |
| 200 + `allowed: false` | 403 `"Not a member of this org"` |
| other | 503 `"Identity provider failed"` |

`SuspendedDetail` is a case-insensitive `IndexOf("suspend")`. Live One membership gate throws `"Tenant is suspended."` (`TenantAccessService` when `TenantStatuses.Suspended` and mode is not `AllowSuspended`). One's own test `Owner_suspend_blocks_member_mutate_allows_get_and_list` POSTs `authz/check` after suspend and expects 403 containing `"suspended"`. Pay now forwards that sentence. 002-065 closed the staff-copy lie **on the gate**. Chrome also grew `workspaceStatusBanner` (see §1.12).

The check body **still omits `user_id`.** `OrgReadyTests.Ready_when_one_allows_member` asserts `LastBody` does not contain `user_id`. That is correct for a human JWT. It is **fatal** for `lzr_sk_` against live One:

```218:223:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "user_id is required when authenticating with an API key.");
        }
```

Even if a second app passed a `user_id`, One rejects `user_id` equal to the key id (`"user_id must be a user subject, not the API key id."`). API-key `authz/check` is “this machine asks about a **human**.” It is not “this key is a member.” 012/08 already named this. Live MemberGate has not branched. Handoff to 02: **do not** have Pay forge a human `user_id`. **Do** decide whether merchant-M2M into Pay uses `/me` bound-tenant == path (skip `authz/check`) or a written subject policy. Until that door exists, a second app **cannot** mint as a machine against live One. Hermetic tests hide this because Fake One returns `{"allowed":true}` for any POST.

FGA on One (`model.fga`):

```
define owner: [user]
define admin: [user] or owner
define member: [user] or admin
```

So `relation=member` is true for owner, admin, and member. That is the read gate. It is **not** the write gate. One `AuthzObjectRules.TenantRelations` allow-lists `owner`, `admin`, `member`, `can_view`, `can_manage_members`, `can_manage_tenant`. Pay could POST `relation: "admin"` (FGA `admin` includes `owner`). It does not.

**Writer** (`MemberGate.cs:60-98`):

1. `RequireMemberAsync` first (so a non-member never gets a "writer" 403; and so a live `lzr_sk_` 400s **here**, before `/me`).
2. Then **`GET /me` again** and read `tenants.FirstOrDefault(t => t.Id == orgId)`.
3. No matching tenant → 403 `"Not a member of this org"`.
4. `Status` present and not `"active"` (case-insensitive) → 403 `"Tenant is suspended."` (the sentence is hardcoded, not One's `"Tenant is not active."` for provisioning/failed).
5. Role must be `"owner"` or `"admin"`. Else 403 `"Writer role required"`.
6. `/me` failure after member passed (`who.Value is null`) → 503 `"Identity provider failed"`.

002-030's **tests** landed: `Member_cannot_create_payment_link`, `Admin_can_create_payment_link`, `Suspended_writer_cannot_create_payment_link`. 002-030's **SoT** did not: there is still no `CheckWriterAsync`. Issue file body still says `status: open` while the 002 index marks 001–080 resolved. Live files win: the overlay is **stricter** than 019 (status check; admin proven) and still the **wrong SoT**. A member with a custom permission that One would treat as `can_manage_tenant` still cannot write — fail closed, probably right for Pay money. An admin who is briefly missing from `/me.tenants` but still FGA-admin cannot write — fail closed, noisy. Writer doubles One RTTs on every mint.

Merchant chrome `canWriteMoney` (`roles.ts:2-4`) is the same string test. Hide-button is **not** authorization; APIs still 403.

`VIEWER` does not appear in merchant source. One `MembershipRoles` is `owner` / `admin` / `member` only. OpenFGA `viewer` is on type **`app`** (OIDC registry), not tenant. Old Hub `VIEWER` is museum. IsolationTests does not ban the string `VIEWER`; the product still must not invent it.

### 1.7 Which routes are writer-gated vs member-gated vs none

| Route | Gate | Evidence |
|-------|------|----------|
| `POST /v1/checkouts` | **Writer** | `CheckoutEndpoints.cs:29` |
| `GET /v1/checkouts/{id}` | Bearer first, then **Member** of `session.OrgId`; non-suspend 403 → **404** (no cross-org existence) | `CheckoutEndpoints.cs:119-140` |
| `GET /v1/orgs/{orgId}/checkouts` | **Member** | `CheckoutEndpoints.cs:152` |
| `POST /v1/payment-links` | **Writer** | `PaymentLinkEndpoints.cs:27` |
| `GET /v1/orgs/{orgId}/payment-links` | **Member** | `PaymentLinkEndpoints.cs:132` |
| `PUT /v1/orgs/{orgId}/gateway` | **Writer** | `GatewayEndpoints.cs:31` |
| `GET /v1/orgs/{orgId}/gateway` | **Member** | `GatewayEndpoints.cs:175` |
| `GET /v1/orgs/{orgId}/gateways` | **Member** | `GatewayEndpoints.cs:213` |
| `POST /v1/orgs/{orgId}/products` | **Writer** | `CatalogEndpoints.cs:24` |
| `GET /v1/orgs/{orgId}/products` | **Member** | `CatalogEndpoints.cs:72` |
| `GET /v1/orgs/{orgId}/payments` | **Member** | `PaymentQueryEndpoints.cs:24` |
| `GET /v1/orgs/{orgId}/receipts` | **Member** | `PaymentQueryEndpoints.cs:71` |
| `GET /v1/orgs/{orgId}/receipts/{id}` | **Member** | `PaymentQueryEndpoints.cs:128` |
| `PUT /v1/orgs/{orgId}/one-webhook` | **Writer** | `OneWebhookEndpoints.cs:71` |
| `GET /v1/orgs/{orgId}/one-webhook` | **Member** | `OneWebhookEndpoints.cs:123` |
| `GET /v1/orgs/{orgId}/ready` | **Member** | `OrgReadyEndpoints.cs:25` |
| `GET /v1/whoami` | Bearer only (no org) | `WhoamiEndpoints.cs:15` |
| `GET /v1/pay/{token}` | **None** | `PublicPayEndpoints.cs:27-33` — no `MemberGate`, no Bearer |
| `POST /v1/pay/{token}/start` | **None** (anonymous); `ChargesPaused` 403 | PublicPay Start |
| `POST /v1/one/webhooks` | HMAC, not Bearer | `OneWebhookEndpoints.cs:19` |
| `POST /v1/webhooks/{provider}/{orgId}` | Plane B (out of this slice) | — |
| `GET /health`, `/v1/health` | None; never One | `HealthEndpoints.cs:9-10` |
| `GET /ready` | None; Postgres CanConnect | `HealthEndpoints.cs:11-21` |

Yes: **POST checkouts, PUT gateway, POST payment-links, POST products, PUT one-webhook secret are writer-gated.** **GET lists are member-gated.** That matches the standing law. The merchant mint path used by `:5178` is `POST /v1/payment-links` plus `POST /v1/orgs/{orgId}/products`, both writer.

`GET /v1/checkouts/{id}` 019 B4 (existence oracle) is closed as 002-062: missing Bearer is 401 even for unknown ids (`Get_without_bearer_is_401_for_unknown`). Unknown id **with** Bearer skips One and 404s (`Get_unknown_is_404` — still an existence oracle **for anyone who has a Bearer**, including a token for another org). After lookup, non-member 403 that is not suspend is rewritten to 404 so cross-org ids do not leak. Suspend 403 is passed through. A second app with a valid Bearer can still probe whether a checkout id exists (404 vs member 200). That is remaining P3, not a 019 regression.

`ChargesPaused` is checked on writer mint (`CheckoutEndpoints`, `PaymentLinkEndpoints`) and on **buyer start**. Pause is real **if the flag is set**. The flag is set from Plane A (002 closed dialect + per-org secret). Staff belt on suspend is now both One 403 pass-through **and** writer `/me.status != active`. Buyers never send Bearer. **The buyer belt is still only `ChargesPaused`.** That is correct product law (buyers are not One). A second app that never registers Plane A and never PUTs `one-webhook` will keep taking money after One suspends the workspace, unless it also stops minting (writer overlay status check) **and** has no leftover public links. Leftover public links are the reason Plane A exists.

### 1.8 Timeouts, 400/429 pass-through, suspend copy, no PAT — checklist for this SHA

| Requirement | Live |
|-------------|------|
| Timeouts | `OneOptions.TimeoutSeconds` default 5; client sets `HttpClient.Timeout`; `TaskCanceledException` → 503 `"Identity provider unreachable"`. Whoami test `Whoami_maps_one_timeout_to_503` (factory Delay 5s, client timeout 2s). |
| 400 pass-through | **MemberGate only.** `Ready_400_when_one_400`. Whoami still 503s One 400. |
| 429 pass-through | **MemberGate only.** `Ready_429_when_one_429`. Whoami still 503s One 429. |
| Suspend copy | MemberGate 403 with `"suspend"` in detail is passed through. Writer overlay hardcodes `"Tenant is suspended."` when `/me` status is not active. Chrome `workspaceStatusBanner`. Tests: `Ready_403_passes_through_suspended_detail`, `Suspended_writer_cannot_create_payment_link`. |
| No PAT | Grep empty in `apps/lazuar-pay/src`. `.env.example` says no PAT, no OpenFGA admin. `register-spa.sh` refuses to run on missing Ada JWT and tells the operator not to export `ZITADEL_PAT`. IsolationTests bans `Modules.One` / `namespace Lazuar.Pay.One;`. SPA `client_id` is public PKCE. |

---

## 2. What Pay requires of One to boot a shop

A shop, in this product, is **not** a Pay row created at “sign up.” 012/06 lock: Pay-side rows appear when the merchant creates a product or a charge exists — not at tenant create. “Boot a shop” for 020 means: staff can log in, pick or create a workspace, paste processor keys, mint a pay link, and a buyer can pay. That loop requires One to be **up** for every staff hop.

### 2.1 The One HTTP Pay actually issues (staff path)

| Step | Pay | One |
|------|-----|-----|
| Staff opens `:5178` | OIDC PKCE against Zitadel `:8085` (not One API). Login UI is One product login `:5175`. | Zitadel app must exist (`POST /tenants/{id}/apps` type `spa` via `register-spa.sh`). One `REDIRECT_ALLOWLIST` must include `http://localhost:5178/callback`. |
| Callback / Home / OrgLayout | `GET /v1/whoami` | `GET /api/v1/me` with the same Bearer. May JIT-join. |
| Create workspace | Merchant `oneApi.createTenant` **directly to One** (`POST /tenants`), not through Pay | Caller becomes owner. One CORS must allow `:5178` (it does, Development CSV). |
| Org chrome | `GET /v1/whoami` with `X-Lazuar-Tenant-Id` | `/me` with hint → `active_tenant_id`. |
| GET lists (links, payments, receipts, gateway, products) | `MemberGate.RequireMemberAsync` | `POST /tenants/{orgId}/authz/check` `relation=member`. Tenant must not be suspended (One 403). `object.id` must be the path UUID. |
| PUT keys / POST products / POST payment-links / POST checkouts / PUT one-webhook | `RequireWriterAsync` = member check **plus** `/me` role `owner`\|`admin` and status `active` | Same `authz/check` + second `GET /me`. |
| Buyer `GET/POST /v1/pay/{token}` | **No One call** | None. Pause is Pay-local `ChargesPaused` from Plane A. |

### 2.2 Tenant status `active`; owner/admin for writers

Live One statuses that appear on `/me.tenants[].status` include at least `active`, `suspended`, and (from `TenantAccessService`) `provisioning` / `failed` / `deleted`. Pay writer treats anything other than `active` (when status is non-empty) as 403 `"Tenant is suspended."` — including provisioning/failed, which One would describe as `"Tenant is not active."` Fail closed. Fine for money. Copy is slightly wrong for provisioning.

Member GET on a suspended tenant: One `authz/check` 403 `"Tenant is suspended."` → Pay 403 same sentence. Overview still **renders** if `/me` lists the tenant (`OrgLayout` only requires `tenants.find`). Banner: `"This workspace is suspended. Charges are paused."` Money GETs then 403 with the suspend sentence. That is the 002-065 close.

Owner and admin may write. Member may not. Proven: `Member_cannot_put_gateway`, `Member_cannot_create_product`, `Member_cannot_create_checkout`, `Member_cannot_create_payment_link`, `Member_cannot_put_one_webhook_secret`, `Admin_can_create_payment_link`. Member **can** `GET` gateway metadata (`Member_can_get_gateway_metadata`). Payment/receipts member-read is the `RequireMemberAsync` code path; `PaymentQueryTests` still uses `PayTest.Owner` (owner is a member via FGA). 019 G12 (dedicated member-token GET payments/receipts) is **still missing**. Not a money leak.

### 2.3 If One is down: 503

| Surface | One down | Buyer path |
|---------|----------|------------|
| `GET /v1/whoami` | 503 `"Identity provider unreachable"` (timeout/transport) or `"Identity provider failed"` (5xx) | n/a |
| Any MemberGate route | 503 same | n/a |
| Writer mint | 503 (member hop first) | n/a |
| `GET /health` | 200 `{status:ok}` — **does not call One** | n/a |
| `GET /v1/pay/{token}` | 200/404 from Pay DB | works |
| `POST /v1/pay/{token}/start` | PSP hop; no One | works **unless** `ChargesPaused` |
| Plane B PSP webhook | no One | fulfill continues |

Production implication for **first-party dogfood** (One + Pay merchant + Pay checkout): staff shell is dark if One is down. Buyers already in flight still pay. New staff mint does not. That is fail-closed for money creation and fail-open for captured charges — correct.

Production implication for **second apps**: if the second app is a **server** calling Pay `/v1` with a Bearer, **every mint and every merchant GET is coupled to One being up**, because Pay re-introspects on every request. There is no Pay session, no cached membership, no “this `lzr_sk_` was valid five minutes ago.” IsolationTests forbids a Pay membership cache table (good — 012/07: do not cache `/me` for authorization). The cost is: **Pay availability for staff/M2M is min(Pay, One, OpenFGA behind One, Zitadel if the Bearer is a JWT that One re-validates).** A second app that expected Stripe-shaped “secret key is valid until revoked, Pay is the only hop” does not get that. One is on the hot path. Document that as Consumer-0 physics, not as a bug to “fix” by storing users in Pay.

If One is down, Pay must not invent a local allow. 503 is the product.

### 2.4 What a second app must provision on One before Pay will mint

1. A One **tenant** (workspace). UUID is Pay `org_id`.
2. At least one **owner or admin** membership (human JWT) **or** a machine door that does not exist yet (`lzr_sk_` + MemberGate branch — 02).
3. One CORS if the second app is a **browser** calling Pay from a new origin — **and** One CORS if that browser also calls One (`POST /tenants`, register SPA). Server-side second apps skip CORS.
4. Pay `Pay:CorsOrigins` CSV must include that browser origin (Production empty fails boot).
5. Plane A: register Pay's `/v1/one/webhooks` on that tenant in One, PUT the shown-once `whsec_` into Pay (`PUT /v1/orgs/{orgId}/one-webhook`) so suspend actually pauses leftover public links.
6. Processor vault (writer) or Test in Dev/Testing.
7. **Not** a Pay user. **Not** a Pay org table. **Not** a Zitadel PAT in Pay.

That list is why “Pay as a kernel other products can swallow in an afternoon” is not true on this SHA even after 002. The afternoon is: create a One workspace, get a human JWT or wait for 02, set CORS CSV, paste `whsec_`, paste PSP keys. There is no sample app in this repo that does that without cloning merchant Vite.

---

## 3. Can Pay be used by an app that already has its own users?

### 3.1 Two planes, restated for 020

012/06 and 011/01 already split the world. Live files still match:

| Plane | Identity | Live |
|-------|----------|------|
| Merchant staff | One / Zitadel humans. `GET /me.user_id` is Zitadel `sub`. Roles `owner`/`admin`/`member`. | Merchant SPA OIDC. Pay MemberGate. |
| Merchant machine | One `lzr_sk_` bound to **one** tenant. `/me` `user_id` is the key GUID. | Host **forwards** Bearer. MemberGate **omits** `user_id` → live One 400. No productization. No test. |
| Buyer / payer | Email/name on the checkout. `payers` table in Pay. **Not** a One membership. **Not** a Zitadel human. | `:5179` has no OIDC. PublicPay has no Bearer. |

Buyers **already** are not One. That plane is the kernel-shaped half. A second app that only needs “send this human to a hosted pay link” can mint as **staff** (today) and hand the buyer a URL. The buyer never sees One. That is the hosted-cashier product 002 finished.

A second app that **already has its own users** (not One humans) wants one of three things:

**A. Hosted cashier, their staff are not One.** They have a Hub, a Cognito pool, a custom session cookie. They want to call `POST /v1/payment-links` as their backend. Today the backend must present a Bearer One accepts. Their users are invisible to Pay. **Their operators** still need a One workspace and a token. That is Consumer-0 physics: Pay does not know what a “user” is except what One `/me` returns.

**B. Embed Pay checkout in their logged-in app.** Their logged-in human is a **buyer**, not staff. They should use the public token URL (or a future headless `/v1` start) **without** One. CORS must list their origin. They must **not** send their app session to Pay as Bearer — Pay would forward it to One, One 401, Pay 401, and they would have taught Pay a foreign cookie. Checkout today is origin `:5179` only unless `Pay:CorsOrigins` includes them.

**C. Their logged-in humans are merchants of Pay (multi-tenant SaaS using Pay as a processor).** Each of **their** customer-companies must map to a One tenant id stored on every Pay row. They must either (c1) create One workspaces per customer (One is their IdP for money), or (c2) hold one One workspace and put all charges under one `org_id` (no isolation — refuse), or (c3) demand Pay grow a local user table (refuse). **(c1) is the product.** Document it. Do not build (c3).

### 3.2 Kernel door vs refuse

**Refuse (this slice, standing law, IsolationTests):**

- Pay-local `users` / `members` / `organizations` tables.
- Pay-minted `sk_*` (collision with Stripe; 012/08).
- Cookie session on Pay (`lazuar_auth`, `AllowCredentials`).
- Treating the second app's IdP JWT as Pay AuthN without One.
- Copying `Modules/One`.
- `is_platform_admin` as Pay superuser.
- OIDC on `:5179`.
- Creating a Zitadel human per cardholder.

**Kernel door (missing, not refuse):**

- MemberGate accepting **machine keys** One already mints (`lzr_sk_`). Design belongs to [02-machine-keys-m2m.md](./02-machine-keys-m2m.md). This slice's job is to say: the host already forwards Bearer; live `authz/check` without `user_id` 400s keys; `/me` for keys works and would make writer overlay treat tenant-admin-equivalent scopes as `admin`; there is no test; do not invent a Pay key vault.
- CORS CSV that ops can set to the second app's origin without a code edit. **Landed** as `Pay:CorsOrigins` (see §4). Ops still has to set it. Compose default is still laptop.
- Docs that say **Consumer-0**: first-party dogfood is One humans + this merchant SPA. A stranger integrates as a **client of `/v1`** with a One workspace, not as a clone of `:5178`.
- Outbound `payment.completed` (03, not this slice) so their app does not poll.

**The honest sentence:** Pay is Consumer-0 of One for **staff and tenancy**. Pay is not Consumer-0 of One for **buyers**. A second app with its own users can use Pay for those users **as buyers** without One accounts. It cannot use Pay for those users **as merchants** without a One workspace per money tenant. That is product law, not a gap to close with a Pay user table.

### 3.3 What “Pay is Consumer-0 of One only” would mean if we refused the kernel door

If the program concludes Pay is **only** a hosted cashier for One humans:

- Merchant SPA stays the only first-class client.
- `lzr_sk_` on Pay `/v1` stays accidental (forwarding exists; MemberGate 400s live).
- Second apps are told: log into `:5178` or clone it.
- CORS CSV is for deployed `:5178`/`:5179` HTTPS, not for Hub.

020's parent question is the opposite: “what is missing so **another app** can integrate without cloning this repo.” Refusing the kernel door would answer that with “clone the repo.” This paper does **not** recommend that refuse. It recommends documenting Consumer-0 **and** opening the machine-key door (02) **and** CORS CSV (landed, ops) **and** never a Pay user table.

---

## 4. CORS: `Pay:CorsOrigins`

019 B10: `Program.cs` hardcoded eight laptop origins. No `Pay:CorsOrigins`. Production merchant HTTPS origin got no `Access-Control-Allow-Origin`. One's rule (empty CORS fails boot) was the opposite of Pay's silent list.

002-049 / 002-066 / 002-080 closed the **code** shape. Live `PayCors`:

```5:59:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs
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
    // Add → WithOrigins(Resolve(...)).AllowAnyHeader().AllowAnyMethod()
    // Resolve: parse CSV; else Development/Testing → DevelopmentOrigins;
    // else throw InvalidOperationException("Pay:CorsOrigins must be configured in Production and Staging.")
}
```

Quote the throw: **`Pay:CorsOrigins must be configured in Production and Staging.`** Empty in Production **fails boot**. Same honesty as One's `App:CorsOrigins must be configured in Production and Staging.` Pay copied the **rule**, not One's code. `appsettings.json` (base) does **not** set `Pay:CorsOrigins`. `appsettings.Development.json` sets the eight laptop URLs. Production/Staging with no env and no appsettings → throw at `PayCors.Add` during `WebApplication.CreateBuilder` composition — process does not serve.

`AllowAnyHeader` + `AllowAnyMethod`. **No** `AllowCredentials` (grep empty). Right shape for a Bearer SPA: `Authorization` is a non-safelisted header, so browsers preflight; the policy must allow the header; cookies must not ride along.

`CorsTests` on this SHA (all on `PayApiFactory` Testing — 019 B9's “bare `WebApplicationFactory` migrates Postgres” is closed):

| Test | Proves |
|------|--------|
| `Health_allows_merchant_origin` | `Origin: http://localhost:5178` on `GET /health` → ACAO 5178 |
| `Health_allows_checkout_origin` | 5179 |
| `Health_allows_preview_checkout_origin` | 4179 |
| `Health_does_not_allow_ops_origin` | 3003 no ACAO |
| `Health_does_not_allow_portal_origin` | 3004 no ACAO |
| `Health_allows_configured_extra_origin` | factory `CorsOrigins = "https://checkout.example"` allows that origin |
| `Configured_origins_replace_laptop_list` | same factory: 5179 **denied** once CSV is set |
| `Public_pay_get_allows_checkout_origin` | `GET /v1/pay/missing` Origin 5179 ACAO (404 body, CORS still set) |
| `Public_pay_post_allows_checkout_origin` | `POST /v1/pay/missing/start` |
| `Public_pay_options_allows_checkout_origin` | OPTIONS 5179 ACAO, status < 300 |
| `Public_pay_options_denies_ops_origin` | OPTIONS 3003 no ACAO |
| `Empty_cors_in_production_fails_boot` | `PayCors.Resolve(null, Production)` and `"  "` Staging throw containing `Pay:CorsOrigins` |
| `Empty_cors_in_development_uses_laptop_list` | null Development and `""` Testing → `DevelopmentOrigins` |

**Second app origin is not on the list unless ops sets CSV.** `Configured_origins_replace_laptop_list` is the trap: if production sets only `https://merchant.example`, **checkout** `https://checkout.example` and any second-app origin are denied. Ops must list **every** browser origin that calls Pay: merchant, checkout, preview if used, **and** the second app. There is no wildcard. There is no “allow any origin of this tenant.” Pay does not know tenant origins — that would be a Pay org table.

Compose (`docker-compose.pay.yml`) still defaults:

```
Pay__CorsOrigins: ${Pay__CorsOrigins:-http://localhost:5178,http://127.0.0.1:5178,http://localhost:5179,http://127.0.0.1:5179}
```

That default **omits** 4178/4179 (preview). It is a laptop CSV so Development-in-compose does not fail boot. Production compose **must** override. README says so. `.env.example` says so:

```
# Comma-separated browser origins allowed to call Pay. Development defaults to
# laptop merchant/checkout/preview ports. Production/Staging empty fails boot.
# Never AllowAnyOrigin. Never add ops :3003 or portal :3004.
# Docker/production: set the public merchant and checkout HTTPS origins.
# Pay__CorsOrigins=https://checkout.example,https://merchant.example
```

One CORS is a **second** allowlist. Merchant `POST /tenants` is browser → One `:8080`, not Pay. One Development CSV includes `:5178` (and 5173/5174/5177/5180/5181). It does **not** include `:5179` (correct — checkout must not call One). A second-app origin that creates workspaces from the browser needs **One** `App:CorsOrigins` too. Server-side create (`POST /tenants` with a JWT from their backend) skips One CORS.

Hub `:3003`/`:3004` stay denied. Do not add them “temporarily.”

---

## 5. OIDC: merchant SPA vs checkout vs M2M

### 5.1 Merchant SPA (`:5178`)

`getOidcConfig`: authorization code + PKCE, `response_type: 'code'`, `automaticSilentRenew: true`, `userStore: sessionStorage`. Authority default `:8085`. Redirect default `http://localhost:5178/callback`. Silent renew default `{origin}/silent-renew.html` (002-044 closed iframe-to-callback). Post-logout default `http://localhost:5178/`. Scope default `openid profile email offline_access`. **No** `extraQueryParams` / `urn:zitadel:iam:org:project:id:…:aud`. Login UI is One `:5175` via the issuer, not a Pay password form.

`package.json` scripts: `vite --port=5178 --host=0.0.0.0 --strictPort`. Preview **4178**. Vite config dual-pins those ports. `strictPort` so a busy 5178 does not steal login `:5175` or checkout `:5179`.

`pickApiBearerToken`: JWT-like `access_token` only (three non-empty `.` parts). Opaque, JWE, empty, signed-out → `undefined`. **Never** returns `id_token`. Tests lock that.

`RequireAuth` (002-035 closed the livelock): `isAuthenticated` **and** `pickApiBearerToken`. Missing JWT access shows an error (“This session has no JWT access token…”) with a Sign in button, not an infinite `signinRedirect` with no copy. `HomePage` / `OrgLayout` still `signinRedirect` when the picker is empty (OrgLayout sets `returnTo` first). Opaque token is now a visible failure, not a spin.

`payApi.ts`: credentials omitted. `fetch` never sets `credentials: "include"`. Cookie vs Bearer SPA: **Bearer + sessionStorage**. Correct.

`locks.test.ts` bans `type="password"`, `/one/auth/login`, `lazuar_auth`, Hub `@repo/api-types-ts`. IsolationTests also bans Hub types on both Vite `package.json`s.

`register-spa.sh` POSTs One `/tenants/$TENANT_ID/apps` with `{ name, type: "spa", redirect_uris, post_logout_redirect_uris }`. Fails if 201 includes `client_secret` (confidential leak). Optional `WRITE_ENV=1` writes only `VITE_ZITADEL_CLIENT_ID` to gitignored `.env`. Default redirect is `http://localhost:5178/callback` only. It does **not** register the `127.0.0.1:5178/callback` twin unless `REDIRECT_URI` is overridden. README tells humans to add the twin to One `REDIRECT_ALLOWLIST`. One `Zitadel:UseStub=true` (Development default on One) returns a stub `client_id` that cannot complete login — documented on One, mentioned in 019, still true.

Create workspace: `oneApi.createTenant` POSTs One `/tenants` with Ada Bearer. Caller becomes owner. IsolationTests forbids Pay org tables. **Create via One API from merchant exists.**

Invite: merchant `src` grep `invite` is **empty**. One `POST /tenants/{tenantId}/members/invite` exists. Pay merchant has no Team page. Second engineer today: use `lazuar-app`. 019 G3 / checklist O10 rot. Out of 002 (gaps that are not live lies). Remaining for 020 as chrome, not as a Pay members table.

### 5.2 Checkout (`:5179`) — no login

`package.json` has no `oidc-client-ts`, no `react-oidc-context`, no `@lazuar/one-client`. `main.tsx` is `StrictMode` + `App` — no `AuthProvider`. `App.tsx` talks only to `/v1/pay/{token}` and `/start`. `locks.test.ts` `has no OIDC dependency`. PublicPay handlers take no `OneClient` and call no `MemberGate`. Copy on the paid screen (019): “This page is not a membership login.” Buyers have no One account. **Confirmed.**

Vite **5179** `strictPort`, preview **4179**. Production build requires `VITE_PAY_API_URL`. No OIDC env.

A second app that embeds checkout in an iframe or copies the SPA still must not add OIDC to `:5179`. If they want a logged-in **buyer** portal, that is their app, not Pay. Magic-link receipts stay Pay later (NP-BUY), not One.

### 5.3 M2M has no SPA

One OIDC app type `m2m` is Zitadel `client_credentials` for **the integrator's own APIs**, not a One `lzr_sk_`, not Pay's worker credential. `register-spa.sh` hardcodes `type:"spa"`. Grep of Pay apps for `m2m`: empty. Pay must not register an m2m app to “skip login.” Machine access to Pay `/v1` is Family A `lzr_sk_` minted in One (02), presented as Bearer, introspected by One. There is no Pay SPA for machines. There is no Pay client_credentials dance.

Redirect ports that exist:

| App | Dev | Preview | Callback |
|-----|-----|---------|----------|
| Merchant | 5178 | 4178 | `/callback`, `/silent-renew.html` |
| Checkout | 5179 | 4179 | **none** |
| One login | 5175 | — | One's, not Pay |
| One app (lazuar-app) | 5174 | — | not Pay |
| Hub ops / portal | 3003 / 3004 | — | **deny** on Pay CORS |

A second-app SPA that is not merchant would need its **own** One `type=spa` app (redirect to **their** origin) if their staff are One humans, **or** no SPA at all if their backend holds `lzr_sk_` (once 02 lands). Do not reuse `lazuar-pay-merchant`'s `client_id` on another origin — Zitadel will reject the redirect URI.

---

## 6. Tenant = `org_id` = One tenant id on every row

### 6.1 Product law (012/06, live schema)

012/06 binding answers that live files still obey:

1. One tenant UUID **is** Pay `org_id`. Same bytes. No Pay-side surrogate.
2. Pay does not have an `organizations` / `tenants` / `workspaces` table. IsolationTests: source must not contain `ToTable("organizations")`, `ToTable("users")`, `ToTable("members")`.
3. Money rows store that id as `OrgId`. It is a **copy of One's id**, not a foreign key into a Pay org row.
4. Authorization SoT is path `{orgId}` plus One membership. Header is a hint.
5. Create workspace in Pay UI is `POST /api/v1/tenants` on One.

`Rows.cs` `OrgId` appears on: `OrgSettingsRow`, `CheckoutRow`, `PaymentLinkRow`, `IdempotencyKeyRow`, `ProductRow`, `GatewayCredentialRow`, `PspWebhookEventRow`, `ChargeRow`, `SubscriptionRow`, `JournalEntryRow`, `DocumentRow`, `DocumentSequenceRow`, `PayerRow`, `AuditEventRow`, `MailOutboxRow`. `PriceRow` is under product. `JournalLineRow` is under entry. `OneWebhookEventRow` is delivery-keyed, not org-keyed (org is inside the event body / header). There is no row without a tenant except those two children and the HMAC inbox.

`payers` is a **buyer** profile table (`ToTable("payers")`). IsolationTests does not ban it. Buyers are Pay. That is not a staff user table. Do not “helpfully” join `PayerRow` to One `user_id`.

### 6.2 Second app must have a One workspace

This is **product law**, not a gap. A second app cannot invent a Pay `org_id`. If they send `org_id: "acme"` and One expects a UUID, live One `authz/check` 400s (`object.id must equal the path tenantId` / not a UUID). Pay now returns 400. If they skip One and we someday accepted a homemade org id, every money row would be un-authorizable (MemberGate would 503/400 forever) and Plane A `tenant_id` would never match.

**Document:** “To take money on Pay you have a Lazuar One workspace. Pay `org_id` is that workspace's id. Your app's own user table is not Pay's tenant. Map your company → One tenant (create via One `POST /tenants` as an owner JWT, or an existing membership). Then call Pay `/v1` with a Bearer One accepts.”

**Do not** add `pay.org_map`. 012/06 §7 last-resort mapping table is for “One's UUID is not ours” after a production reason exists. It does not exist. Hub org ids are museum.

### 6.3 Isolation of money

Queries filter `Where(x => x.OrgId == orgId)` after MemberGate. Path org is SoT (`Create_for_other_org_is_403` / `List_other_org_is_403` on checkouts). Fake One `Allow("t1")` returns `allowed: false` for other paths. That is hermetic tenancy, not live FGA. Live FGA is One's problem; Pay's job is to ask and fail closed.

A second app that holds tenant A's `lzr_sk_` (once 02 works) must not read tenant B. One binds the key to one tenant. `/me` for the key has 0–1 tenants. Writer overlay would 403 if path ≠ bound id (`tenant is null` → `"Not a member of this org"`). MemberGate `authz/check` today 400s before that. Either door, path must equal the One tenant.

---

## 7. IsolationTests. Cathedral bans

```1:17:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
public class IsolationTests
{
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

Tests:

| Test | Ban |
|------|-----|
| `Host_csproj_does_not_reference_the_old_api` | csproj tokens in `Banned` |
| `Test_csproj_does_not_reference_the_old_api` | same |
| `Source_does_not_use_mediatr_or_hub_modules` | every `src/**/*.cs` vs `BannedSrc` |
| `Source_does_not_create_org_or_user_tables` | `ToTable("organizations"|"users"|"members")` |
| `Vite_apps_do_not_use_hub_types` | merchant + checkout `package.json` no `@repo/api-types-ts` |
| `No_csproj_references_apps_lazuar_api` | no `apps/lazuar-api`, no `Razorpay.Api` |

020 standing law: IsolationTests stay red on cathedral strings (`MediatR`, `IEnumerable<IHostedRail>`, Hub `@repo/api-types-ts`). They do. `namespace Lazuar.Pay.Identity` is allowed; `namespace Lazuar.Pay.One;` is not. The HTTP façade lives under `Identity/` on purpose.

What IsolationTests does **not** ban (and must not be used as a loophole):

- A future `ToTable("api_keys")` in Pay — 012/08 refuse; IsolationTests would not catch it unless someone adds the string. This paper's refuse list covers it.
- `lzr_sk_` forwarding — not a cathedral.
- `payers` — buyers.

Do not weaken these tests to “let a second app in.” A second app is a **client**, not a project reference.

---

## 8. 019-evals/07 vs this SHA

019/07 ran on `9f04ad58` `feat/018-merchant-shell`. 002 claimed 001–080 resolved on `fix/002-pay-host-bugs`. This SHA is `6d730d15` on that branch. Live files vs 019/07:

### 8.1 Closed as hosted-cashier bugs (do not re-open as 019)

| 019 | 002 | Live on `6d730d15` |
|-----|-----|-------------------|
| B1 HMAC dialect | 011 | `OneWebhookSignature` documents One `v1=` + `X-Lazuar-Timestamp`; combined `t=,v1=` compat. Out of this slice except Plane A pause. |
| B2 one process `whsec_` | 029 | Per-org `PUT /v1/orgs/{orgId}/one-webhook` + process fallback. Writer gated. |
| B3 stale `returnTo` / `setOrgHint` | 047 | `resolvePostLoginPath` drops returnTo when org not in tenants. `OrgLayout` `setOrgHint` only if `match`. |
| B4 checkout GET existence oracle | 062 | Bearer 401 first. Unknown with Bearer still 404 without One (remaining P3). Non-member → 404 unless suspend. |
| B5 opaque token loop | 035 | `RequireAuth` requires `pickApiBearerToken`; error copy + Sign in. |
| B6 invalid JSON after HMAC | 063 | try/catch → 400 `"invalid event"`. Empty body 400. |
| B7 One 400/429 → 503 | 064 | **MemberGate** pass-through. Tests on `/ready`. Whoami **not** mapped. |
| B8 suspend copy | 065 | Pass-through + writer status + chrome banner. |
| B9 CORS tests only `/health` | 066 | GET/POST/OPTIONS `/v1/pay/*`; `PayApiFactory`. |
| B10 CORS hardcoded laptop | 049, 080 | `Pay:CorsOrigins`. Production empty throws. Compose still laptop default. |
| G1 writer `/me` overlay | 030 | Status check + admin test + payment-link member 403. **Still `/me` overlay.** SoT remaining. |
| G2 dummy ready | 078 | `ready` = !paused && (vault \|\| Test allowed). Spec comment updated. SPA still unused. |
| G9 spec omits `name` | 074 | `WhoamiResponse.name` in TypeSpec. |
| G13 payment-link member test | 030 | `Member_cannot_create_payment_link`. |
| G14 admin writer untested | 030 | `Admin_can_create_payment_link`. |

### 8.2 Remaining from 019 that 002 did not make a kernel, or never listed

| 019 | Status on this SHA | 020 class |
|-----|-------------------|-----------|
| G3 invite from merchant | still empty grep | Missing chrome. Do not add Pay invites table. Call One or deep-link `lazuar-app`. |
| G4 `lzr_sk_` kernel | still no string in host tests; MemberGate omits `user_id` | **Kernel door. Handoff to 02.** |
| G5 last workspace per-tab hint | `dashboardPath` + `resolvePostLoginPath`; Home still ignores `active_org_id` unless hint forwarded | P3 chrome. Not a second-app blocker. |
| G6 register-spa twins / audience | script still localhost:5178 only; no Zitadel audience pin | P2 dogfood of 127.0.0.1 and staging audience. |
| G7 Plane A catalog / registration / SSRF / `tenant.deleted` | Pay still does not POST One `/tenants/{id}/webhooks`. `tenant.deleted` pause is a 04 question. | Ops hatch. |
| G8 SPA chrome is `/me` not `authz/check` | still | Acceptable. Money routes re-check. |
| G10 `IsPlatformAdmin` forwarded unused | still | Refuse: do not grow a Pay backdoor. |
| G11 `authz/batch-check` never called | still | Fine for v1. |
| G12 member GET payments/receipts dedicated test | still only gateway member-get | Missing test, not missing gate. |
| G15 whoami 403 mapper / `lzr_sk_` | still missing both tests | Kernel + honesty. |
| G16 checklist rot | 002 closed the **bugs**; O10 invite tick vs live still rot if 013 checklists were not un-ticked | Out of this paper to edit checklists. |

### 8.3 What 002 closing does **not** mean

002 made the **hosted cashier** honest: One can pause, CORS is configurable, suspend copy is not “not a member,” writer tests cover admin/member/payment-links, ready means something. It did **not** make Pay a kernel. 020 README said that. This SHA confirms it:

- No machine-key productization.
- No second-app origin until ops CSV.
- Staff are One. Buyers are not. Second-app **merchants** must be One tenants.
- Writer SoT is still `/me` role overlay.
- Whoami 400/429 still 503.
- Compose CORS default is still laptop.

---

## 9. How to solve (do not implement from this paper)

When a later slice does:

### S1. Document Consumer-0 (product law, not a code change)

Write in Pay README (and 09-spec-docs-sample, not here as a PR):

- First-party dogfood is One humans + `:5178` + `:5179`. That is Consumer-0.
- Pay talks to One over HTTP. Staff membership, tenant id, machine keys, and tenant.suspended **are** One. Pay does not copy them.
- Buyers are not One. Hosted pay and Plane B do not call One.
- A second app integrates as a **client of `/v1`**. It needs a One workspace. `org_id` is that id. It does not clone this repo's Vite apps unless it wants this chrome.
- Pay availability for mint/list is min(Pay, One). 503 if One is down. Health is not a One probe.

Do not write “Pay is standalone.” Do not write “bring your own IdP.” Do not write “we will add a users table.”

### S2. CORS CSV for second-app origins (mostly landed)

Ops: `Pay__CorsOrigins` = merchant HTTPS + checkout HTTPS + **each** second-app browser origin. CSV replaces the laptop list (`Configured_origins_replace_laptop_list`). Empty Production/Staging already throws — keep that.

Compose default may stay laptop for Development. Production compose override is mandatory; fail the deploy if the value still contains `localhost:5178` in real prod (host-production slice, not this one).

Never `AllowAnyOrigin`. Never `AllowCredentials`. Never add `:3003`/`:3004`/`:5173`/`:3005`.

If the second app is server-side only, CORS is irrelevant; they call `:8081` from a backend.

One `App:CorsOrigins` is a separate CSV if their **browser** calls One.

### S3. MemberGate accepting machine keys (handoff to 02)

This slice's constraint for 02:

- Do **not** mint keys in Pay. One already mints `lzr_sk_`.
- Do **not** store N merchant `lzr_sk_` in Pay as a vault.
- Do **not** send `user_id` = key id on `authz/check` (One 400s that).
- Do **not** skip MemberGate for JWTs.
- **Do** branch: if Bearer is a JWT, keep today's omit-`user_id` `authz/check` `member` (and, if you fix G1, `admin` for writer). If Bearer is `lzr_sk_`, introspect `/me` (One already returns bound tenant + synthetic role) and require path `orgId` == that bound id; treat tenant-admin-equivalent as writer; treat other scopes as member. Fail closed if `/me` has zero tenants or status not active.
- **Do** add `Whoami_forwards_machine_key_shape`: `Authorization: Bearer lzr_sk_test`, Fake One 200 `/me` with key-shaped body, Pay 200.
- **Do** add a live-shaped test that Fake One 400 `"user_id is required when authenticating with an API key."` today becomes Pay 400 — until the branch exists, that test documents the hole; after the branch, the test becomes “key whoami + mint without authz user_id.”

02 owns scopes, prefix, revoke. This paper only forbids solving it with a Pay user table.

### S4. Never a Pay-local user table

IsolationTests already bans `users` / `members` / `organizations`. Keep them. A PR that adds `ToTable("accounts")` for staff is the same sin with a different noun. Buyers stay `payers` (email/name). Staff stay One. Machines stay One keys.

Invite chrome: `fetch` One `POST /tenants/{orgId}/members/invite` with the staff JWT, or deep-link `lazuar-app`. Accept stays on One.

### S5. Writer = `authz/check` `admin` (optional vs 02)

Smaller than 02. `CheckWriterAsync` posts `relation: "admin"`. FGA `admin` includes `owner`. Drop the second `/me` from the gate. Keep `/me` for whoami and sidebar. Tests: Fake One `allowed: true` on admin check for owner and admin; `allowed: false` on member even if `/me` lies. Until then, the overlay is fail-closed and double-RTT. Not a second-app blocker once 02 exists (`/me` for keys already has a role). Prefer one SoT before teaching strangers two hops.

### S6. Whoami 400/429 map like MemberGate

Pass through One 400/429 on `/v1/whoami` the same way. Add `Whoami_maps_one_403`. Cheap honesty. Not a kernel door.

### S7. Ready as a kernel probe

Leave `GET /v1/orgs/{orgId}/ready` as member + !paused + (vault or Test). Document in 09 that second apps may use it as “can this org take money.” Merchant does not have to call it. Do not grow five-rail health into it.

### S8. Register SPA / audience (dogfood)

Keep One `POST /tenants/{id}/apps` `type=spa`. Optionally append 127.0.0.1 twins. Document `Zitadel:UseStub=false` and audience scope next to the script. Do not put PAT in the script. M2M type is not this script.

### S9. Tests to add before claiming a second app can mint

1. `Authorization: Bearer lzr_sk_test` whoami → Fake One 200 (G4 / O13).
2. Same Bearer on `POST /v1/payment-links` against Fake One that 400s authz without `user_id` — today's live shape — Pay 400 until 02 branches; after 02, 201 when `/me` bound tenant matches.
3. Member token `GET /v1/orgs/t1/payments` and receipts 200 (G12).
4. Whoami One 403 mapper.
5. Whoami One 400/429 if S6 lands.
6. CORS extra origin on `/v1/whoami` OPTIONS (today extra origin is proven on `/health` only plus public pay on 5179). Nice; not blocking if default policy applies.

### S10. What not to “solve”

- Pay OIDC middleware.
- Pay OpenFGA store with types `payment` / `document`.
- Caching membership in Redis “because One is slow.”
- A platform god-key in Pay `.env` that speaks for all shops.
- Dual-write Hub `GlobalUser` and One.
- Checkout login “so we know the buyer.”

---

## 10. Ranked holes this slice

1. **P0 kernel — MemberGate cannot accept live `lzr_sk_`.** Host forwards any Bearer. One `/me` for keys works. One `authz/check` without `user_id` 400s API keys; Pay now faithfully returns 400. A second app that is not a One human SPA **cannot mint**. Hermetic tests never send `lzr_sk_`. This is the door 020 exists to name. Handoff to 02. Do not cathedral a Pay key table. **Missing feature**, not a 002 regression.

2. **P0 product law — second-app merchants need a One workspace; `org_id` is that UUID.** Not a bug. A hole in **docs/sample** (09) if we keep implying Pay is a drop-in Stripe. Refuse the alternative (Pay user table). Document Consumer-0.

3. **P1 — Writer SoT is still `/me` overlay, not `authz/check admin`.** 002 closed the missing tests and the missing status check. Split-brain with FGA remains. Extra One RTT. Fail closed. **Missing feature / residual of 030.**

4. **P1 — CORS CSV is real; second-app origin is still an ops act; configured list **replaces** laptop origins.** Production empty fails boot (correct). Forgetting checkout when adding Hub is a silent browser fail. Compose default is laptop. **Missing ops + 09 docs**, not missing code. Do not hardcode a Hub origin.

5. **P1 — One on the hot path for every staff/M2M request.** 503 if One is down. Correct fail-closed. Production implication: a second app's mint SLA includes One+FGA. Document. Do not cache `/me` for authz.

6. **P2 — Whoami 400/429 still 503; whoami 403 untested; no `lzr_sk_` whoami test.** Honesty leftovers of 064/G15.

7. **P2 — Invite chrome missing.** Create workspace is real. Second engineer uses `lazuar-app`. Do not add Pay members.

8. **P2 — `register-spa.sh` twins and Zitadel audience pin.** Staging One that requires audience 401s a syntactically JWT access token.

9. **P2 — Member GET payments/receipts has no dedicated member-token test.** Gate exists.

10. **P3 — `GET /v1/checkouts/{id}` with Bearer still 404s unknown without calling One** (existence oracle for any valid token). Suspend vs 404 split is correct.

11. **P3 — `/me` can write (JIT join).** Pay whoami is a proxy. Do not poll it from a second app hot loop.

12. **P3 — `is_platform_admin` forwarded, unused.** Refuse Pay superuser.

13. **P3 — Writer copy `"Tenant is suspended."` for any non-active status** including provisioning. Fail closed; sentence slightly wrong.

14. **Confirmed good (do not “fix”):** no PAT; no checkout OIDC; picker never `id_token`; sessionStorage not cookies; no `AllowCredentials`; no VIEWER; POST mint writer / GET lists member; staffDisplay not numeric sub; `register-spa.sh` speaks One apps; IsolationTests cathedral bans; path org SoT; health never calls One; buyers never hit MemberGate; Production CORS empty throws; MemberGate 400/429/suspend pass-through; ready is not dummy; per-org Plane A secret writer-gated.

---

## 11. Bugs vs missing vs refuse (this slice only)

### Bugs (live lie or fail-open)

None at P0 for **hosted cashier** identity after 002. Remaining identity bugs are honesty leftovers: whoami 400/429 mapping, missing tests, writer SoT drift, checkout-id oracle for Bearer holders. Fail-closed 503 when One is down is **not** a bug.

### Missing features (kernel)

- Machine-key door on MemberGate (02).
- Docs: Consumer-0, CORS CSV for strangers, `org_id` = One tenant.
- Sample second app (09).
- Invite chrome via One HTTP (optional).
- Writer `authz/check admin` (optional, cleaner SoT).

### Refuse

- Copy `apps/lazuar-api/Modules/One` or `namespace Lazuar.Pay.One`.
- Hold `ZITADEL_PAT`, login-client PAT, OpenFGA admin, or One `Webhooks:SigningSecretEncryptionKey`.
- Parse `urn:zitadel:iam:org:project:roles`.
- Invent Pay `VIEWER`. One tenant roles are `owner` / `admin` / `member`.
- OIDC / whoami / `RequireAuth` on `:5179`.
- Cookie session on Pay (`lazuar_auth`, `AllowCredentials`, port-unscoped localhost cookies).
- Add `:3003`, `:3004`, `:5173`, `:3005` to Pay CORS “temporarily.”
- Pay `organizations` / `users` / `members` / homemade `sk_*` tables.
- `POST /platform/tenants` from merchant.
- Treat `is_platform_admin` as Pay superuser.
- Heal opaque access tokens by sending `id_token`.
- A Pay-side `lzr_sk_` mint. Hatch + forward.
- Bring-your-own-IdP for **merchants** without a One workspace.
- Cache `/me` as authorization.
- Tail Zitadel events instead of One's catalog.

---

## 12. Production-ready bar for this slice

**First-party dogfood (One + Pay merchant + Pay checkout)** on identity:

- Staff PKCE → Bearer → whoami → member/writer gates: **yes**, if One is up, tenant UUID is real, CORS lists 5178/5179 or the deployed HTTPS pair, SPA is `type=spa` with JWT access tokens, and One CORS lists the merchant origin for `POST /tenants`.
- Buyers anonymous: **yes**.
- Suspend staff copy: **yes**. Buyer pause: Plane A (04) + `ChargesPaused`.
- Production CORS empty fails boot: **yes**.
- No PAT: **yes**.

**Second app without cloning this repo:**

- Server with One owner JWT: can call `/v1` **today** if CORS is N/A (no browser) and `org_id` is a One UUID. That is still Consumer-0 (the JWT is a One human).
- Server with only `lzr_sk_`: **no** on live One (MemberGate 400).
- Browser on a new origin: **no** until ops sets `Pay:CorsOrigins` (and One CORS if they call One). Code supports it; default list does not include them.
- App with its own users as **buyers**: **yes** (public token). As **merchants**: **no** without One workspaces.

This slice's production bar for 020 is therefore: **hosted cashier identity is production-shaped; kernel identity is not.** Closing 02 + documenting Consumer-0 + CORS CSV ops is the path. A Pay user table is not.

---

## Appendix: quoted evidence

### A. `PayCors` — CSV, laptop default, Production throw

```33:46:apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayCors.cs
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
```

`.env.example` lines 21–25: comma-separated origins; Production/Staging empty fails boot; never AllowAnyOrigin; never ops :3003 or portal :3004.

`CorsTests.Empty_cors_in_production_fails_boot` asserts the throw message contains `Pay:CorsOrigins`. `Configured_origins_replace_laptop_list` asserts a CSV of `https://checkout.example` **denies** `http://localhost:5179`.

### B. Whoami forwards Bearer to One `/me`; no 400/429 pass-through

```13:42:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs
    static async Task<IResult> Handle(HttpRequest request, OneClient one, CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        return Map(result);
    }
    // Map: 200 value; timeout/transport 503 unreachable; 401 rejected; 403 forbade; else 503 failed
```

`WhoamiTests` covers 200 map (`active_org_id`), empty tenants, 401 skip One, One 401, timeout 503, One 500 → 503. No 403. No 400. No `lzr_sk_`.

### C. Member = authz `member`; 400/429/suspend pass-through; writer = `/me` overlay + active

```36:47:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
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
```

```80:95:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
        var tenant = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId);
        if (tenant is null)
        {
            return PayErrors.Status(403, "Forbidden", "Not a member of this org");
        }

        if (!string.Equals(tenant.Status, "active", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(tenant.Status))
        {
            return PayErrors.Status(403, "Forbidden", "Tenant is suspended.");
        }

        if (tenant.Role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }
```

`OneClient.CheckMemberAsync` body is always `Relation = "member"`. No `CheckWriterAsync` in the repo.

### D. Ready is not dummy

```31:38:apps/lazuar-pay/src/Lazuar.Pay/Identity/OrgReadyEndpoints.cs
        var settings = await db.OrgSettings.FindAsync([orgId], cancellationToken);
        var hasVault = await db.GatewayCredentials.AnyAsync(x => x.OrgId == orgId, cancellationToken);
        var ready = IsReady(settings?.ChargesPaused == true, hasVault, PayProviders.AllowsTest(env));
        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = ready }, OneClient.Json);
    }

    internal static bool IsReady(bool chargesPaused, bool hasVault, bool allowsTest) =>
        !chargesPaused && (hasVault || allowsTest);
```

### E. Timeouts

```25:30:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
        var baseUrl = (string.IsNullOrWhiteSpace(opt.BaseUrl)
            ? "http://localhost:8080/api/v1"
            : opt.BaseUrl).TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl);
        var timeout = opt.TimeoutSeconds <= 0 ? 5 : opt.TimeoutSeconds;
        _http.Timeout = TimeSpan.FromSeconds(timeout);
```

```122:128:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
        catch (TaskCanceledException)
        {
            return new OneCallResult<T> { TimedOut = true };
        }
        catch (HttpRequestException)
        {
            return new OneCallResult<T> { TransportFailed = true };
        }
```

### F. Live One: API key authz requires a human `user_id`; `/me` for keys is bound tenant

```50:52:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Platform/MeEndpoints.cs
        if (ApiKeyScopeHelper.IsApiKey(user))
        {
            return await GetMeForApiKey(user, sub, db, access, cancellationToken);
        }
```

`GetMeForApiKey`: `User_id = keyId`; one tenant if bound; `Role = admin` if tenant-admin-equivalent scopes else `member`; `Active_tenant_id` set.

```218:234:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "user_id is required when authenticating with an API key.");
        }
        // ...
                detail: "user_id must be a user subject, not the API key id.");
```

### G. Live One suspend on `authz/check`

```265:272:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Tenancy/TenantAccessService.cs
        if (row.Tenant.Status == TenantStatuses.Suspended
            && !isPlatformAdmin
            && mode != TenantAccessMode.AllowSuspended)
        {
            throw new TenantAccessException(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Tenant is suspended.");
        }
```

`TenantSuspendReactivateTests.Owner_suspend_blocks_member_mutate_allows_get_and_list`: after suspend, member `POST authz/check` is 403 containing `"suspended"`.

### H. FGA + MembershipRoles (no VIEWER on tenant)

```7:12:lazuar-one/deploy/dev/openfga/model.fga
type tenant
  relations
    define owner: [user]
    define admin: [user] or owner
    define member: [user] or admin
```

```5:8:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/MembershipRoles.cs
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
```

`AuthzObjectRules.ValidateObject`: tenant `object.id` must parse as UUID equal to path tenant. Hermetic Pay tests use `"t1"`; live One 400s that; Pay MemberGate now 400s.

### I. Merchant OIDC vs checkout anonymity

```24:33:apps/lazuar-pay-merchant/src/auth/oidcConfig.ts
    authority,
    client_id,
    redirect_uri,
    silent_redirect_uri,
    post_logout_redirect_uri,
    scope,
    response_type: 'code',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
```

```14:18:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

Checkout `main.tsx`: no `AuthProvider`. Checkout `locks.test.ts`: package.json must not contain `oidc-client-ts` or `react-oidc-context`.

`register-spa.sh`: `type:"spa"`; error if `client_secret` present; “Do not export ZITADEL_PAT here.”

### J. IsolationTests org/user tables and Hub types

```48:77:apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs
    public void Source_does_not_create_org_or_user_tables()
    {
        // ToTable("organizations"|"users"|"members") forbidden in src
    }

    public void Vite_apps_do_not_use_hub_types()
    {
        // package.json of merchant and checkout: no @repo/api-types-ts
    }
```

### K. Health never calls One

```7:10:apps/lazuar-pay/src/Lazuar.Pay/Hosting/HealthEndpoints.cs
    public static void MapHealth(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
```

### L. Chrome suspend banner; hint after membership

```1:8:apps/lazuar-pay-merchant/src/lib/workspaceStatus.ts
export function workspaceStatusBanner(tenant: WhoamiTenant): string | null {
  if (tenant.status === 'suspended') {
    return 'This workspace is suspended. Charges are paused.'
  }
  return null
}
```

```50:57:apps/lazuar-pay-merchant/src/layout/OrgLayout.tsx
        const match = body.tenants.find((t) => t.id === orgId) ?? null
        setTenant(match)
        if (match) {
          setOrgHint(orgId)
          setError(null)
        } else {
          setError('Not a member of this org')
        }
```

### M. 012/06 product law (historical paper, still matched by live schema)

> One tenant UUID is Pay `org_id`. Same bytes. Same string in JSON. No Pay-side surrogate.  
> Pay does not have an `organizations` / `tenants` / `workspaces` table.  
> Cardholders never become Zitadel users because they bought an ebook.

### N. Bearer is prefix-only (machines pass the parser)

```13:19:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return authorization.Length > prefix.Length && !string.IsNullOrWhiteSpace(authorization[prefix.Length..]);
```

---

## End

Coordinates: **2026-08-28** / **`6d730d15`** / `fix/002-pay-host-bugs`. Sibling One is HTTP on 8080, not a project reference. 019/07 is historical; this file is the 020 identity deliverable. Do not flip 011 checklist cells from this paper. Do not copy `Modules/One`.
