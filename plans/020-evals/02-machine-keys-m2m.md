# 02 — Secret keys / M2M (`lzr_sk_` vs Pay homemade keys vs staff JWT)

**Program:** 020-evals  
**Paper:** 02 — Machine keys so another app can call Pay without a human OIDC session  
**Date:** 28 August 2026  
**Type:** Uncondensed evaluation. **Not an implementation.** **Not** a flip of [011/11](../011-new-lazuar-pay/11-checklist.md) Status cells. **Not** a project reference into `apps/lazuar-api`. **Not** a copy of sibling `Modules/One` (that folder is Hub museum in this repo; live mint lives in sibling `lazuar-one`).

| | |
|--|--|
| Repo | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` |
| Branch | `fix/002-pay-host-bugs` |
| HEAD | `6d730d155c871465c35c192cf7730bfd270b47fa` |
| Subject | `fix(pay): store per-org One webhook secrets` |
| Sibling One | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` on `main` |
| One SHA this paper actually opened | `6b78e9d455618f2a0cfadf46089d5f993f081983` — `fix(api): allow Pay merchant :5178 as a CORS origin` (AuthorDate Tue, 25 Aug 2026 03:12:50 +0800) |
| Historical paper this slice re-reads | [012-one-to-pay/08-machine-keys.md](../012-one-to-pay/08-machine-keys.md) (written 20 August 2026 against One `0f79fe4f` — **not** the SHA opened here) |

**Standing law used as the ruler (not as evidence that the code matches):**

- Pay holds OIDC `client_id` + later **one-tenant** `lzr_sk_` + webhook HMAC. Never a Zitadel PAT / OpenFGA admin / masterkey.
- A key is bound to **one** One tenant. No god-key in Pay `.env` that speaks for every merchant.
- Pay talks to One over HTTP. No pepper, no `SELECT` from One `api_keys`, no copy of `Modules/One`.
- Steal HTTP **judgment** from Hub (`sk_test_` / `sk_live_`, `payments.checkouts:write`). Do not revive that mint. Prefix collision with Stripe is a closed incident.
- Processor vault secrets (Stripe `sk_test` / `sk_live`, CHIP Bearer, Billplz, …) are a **different family** from API credentials.
- `Pay:OneWebhookSecret` / per-org One webhook ciphertext is HMAC inbound, **not** an M2M caller secret.
- `Pay:WrapKey` / `SecretBox` wraps BYOK and HMAC secrets at rest. It is not caller auth.
- Buyers are not One humans. Public `/v1/pay/{token}` has no Bearer.

**Question this paper answers with live files:**

Today every merchant `/v1` door uses Bearer that MemberGate sends to One as a **user JWT**. There is no Pay-minted `sk_live_`. One already mints `lzr_sk_`. Does Pay accept One’s machine key? Does Pay mint its own? What would a second app actually put in `Authorization`?

Short answers, expanded below:

1. **Accept, as a shape:** `Bearer.TryGet` does not parse JWT vs `lzr_sk_`. It forwards whatever is after `Bearer `. One’s selector will treat `lzr_sk_` as an API key.
2. **Accept, as a productized M2M door: no.** MemberGate’s `authz/check` body **omits `user_id`**. Live One **400s** a key on that body (`user_id is required when authenticating with an API key.`) or **403s** if the key lacks `authz:check`. Writer overlay (`/me.tenants[].role` must be `owner|admin`) would then treat a typical key as `member` anyway. There is **no** hermetic test that sends `Bearer lzr_sk_…`. Grep of `lzr_sk_` / `ONE_API_KEY` / `Pay:OneApiKey` under `apps/lazuar-pay` is **empty**.
3. **Mint its own: no.** Focused Pay has no `POST /v1/api-keys`, no `sk_*` generator, no `one.ApiCredentials` table. Hub museum in this repo still mints `sk_test_` / `sk_live_`. IsolationTests forbids copying that cathedral.
4. **What a second app puts in `Authorization` today:** a human OIDC **access_token** JWT, the same bytes the merchant SPA already sends. If they put `lzr_sk_…`, whoami *would* work against live One; every org-gated money door would fail 400 or 403. If they put Hub `sk_live_` or Stripe `sk_live_`, One’s JWT scheme 401s and Pay maps that to 401.

019 already named “no machine key” as a kernel gap ([019-evals/00-evaluation.md](../019-evals/00-evaluation.md), [07](../019-evals/07-identity-authz-cors.md) §15 / G4). That paper’s writer-overlay sentence is **incomplete**: it assumed `/me` role would be reached. Live MemberGate dies on `authz/check` first. This 020 slice is the full trace.

002 closed occupancy, Plane B HMAC, CORS, spec honesty, and **per-org One inbound HMAC** (`6d730d15`). Kernel doors (M2M Bearer that is not a human JWT, outbound `payment.completed`, a second-app sample) were **out of 002**. They are still out.

---

## Coordinates

Focused Pay host is `apps/lazuar-pay` on **http://localhost:8081**. Merchant Vite is `apps/lazuar-pay-merchant` on **:5178**. Checkout Vite is `apps/lazuar-pay-checkout` on **:5179**. Identity plane is sibling One: API **:8080** (`/api/v1`), product login **:5175**, Zitadel issuer **:8085**. Old Hub ops **:3003** and portal **:3004** are a different product.

Pay does **not** run ASP.NET JWT middleware. Grep of `AddAuthentication` / `AddJwtBearer` under `apps/lazuar-pay/src` is empty. Staff identity is: browser PKCE against Zitadel → JWT `access_token` in `sessionStorage` → `Authorization: Bearer` on Pay → Pay forwards that **same header** to One `GET /me` and `POST …/authz/check`. One says 200 or Pay maps the failure. That is the whole AuthN loop **today**.

There is no second AuthN loop for machines. There is no process-level `ONE_API_KEY` that Pay attaches when the inbound header is missing. Fail closed: missing Bearer is 401 and **does not call One**.

---

## Files opened

### Pay host — identity / Bearer / gates

- `apps/lazuar-pay/src/Lazuar.Pay/Program.cs`
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
- `apps/lazuar-pay/src/Lazuar.Pay/Hosting/PayErrors.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.json`
- `apps/lazuar-pay/src/Lazuar.Pay/appsettings.Development.json`
- `apps/lazuar-pay/.env.example`
- `apps/lazuar-pay/README.md`

### Pay host — money / catalog / vault / wrap (gates + families)

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs` (`GatewayCredentialRow`, `OrgSettingsRow.OneWebhookCiphertext`)
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs`
- `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs`

### Pay host — tests / spec / merchant picker

- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Catalog/CatalogTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakeOneHandler.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs`
- `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- `packages/pay-spec/main.tsp`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.ts`
- `apps/lazuar-pay-merchant/src/auth/bearerToken.test.ts`
- `apps/lazuar-pay-merchant/src/lib/payApi.ts`

### Museum Hub in this repo (Family C — steal judgment, do not revive)

- `apps/lazuar-api/src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs`
- `apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`
- `apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs`

### Sibling One (live mint / auth — Pay must consume, not copy)

- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyDefaults.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyAuthenticationHandler.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyHasher.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyScopeHelper.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/AuthenticationExtensions.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/JwtAccessTokenGuard.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ScimTokenDefaults.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Tenancy/TenantAccessService.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/ApiKeys/ApiKeyEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/ApiKeys/ApiKeyService.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzService.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Platform/MeEndpoints.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEventCatalog.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/ApiKeys/ApiKey.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/MembershipRoles.cs`
- `lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/TenantPermissions.cs`
- `lazuar-one/packages/api-spec/modules/api-keys/models.tsp`
- `lazuar-one/packages/api-spec/modules/api-keys/routes.tsp`
- `lazuar-one/apps/lazuar-api/tests/Lazuar.One.Api.Tests/Integration/ApiKeyTests.cs`
- `lazuar-one/apps/lazuar-api/tests/Lazuar.One.Api.Tests/Integration/MeTests.cs`
- `lazuar-one/apps/lazuar-api/tests/Lazuar.One.Api.Tests/Unit/AuthzServiceResolveSubjectTests.cs`

### Historical / tracker (honesty of ticks, not authority)

- `plans/012-one-to-pay/08-machine-keys.md`
- `plans/013-prods/08-one-identity-production.md` §6 (MemberGate + `lzr_sk_` warning)
- `plans/013-prods/checklists/o13-lzr-sk.md` (ticked; tests do not match)
- `plans/011-new-lazuar-pay/11-checklist.md` (`NP-ONE-014`, `NP-ONE-020`, `NP-API-004`, `NP-SOON-007` still `todo`)
- `plans/011-new-lazuar-pay/02-one-integration.md`
- `plans/019-evals/07-identity-authz-cors.md`, `00-evaluation.md`, `08-contracts-spec-honesty.md`, `10-honesty-bugs-gaps.md`
- `plans/020-evals/README.md`

**Not opened on purpose:** Hub `Modules/One/**` beyond the mint/middleware/scopes needed to steal judgment; rail HTTP adapters beyond proving Family B uses `SecretBox.Unprotect`; One OpenFGA model; Zitadel Console.

---

## 1. Pay Bearer extraction — what token types are forwarded

### 1.1 There is no Pay AuthN middleware

`Program.cs` binds `OneOptions`, registers typed `HttpClient<OneClient>`, maps identity and money. It never calls `AddAuthentication`. CORS then maps:

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

Identity is per-handler. Each merchant door that needs a human (or, hypothetically, a machine) calls `Bearer.TryGet` and then `MemberGate` or `OneClient.GetWhoamiAsync`.

### 1.2 `Bearer.TryGet` — prefix only, fail closed, no JWT vs key split

```1:21:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/Bearer.cs
namespace Lazuar.Pay.Identity.Client;

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

Rules, from the bytes:

| Inbound `Authorization` | `TryGet` | Then |
|-------------------------|----------|------|
| missing / whitespace | false | 401 `"Missing bearer token"`, One **not** called |
| `Basic …` / raw `sk_live_…` / cookie | false | same 401 (must be `Bearer `) |
| `Bearer ` with empty remainder | false | 401 |
| `Bearer eyJ…` (compact JWT) | true | entire header forwarded to One |
| `Bearer lzr_sk_…` | true | entire header forwarded to One |
| `Bearer lzr_scim_…` | true | entire header forwarded to One (One product APIs 403 SCIM) |
| `Bearer sk_test_…` / `sk_live_…` | true | entire header forwarded; One treats as JWT → 401 |
| `Bearer <Zitadel PAT>` / opaque | true | forwarded; One JWT scheme 401 |

The method returns the **whole header value** (`"Bearer tok"`), not the token remainder. `OneClient` then `TryAddWithoutValidation("Authorization", authorization)` so odd `lzr_sk_` shapes do not throw `AuthenticationHeaderValue`. That is the right shape for a proxy. It is also why Pay **cannot** claim it “accepts only JWTs”: it never looked.

Fail closed on missing header is locked:

```55:63:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/WhoamiTests.cs
    [Test]
    public async Task Whoami_without_authorization_is_401_and_skips_one()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/whoami");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }
```

`OrgReadyTests.Ready_401_without_bearer_skips_one` is the same lock on a MemberGate door.

There is **no** test that the remainder starts with `lzr_sk_`, **no** test that a three-segment JWT is required, **no** test that `sk_live_` is rejected **before** One. JWT-likeness is a **merchant SPA** rule (`pickApiBearerToken`), not a host rule.

### 1.3 Whoami forwards the header verbatim

```13:23:apps/lazuar-pay/src/Lazuar.Pay/Identity/WhoamiEndpoints.cs
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
```

`GetWhoamiAsync` GETs `{BaseUrl}/me` (default `http://localhost:8080/api/v1/me`):

```35:45:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
    internal async Task<OneCallResult<WhoamiResponse>> GetWhoamiAsync(
        string authorization,
        string? tenantHint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantHint);
        }
```

Hermetic lock (`WhoamiTests.Whoami_maps_org_id_from_one_me`): inbound `"Bearer tok"` becomes outbound `Authorization: Bearer tok` on `GET …/me`. The fixture token is the string `tok`, not a JWT and not `lzr_sk_`. The host does not care.

Failure map (`WhoamiEndpoints.Map`):

| One / transport | Pay |
|-----------------|-----|
| 200 + mappable `/me` | 200 `WhoamiResponse` |
| 401 | 401 `"Identity provider rejected the token"` |
| 403 | 403 `"Identity provider forbade this caller"` |
| timeout / `HttpRequestException` | 503 `"Identity provider unreachable"` |
| 500 / unreadable JSON / missing `user_id` | 503 `"Identity provider failed"` |

`OneMeMapper.ToWhoami` copies `tenants[].role` and `tenants[].status` as strings. It does not know `api_key` vs human. `user_id` is whatever One sent. For a key, that is the **key GUID** (One `GetMeForApiKey`). Pay will happily return it.

`active_role` from One is **dropped**. Writer overlay re-reads `tenants[].role`, not `active_role`.

### 1.4 MemberGate forwards the same header into `authz/check` — body has no `user_id`

```7:46:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
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
        // …
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.CheckMemberAsync(authorization, orgId, hint.ToString(), cancellationToken);
```

`CheckMemberAsync` POSTs `tenants/{orgId}/authz/check` with:

```84:90:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs
        request.Content = JsonContent.Create(
            new OneAuthzCheckRequest
            {
                Relation = "member",
                Object = new OneAuthzObject { Type = "tenant", Id = orgId }
            },
            options: Json);
```

`OneAuthzCheckRequest` has `Relation` and `Object` only. There is no `User_id` property. OrgReady hermetic test **asserts the omission**:

```30:33:apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OrgReadyTests.cs
        Assert.That(factory.One.LastBody, Does.Contain("\"relation\":\"member\""));
        Assert.That(factory.One.LastBody, Does.Contain("\"type\":\"tenant\""));
        Assert.That(factory.One.LastBody, Does.Contain("\"id\":\"t1\""));
        Assert.That(factory.One.LastBody, Does.Not.Contain("user_id"));
```

For a **user JWT** that omission is correct: One `AuthzService.ResolveSubject` uses the caller when `user_id` is blank. For an **API key** that omission is a 400. Section 4 traces the live One path. The hermetic test that “locks” the omission is a JWT-shaped fixture (`Bearer tok` + Fake One 200 `{"allowed":true}`). It does not know keys exist.

MemberGate maps One status:

| One | Pay `RequireMemberAsync` |
|-----|--------------------------|
| 200 `allowed: true` | pass (`null`) |
| 200 `allowed: false` | 403 `"Not a member of this org"` |
| 401 | 401 `"Identity provider rejected the token"` |
| 403 with `"suspend"` in detail | 403, One detail passed through |
| 403 otherwise | 403 `"Not a member of this org"` (**swallows** One’s `API key lacks required scope authz:check.`) |
| 400 | 400, One `detail` if present |
| 429 | 429 `"Identity provider rate limited"` |
| timeout / transport | 503 `"Identity provider unreachable"` |
| other | 503 `"Identity provider failed"` |

`Ready_400_when_one_400` proves Pay **will** surface `detail` on 400. That is the most likely live status for a well-scoped `lzr_sk_` (section 4).

### 1.5 Merchant SPA will not send `lzr_sk_`

```10:18:apps/lazuar-pay-merchant/src/auth/bearerToken.ts
/**
 * Pick a Bearer token for Pay / One APIs.
 * Send only a JWT access_token. Never send id_token (not an API credential).
 */
export function pickApiBearerToken(user: User | null | undefined): string | undefined {
  if (!user) return undefined
  if (isJwtLike(user.access_token)) return user.access_token
  return undefined
}
```

`isJwtLike` requires three non-empty `.` segments. `lzr_sk_` has no dots. The SPA would send nothing, then 401. That is **correct** for a human chrome. A second app is not this origin. `payApi.ts` always sets `Authorization: Bearer ${accessToken}` — the caller of `payJson` is assumed to have already picked a JWT.

`packages/pay-spec/main.tsp` says “Requires Bearer” on whoami / writer doors. It never names `lzr_sk_`, never `@useAuth`, never a machine-key model. Grep of `lzr_sk_` / `api-keys` under `packages/pay-spec` is empty.

Pay README “Live whoami” tells the engineer to copy the **access_token**, not a key:

```48:51:apps/lazuar-pay/README.md
Log in at `http://localhost:5175` (product login). Demo user is whatever One README lists (often `ada@acme.test` / `Password1!`). Copy the **access_token**, not the `id_token`.

curl -sS -H "Authorization: Bearer $ACCESS_TOKEN" http://localhost:8081/v1/whoami
```

That is Mode U. Mode M is undocumented on the live host.

### 1.6 Token-type verdict

Pay forwards **any** non-empty `Bearer ` remainder to One. It does not mint, hash, or classify. Classification is One’s PolicyScheme (section 2). Fail closed is: **no Bearer → 401, skip One**. It is not: **wrong family → 401 before One**. A Stripe secret, a Hub `sk_live_`, an `id_token`, a PAT, and a `lzr_sk_` all leave Pay’s extractor as “a Bearer”. Honesty: the host is a proxy, not an IdP.

---

## 2. One live API keys (sibling SHA `6b78e9d4`)

This section is **One source**, not a restatement of 012/08. 012 was written against One `0f79fe4f` (20 August 2026). This paper opened `6b78e9d455618f2a0cfadf46089d5f993f081983`. The mint contract is still the same family. Quotes below are from that SHA.

### 2.1 HTTP contract

TypeSpec (`packages/api-spec/modules/api-keys/routes.tsp`):

```11:42:lazuar-one/packages/api-spec/modules/api-keys/routes.tsp
@route("/tenants/{tenantId}/api-keys")
interface ApiKeyOperations {
  @useAuth(BearerAuth)
  @summary("Create a tenant API key (secret returned once)")
  @post
  createApiKey( … ): { @statusCode statusCode: 201; @body body: ApiKeyCreatedResponse; } | Err;

  @useAuth(BearerAuth)
  @summary("List API key metadata (no secrets). API keys need keys:read.")
  @get
  listApiKeys( … ): LazuarOneApi.Core.PaginatedResponse<ApiKey> | Err;

  @useAuth(BearerAuth)
  @summary("Revoke an API key")
  @delete
  @route("/{keyId}")
  revokeApiKey( … ): { @statusCode statusCode: 204; } | Err;
}
```

Runtime base is `/api/v1`. Full local URLs:

```text
POST   http://localhost:8080/api/v1/tenants/{tenantId}/api-keys
GET    http://localhost:8080/api/v1/tenants/{tenantId}/api-keys?page=&page_size=
DELETE http://localhost:8080/api/v1/tenants/{tenantId}/api-keys/{keyId}
```

Handlers: `Features/ApiKeys/ApiKeyEndpoints.cs`. Create requires membership `minRole: admin` (JWT admin|owner, or key with `admin`/`*`). List: JWT any member; a **key** needs `keys:read` (`RequireApiKeyScope(user, "keys:read")`). Revoke: admin again. Cross-tenant / missing revoke → 403 `"Not allowed to revoke this API key."` (D06, existence not leaked).

`CreateApiKeyRequest` (`models.tsp`):

- `name` required, 1–200.
- `scopes` optional. Comment on the model: omitted → `["tenant:read"]`; **explicit empty array is 400**; empty is no longer full-admin (P12). Catalog listed on the model: `authz:check, members:read, apps:read, keys:read, tenant:read, webhooks:read, webhooks:write, events:read, audit:read, admin, *`.
- `expires_at` optional UTC.
- Created response includes `secret` (`Format: lzr_sk_…`). List/get metadata **never** includes `secret` or `key_hash`.

Live service (`ApiKeyService.CreateAsync`):

```65:76:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/ApiKeys/ApiKeyService.cs
        // D69: omitted scopes → default tenant:read; explicit empty array → 400.
        IReadOnlyList<string> scopesToNormalize;
        if (scopes is null)
        {
            scopesToNormalize = ApiKeyScopeHelper.DefaultCreateScopes;
        }
        else if (scopes.Count == 0)
        {
            throw ServiceException.BadRequest(
                "scopes must contain at least one value. " +
                "Omit scopes to default to tenant:read, or pass admin/* for full tenant access. " +
                "Empty scopes are no longer full-admin (P12).");
        }
```

Unknown scope → 400 `"Unknown scope '{s}'. Allowed: …"`. Hub Family C strings (`payments.checkouts:write`) are unknown here. Tests: `Create_returns_secret_once_list_has_no_secret_db_hash_only` (`secret.Should().StartWith("lzr_sk_")`, list body has no `"secret"`), `Create_omitted_scopes_defaults_to_tenant_read`, `Create_explicit_empty_scopes_returns_400`.

### 2.2 Prefix, hash, secret-once

```11:12:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyDefaults.cs
    /// <summary>Product key prefix (D08).</summary>
    public const string KeyPrefix = "lzr_sk_";
```

Generation:

```249:256:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/ApiKeys/ApiKeyService.cs
    /// <summary>lzr_sk_ + base64url(32 random bytes).</summary>
    private static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var payload = Base64UrlEncode(bytes);
        return ApiKeyDefaults.KeyPrefix + payload;
    }
```

Public prefix stored: first 16 characters of the secret (`secret[..16]`). Not unique. Not a lookup key.

Hash (HMAC-SHA256, pepper is the HMAC **key**, secret is the **message**; lowercase hex):

```12:18:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyHasher.cs
    public static string Hash(string secret, string? pepper)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var key = Encoding.UTF8.GetBytes(pepper ?? "");
        var data = Encoding.UTF8.GetBytes(secret);
        var bytes = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
```

Pay **cannot** verify a merchant `lzr_sk_` locally. Pay has no `ApiKeys:Pepper` and must never grow one. Introspection is HTTP to One with that Bearer.

Domain row (`Domain/ApiKeys/ApiKey.cs`): `Id`, `TenantId`, `Name`, `KeyPrefix`, `KeyHash`, `Scopes`, `CreatedBy`, timestamps. Comment: “Full secret is never stored — only KeyHash.” (The hash comment says “SHA-256 hex of peppered secret material”; the hasher is HMAC, not concat SHA-256. Trust the hasher.)

### 2.3 Bearer selector — JWT vs `lzr_sk_` vs `lzr_scim_`

```10:47:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/AuthenticationExtensions.cs
    /// Detection order: <c>lzr_scim_</c> → ScimToken; <c>lzr_sk_</c> → ApiKey; else JWT.
    …
                        if (token.StartsWith(ScimTokenDefaults.KeyPrefix, StringComparison.Ordinal))
                        {
                            return ScimTokenDefaults.AuthenticationScheme;
                        }

                        if (token.StartsWith(ApiKeyDefaults.KeyPrefix, StringComparison.Ordinal))
                        {
                            return ApiKeyDefaults.AuthenticationScheme;
                        }
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
```

`ScimTokenDefaults.KeyPrefix` is `lzr_scim_`. Product `/api/v1` then `RejectScim` → 403 `"SCIM tokens cannot call product APIs."`

JWT leftover (including Hub `sk_live_`, Stripe `sk_live_`, opaque PAT, `id_token`) hits JwtBearer. `JwtAccessTokenGuard` rejects missing `jti` (`"JWT access tokens must include a jti claim (ID tokens are not API credentials)."`). Challenge body: `"Authentication is required or the access token is invalid."`

API key handler (`ApiKeyAuthenticationHandler`):

- Requires `Authorization: Bearer lzr_sk_…`.
- Hash → lookup `api_keys.key_hash` → `Verify` in fixed time.
- Unknown → Fail `"Invalid API key."`
- `RevokedAt != null` → Fail `"API key has been revoked."`
- `ExpiresAt <= now` → Fail `"API key has expired."`
- Claims: `sub` / `NameIdentifier` = **key Id (GUID)**; `tenant_id` = bound tenant; `auth_type` = `api_key`; repeatable `scope`.
- Challenge body (client-visible): `"Authentication is required or the API key is invalid."` Logs may say revoked vs expired vs unknown; the client does not get a distinct code. Prefix in logs truncated to 16 chars. Full token never logged.

### 2.4 Scope catalog (closed) and empty ≠ admin

```15:31:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Auth/ApiKeyScopeHelper.cs
    public static readonly IReadOnlyList<string> KnownScopes =
    [
        "authz:check",
        "members:read",
        "apps:read",
        "keys:read",
        "tenant:read",
        "webhooks:read",
        "webhooks:write",
        "events:read",
        "audit:read",
        "admin",
        "*",
    ];

    /// <summary>Default scopes when create request omits scopes (D69).</summary>
    public static readonly IReadOnlyList<string> DefaultCreateScopes = ["tenant:read"];
```

`HasAnyScope`: JWT principals **always pass**. Empty key scopes **deny** when any scope is required (P12). `admin` and `*` short-circuit. `IsTenantAdminEquivalent` is `admin`/`*` only.

`authz/check` coarse gate:

```192:208:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs
    /// <summary>Coarse M2M scope gate: keys need authz:check (or admin / *). Empty scopes denied (P12).</summary>
    private static IResult? DenyApiKeyScope(ClaimsPrincipal user)
    {
        if (!ApiKeyScopeHelper.IsApiKey(user))
        {
            return null;
        }

        if (ApiKeyScopeHelper.HasAnyScope(user, "authz:check"))
        {
            return null;
        }

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: "API key lacks required scope authz:check.");
    }
```

Then, still on the same handler:

```211:236:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs
    private static IResult? RejectApiKeyAuthzSubject(ClaimsPrincipal user, string? userId)
    {
        if (!ApiKeyScopeHelper.IsApiKey(user))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "user_id is required when authenticating with an API key.");
        }

        var keyId = TenantAccessService.GetUserId(user);
        if (!string.IsNullOrEmpty(keyId)
            && string.Equals(userId.Trim(), keyId, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "user_id must be a user subject, not the API key id.");
        }

        return null;
    }
```

Service-level duplicate (`AuthzService.ResolveSubject`) throws the same two 400s. Unit tests: `Api_key_omit_user_id_throws_bad_request`, `Api_key_self_user_id_throws_bad_request`. JWT omit `user_id` returns the caller (`Jwt_omit_user_id_returns_caller`). **This is the hinge** between Pay MemberGate (JWT-shaped body) and One keys.

Keys also cannot `POST /tenants` even with `admin`/`*` (`TenantAccessService.RejectApiKey` → 403 `"This operation requires a user session, not an API key."`). Platform admin is always false for keys.

### 2.5 `GET /me` as a key

`MeEndpoints.GetMe` branches:

```50:53:lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Features/Platform/MeEndpoints.cs
        if (ApiKeyScopeHelper.IsApiKey(user))
        {
            return await GetMeForApiKey(user, sub, db, access, cancellationToken);
        }
```

`GetMeForApiKey`:

- `user_id` = key id (`sub`).
- `email` / `name` typically null.
- `is_platform_admin` false.
- `tenants` is 0–1, the bound tenant (deleted tenant omitted).
- `role` is `"admin"` only when the key has `admin` or `*`; **otherwise `"member"`**. That role is a **projection**, not a membership row. The key is not in `memberships`.
- `GET /me` does **not** require `tenant:read`. Any valid non-revoked key works.
- `permissions` is `ProjectCatalogPermissions`: `admin`/`*` maps to the ROLE-03 **admin** catalog; other scopes are listed only if `TenantPermissions.IsKnown`. ROLE-03 is `{ tenant:update, tenant:delete, domains:manage, roles:manage, events:read, audit:read, sso:manage, scim:manage, streams:manage }`. **`tenant:read` and `authz:check` are not in that list.** A key minted as `["tenant:read","authz:check"]` returns `permissions: []`. Test `Me_and_tenant_list_project_key_scopes_into_permissions`: scopes `events:read` + `tenant:read` project **only** `events:read`.

Locked: `MeTests.Api_key_me_returns_key_id_and_bound_tenant` — `user_id` equals key GUID, `is_platform_admin` false, one tenant, **role `"member"`** for `tenant:read`. `Api_key_admin_scope_me_role_is_admin` is the admin projection.

Pay must **not** treat `/me.tenants[].permissions` as the API-key scope list. Pay must **not** treat `/me.tenants[].role == member` as “this machine cannot charge” without a written M2M policy — that is the human writer overlay colliding with a synthetic role (section 4).

### 2.6 Webhooks already produced (not a Pay M2M secret)

`WebhookEventCatalog` includes `api_key.created` and `api_key.revoked`. Producer payloads are metadata (`key_id`, `name`, `prefix`, `scopes`, …). Never `secret` or `key_hash`. Pay does not subscribe to these for cache drop because Pay has **no** introspection cache of merchant keys. First-slice One inbound in Pay is `tenant.suspended` / `tenant.reactivated` HMAC (section 7).

---

## 3. Does Pay ever send a machine key to One? Honest empty set

Searches run on this SHA against the focused host (not Hub museum, not `plans/`):

| Needle | Path | Result |
|--------|------|--------|
| `lzr_sk_` | `apps/lazuar-pay/**` | **no matches** |
| `ONE_API_KEY` | `apps/lazuar-pay/**` | **no matches** |
| `OneApiKey` / `Pay:OneApiKey` / `One__ApiKey` | `apps/lazuar-pay/**` | **no matches** |
| `api-keys` | `apps/lazuar-pay/**` | **no matches** |
| `ApiKey` as a Pay type | `apps/lazuar-pay/src` | **no matches** |

`OneOptions` is two properties:

```1:11:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneOptions.cs
public sealed class OneOptions
{
    public const string Section = "One";

    /// <summary>One API prefix, e.g. http://localhost:8080/api/v1. Client appends /me.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080/api/v1";

    public int TimeoutSeconds { get; set; } = 5;
}
```

`appsettings.json` `One` section is `BaseUrl` + `TimeoutSeconds` only. `appsettings.Development.json` does not even repeat `One` (inherits). `.env.example`:

```1:3:apps/lazuar-pay/.env.example
# One HTTP façade (no PAT, no OpenFGA admin).
One__BaseUrl=http://localhost:8080/api/v1
One__TimeoutSeconds=5
```

Later lines document `Pay__WrapKey`, `Pay__StripeWebhookSecret`, `Pay__PublicBaseUrl`, `Pay__CheckoutBaseUrl`, `Pay__CorsOrigins`, `Pay__OneWebhookSecret`. **No** `ONE_API_KEY`. Comment on the One façade: “no PAT, no OpenFGA admin” — it no longer says “no lzr_sk_ in C-phases” (012/013 used that phrase). The env still does not grow a key.

`OneClient` never sets `DefaultRequestHeaders.Authorization`. Every outbound Authorization is the **inbound** header, request-scoped. There is no fallback from missing user JWT to an env key. That fail-closed is good. It also means Pay the **process** never originates a machine identity to One: not for whoami, not for `authz/check`, not for webhook registration (Pay does not `POST` One `/tenants/{id}/webhooks` — README line 69).

`PayApiFactory` test One client is `BaseUrl = "http://one.test/api/v1"` plus the inbound header. Tests send `"Bearer tok"`. None send `"Bearer lzr_sk_"`.

Merchant / checkout greps for `lzr_sk_` / `api-keys` / `ApiKeys` (excluding `node_modules`) are empty. There is no Pay chrome for One Settings → API keys.

**Honest empty set:** Pay never *sends a Pay-held machine key* to One, because Pay holds none. Pay *will replay* a caller’s `lzr_sk_` if a caller presents one. Those are different sentences. 012 Mode M (`ONE_API_KEY` in Pay env for workers) did not land after 018/002.

`NP-ONE-020` (“Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC”) is still `todo` on 011/11. Live: Pay holds `client_id` on the merchant SPA, per-org / process HMAC for Plane A, **no** `lzr_sk_`. The refuse half (never PAT / FGA admin) is still true.

---

## 4. Writer vs member — would a machine key’s `/me` even have a role?

### 4.1 What writer is today (humans)

`RequireWriterAsync`:

```60:97:apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs
    public static async Task<IResult?> RequireWriterAsync( … )
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

        return null;
    }
```

Two hops:

1. `POST …/authz/check` `relation=member` (JWT-shaped body, no `user_id`).
2. `GET /me`, find `tenants[]` whose `id == orgId`, require `status` active (or blank), require `role` ∈ {`owner`,`admin`}.

Hermetic proofs of hop 2 (Fake One returns `allowed: true` then `/me` with `role: member`):

- `CheckoutTests.Member_cannot_create_checkout` → 403
- `CatalogTests.Member_cannot_create_product` → 403
- `PaymentLinkTests.Member_cannot_create_payment_link` → 403
- `OneWebhookTests.Member_cannot_put_one_webhook_secret` → 403

Those fixtures never mention keys. They prove the human overlay.

Writer doors on this SHA (not exhaustive of every GET):

| Door | Gate |
|------|------|
| `POST /v1/checkouts` | `RequireWriterAsync` |
| `POST /v1/payment-links` | `RequireWriterAsync` |
| `POST /v1/orgs/{orgId}/products` | `RequireWriterAsync` |
| `PUT /v1/orgs/{orgId}/gateway` | `RequireWriterAsync` |
| `PUT /v1/orgs/{orgId}/one-webhook` | `RequireWriterAsync` |

Member doors: `GET /v1/orgs/{orgId}/ready`, list checkouts / links / products / payments / receipts, `GET` checkout by id (404-masks non-suspend 403), `GET` gateway, `GET` one-webhook configured flag.

Public buyer `GET|POST /v1/pay/{token}`: **no Bearer**. A second app that is a **cashier** still needs the merchant mint doors, not the buyer token.

### 4.2 Live One `/me` for a key — yes, there is a role, and it is the wrong kind of role

From section 2.5: a `tenant:read` (or `tenant:read` + `authz:check`) key’s `/me.tenants[].role` is **`"member"`**. Only `admin`/`*` project `"admin"`. There is never `"owner"` on a key (no membership row; `IsTenantAdminEquivalent` maps to admin, not owner).

If hop 1 somehow passed, hop 2 would 403 `"Writer role required"` for every least-privilege key 012 told Pay to mint. The only keys that would pass hop 2 are `admin`/`*` — which 012 forbids for the worker (`Never * / empty / admin` unless a later paper writes why).

019/07 §15 said: “Writer overlay would then use that synthetic role.” True **if** hop 1 passed. Hop 1 does not pass against live One. Trace:

### 4.3 Predicted path if a second app sends `Authorization: Bearer lzr_sk_…` to Pay today

Assume the key is valid, not revoked, not expired, bound to tenant T, and Pay path `{orgId}` = T. Pay does not prefix-check. `Bearer.TryGet` succeeds. OneClient replays the header.

**A. `GET /v1/whoami`**

1. Pay → One `GET /api/v1/me` with `Bearer lzr_sk_…`.
2. One selector: prefix `lzr_sk_` → ApiKey handler → 200 `GetMeForApiKey`.
3. Pay maps 200. Body: `user_id` = key GUID, `email` null, `is_platform_admin` false, `tenants` length 1, `role` `member` (unless `admin`/`*`), `active_org_id` = T.
4. **Pay 200.** This is the only merchant door that would succeed for a typical key.

There is no hermetic test of this. `o13-lzr-sk.md` ticks “Fake One 200 when the test sends `Authorization: Bearer lzr_sk_…`”. `WhoamiTests` sends `Bearer tok`. Checklist honesty hole (missing feat / stale tick), not a 011 flip.

**B. Member door, e.g. `GET /v1/orgs/{T}/ready`, key scopes `["tenant:read"]` (default / omitted-scopes mint)**

1. Pay → One `POST /api/v1/tenants/{T}/authz/check` with body `{relation: member, object: {type: tenant, id: T}}` and **no `user_id`**.
2. One authenticates the key.
3. `RequireMembershipAsync` for API keys: bound tenant matches T, tenant active → `TenantContext` with `UserId` = key id, `Role` = `member`.
4. `DenyApiKeyScope`: key lacks `authz:check` → **403** `"API key lacks required scope authz:check."`
5. Pay `RequireMemberAsync` sees 403, detail has no `"suspend"` → **403 `"Not a member of this org"`**. The real reason is swallowed.

**C. Member door, key scopes `["tenant:read","authz:check"]` (012’s recommended first mint)**

1–3 as B, but `DenyApiKeyScope` passes (`HasAnyScope` finds `authz:check`).
4. `RejectApiKeyAuthzSubject`: `user_id` missing → **400** `"user_id is required when authenticating with an API key."`
5. Pay maps 400 and **forwards the detail** (`Ready_400_when_one_400` is the lock). **Pay 400** with that sentence.

This is the most likely status for an engineer who actually followed 012 §6.1.

**D. Member door, key scopes `["admin"]` or `["*"]`**

`HasAnyScope` short-circuits, then the same **400** as C (still no `user_id`). `admin` does not skip `RejectApiKeyAuthzSubject`.

**E. Writer door, any of B/C/D**

Hop 1 fails (403 or 400). Hop 2 (`GET /me` role overlay) **never runs**. A second app cannot mint a checkout, pay link, product, or vault row with `lzr_sk_` today.

If hop 1 were later patched to skip `authz/check` for keys and only call `/me`, hop 2 would still 403 `"Writer role required"` unless the key is `admin`/`*`. That is a second, independent hole.

**F. Key bound to tenant B, path org A**

One `RequireApiKeyTenantAsync`: `keyTenantId != tenantId` → 403 `"Not a member of this tenant."` Pay maps generic `"Not a member of this org"` unless the detail contains `"suspend"`. Fail closed on cross-tenant. Good. Swallowed detail. Bad for debugging, not a leak.

**G. Invalid / revoked / expired key**

One ApiKey handler 401. Pay 401 `"Identity provider rejected the token"`. Fail closed.

**H. `Bearer sk_live_…` / `sk_test_…` (Hub Family C or Stripe Family B pasted as Pay Bearer)**

Pay forwards. One selector: not `lzr_sk_` / `lzr_scim_` → JWT. Validation fails. One 401. Pay 401. Fail closed **at One**, not at Pay’s precheck. 012 wanted Pay to fail closed **before** calling One on wrong family so a Stripe BYOK cannot leak into Bearer logs. That precheck does not exist.

**I. `Bearer lzr_scim_…`**

One 403 `"SCIM tokens cannot call product APIs."` Pay member map swallows to `"Not a member of this org"` (no `"suspend"`). Whoami map would 403 `"Identity provider forbade this caller"`.

**J. One down**

503 `"Identity provider unreachable"` / `"Identity provider failed"`. Staff and would-be M2M fail closed. Captured money / PSP webhooks do not use MemberGate.

**K. One 200 `allowed: true` in hermetic Fake One with `Bearer lzr_sk_…`**

Fake One does not implement One’s 400. A test that stubs 200 would **green a lie**. That is why the lock tests in section 10 must either (a) drive live One, or (b) teach Fake One the key 400/403 matrix, or (c) assert Pay’s *own* branch before the HTTP call once Pay grows one.

### 4.4 `authz/check` `user_id` = key id is rejected even if Pay started sending it

012 / NP-ONE-015: “A key’s own `user_id` is the key id and is **rejected** as the check subject (400).” Live: `RejectApiKeyAuthzSubject` and `ResolveSubject` both 400 `"user_id must be a user subject, not the API key id."` Recipe R2 uses the **minting human’s** `/me.user_id`.

That recipe is for “Pay worker asks One whether **Ada** is a member.” It is **not** “second app is a member because it holds a key.” Passing Ada’s Zitadel sub from a second app would make the second app **impersonate Ada** on Pay’s writer overlay (still `owner|admin` on Ada, not on the key). That is the wrong hatch for M2M. Do not “fix” MemberGate by stuffing a human `user_id` into the check when the caller is a key unless the product is “this job acts as Ada.” A cashier integration is not Ada.

### 4.5 019 vs this paper

019/07: “Opaque `lzr_sk_…` would pass this gate and be forwarded to One” — **true**. “Writer overlay would then use that synthetic role” — **only after a 200 member check that live One will not give**. 013/08 §6.4 already warned: “MemberGate today forwards whatever Bearer the caller sent. … For an API-key caller, One returns **400** `user_id is required when authenticating with an API key.` Production MemberGate must branch.” That branch was not built in 018 or 002. HEAD `6d730d15` is per-org HMAC storage, not MemberGate.

---

## 5. Old Hub homemade `sk_test_` / `sk_live_` (museum) — steal judgment, do not revive

This repo still contains the cathedral mint. IsolationTests exist so focused Pay does not grow a project reference into it. Steal the **HTTP judgment**; leave the table.

### 5.1 Mint

```46:57:apps/lazuar-api/Modules/One/Application/Commands/GenerateApiCredentialCommand.cs
        var tokenPair = _tokenGenerator.GenerateSecureToken(40);
        var prefix = request.IsTestMode ? "sk_test_" : "sk_live_";

        var fullPlainToken = $"{prefix}{tokenPair.PlainToken}";
        var fullHash = _tokenGenerator.HashToken(fullPlainToken);
        …
        var scopes = PlatformApiScopes.NormalizeAndValidate(request.Scopes);
```

Prefix is **the same as Stripe secret keys**. That collision is why old docs told Aura to `GET /me` rather than regex the prefix. New Pay’s prefix decision (012): One already chose `lzr_sk_`. Do not mint a third `sk_*` product.

### 5.2 Hash — plain SHA-256, no pepper

```23:28:apps/lazuar-api/BuildingBlocks/Infrastructure/TokenGeneratorService.cs
    public string HashToken(string plainToken)
    {
        var bytes = Encoding.UTF8.GetBytes(plainToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
```

One’s hasher is HMAC-SHA256(pepper, secret). Different database, different algorithm, different prefix. Do not migrate `sk_live_` rows into One `lzr_sk_` rows. Remint.

### 5.3 Scopes — product catalog on the key

```12:26:apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs
    public const string LhdnDocumentsRead = "lhdn.documents:read";
    public const string LhdnDocumentsWrite = "lhdn.documents:write";
    public const string PaymentsCheckoutsRead = "payments.checkouts:read";
    public const string PaymentsCheckoutsWrite = "payments.checkouts:write";
    public const string WebhooksEndpointsManage = "webhooks.endpoints:manage";
    public const string CommerceSubscriptionsRead = "commerce.subscriptions:read";
    public const string CommerceSubscriptionsWrite = "commerce.subscriptions:write";
```

`DefaultAuraIntegratorScopes` = write + read checkouts + webhook manage. One’s catalog will **400** every one of those strings (`Unknown scope`). New Pay must not invent a parallel `payments.checkouts:write` on One keys. One keys authorize **One**. Pay authorizes Pay with “this Bearer introspects as bound to this org.”

### 5.4 Middleware

`ApiKeyAuthenticationMiddleware.TryGetApiKey` accepts `Authorization: Bearer sk_live_|sk_test_...` **or raw** `Authorization: sk_...`. Looks up `one."ApiCredentials"` by SHA-256 hash, caches 5 minutes, sets `IsTestMode` from prefix, attaches `scope` claims. That is Hub K1.

Focused Pay has no equivalent. `Bearer.TryGet` does **not** accept raw `sk_…` without `Bearer `. Good: do not grow a second extractor that sniffs Stripe-shaped secrets as Pay identity.

### 5.5 Cutover papers in this repo are Hub→Hub, not Hub→sibling One

`plans/004-maintenance/api-key-cutover-design.md` and `plans/005-remaining/01-api-key-one-only-cutover.md` moved old Pay’s K1 store from Lhdn → Pay’s `Modules.One` table. That is **not** sibling `lazuar-one` `api_keys`. 012 said this out loud. Still true on this SHA.

**Steal:** secret shown once; hash at rest; revoke is 401 next call; least privilege; do not log the secret. **Refuse:** `sk_*` prefix, SHA-256 without pepper, product scopes on the identity key, Pay-side `ApiCredentials` table, `IsTestMode` from prefix, Aura env name `LAZUAR_API_KEY=sk_test_…`.

---

## 6. Processor vault secrets vs API credentials — different families

Family B lives in `gateway_credentials`, AES-GCM wrapped by `SecretBox` (`Pay:WrapKey`). Row:

```78:88:apps/lazuar-pay/src/Lazuar.Pay/Data/Rows.cs
public sealed class GatewayCredentialRow
{
    public required string OrgId { get; set; }
    public required string Provider { get; set; }
    public required string Ciphertext { get; set; }
    public string? Last4 { get; set; }
    public string? WebhookCiphertext { get; set; }
    public string? PublicMerchantId { get; set; }
    public string Environment { get; set; } = "test";
    public DateTimeOffset UpdatedAt { get; set; }
}
```

`PUT /v1/orgs/{orgId}/gateway` is a **writer** door. Body fields: `secret`, `webhook_secret`, optional `key_id`+`key_secret` concat, `public_merchant_id`, `environment`. Stripe secrets look like `sk_test_…` / `sk_live_…`. CHIP is sent as `Authorization: Bearer` **to CHIP**, not to One:

```61:63:apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs
        var client = http.CreateClient("chip");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "purchases/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", box.Unprotect(cred.Ciphertext));
```

Stripe:

```27:28:apps/lazuar-pay/src/Lazuar.Pay/Rails/Stripe/StripeHosted.cs
        var secret = box.Unprotect(cred.Ciphertext);
        var service = new SessionService(new StripeClient(secret));
```

These bytes never become `OneClient` Authorization. `OneClient` only sees the **staff** Bearer that passed `RequireWriterAsync`. Catalog tests paste `"secret":"sk_test_dummy"` into the **vault** PUT, with `Authorization: Bearer tok` as the staff token. That is the correct split.

Do not mix:

| Family | Prefix / shape | Where | Authorization to One | Authorization to Pay `/v1` |
|--------|----------------|-------|----------------------|----------------------------|
| A. One product key | `lzr_sk_` | One `api_keys`, HMAC+pepper | Bearer (Mode M / merchant M2M replay) | **intended** later, not live |
| B. Processor BYOK | Stripe `sk_test_`/`sk_live_`, CHIP, Billplz, Xendit, Razorpay | Pay vault ciphertext | **Never** | **Never** as identity |
| C. Hub homemade | `sk_test_`/`sk_live_` | Hub `one.ApiCredentials` | n/a | museum only |
| D. Staff OIDC | JWT access_token (`jti`) | browser sessionStorage | Bearer Mode U **today** | Bearer **today** |
| E. One webhook HMAC | `whsec_…` | Pay process + per-org ciphertext | Never (inbound verify) | Never (no Bearer on `POST /v1/one/webhooks`) |
| F. Wrap key | 32-byte base64 | Pay process env | Never | Never |

A Stripe secret that 401s One or Pay is a paste error. Old Hub documented: “This looks like a Stripe secret. Mint a Lazuar Pay key.” That sentence referred to Family C. New sentence: “This looks like a Stripe secret. Paste it in Payment settings, not in One API keys, and not as `ONE_API_KEY`.”

---

## 7. `Pay:OneWebhookSecret` / per-org ciphertext — HMAC inbound, not M2M

HEAD subject is this family: `fix(pay): store per-org One webhook secrets`.

`POST /v1/one/webhooks` has **no** `Bearer.TryGet`. It reads raw body, resolves a secret, verifies `X-Lazuar-Signature` + `X-Lazuar-Timestamp` (or combined `t=,v1=`).

Resolve order (`OneWebhookEndpoints.ResolveSecretAsync`):

1. Peek `org_id` / `tenant_id` from JSON.
2. If `org_settings.one_webhook_ciphertext` is set, `SecretBox.Unprotect` it.
3. Else process `config["Pay:OneWebhookSecret"]`.
4. Missing → 503 `"One webhook secret missing"`. Bad HMAC → 401 `"Invalid HMAC"`.

Staff configure per-org via `PUT /v1/orgs/{orgId}/one-webhook` `{ "webhook_secret" }` — **writer** Bearer to **Pay**, then wrap. GET returns `{ org_id, webhook_configured }` without the secret. Member cannot PUT (`Member_cannot_put_one_webhook_secret`).

`.env.example`:

```27:30:apps/lazuar-pay/.env.example
# One-shop HMAC fallback for POST /v1/one/webhooks. Multi-shop: owner PUT
# /v1/orgs/{orgId}/one-webhook { "webhook_secret" }. Pay does not register the
# URL with One (no PAT). One SSRF blocks loopback.
# Pay__OneWebhookSecret=
```

This secret proves **One sent the event**. It does not let a second app call `POST /v1/checkouts`. Putting `whsec_…` in `Authorization: Bearer` would fail `Bearer.TryGet` only if the `Bearer ` prefix is missing; with the prefix, One would treat it as a JWT and 401. Either way it is not Family A.

`OneWebhookSignature.TryVerify` is HMAC-SHA256 over `{unix}.{body}`. Same primitive as One’s key hasher, **different key, different message, different door**.

Pay does not handle `api_key.revoked` on this inbound path. `ApplyAsync` only special-cases `tenant.suspended` (sets `charges_paused`) and `tenant.reactivated`. A revoked merchant `lzr_sk_` would not drop a Pay cache because there is no cache. When M2M lands and Pay caches introspection, this door must grow a handler — still HMAC, still not Bearer.

---

## 8. `WrapKey` / `SecretBox` — wrapping, not caller auth

```7:21:apps/lazuar-pay/src/Lazuar.Pay/Secrets/SecretBox.cs
/// <summary>AES-GCM wrap for BYOK. Key from Pay:WrapKey (32-byte base64). Never log plaintext.</summary>
public sealed class SecretBox(IConfiguration config, IHostEnvironment env)
{
    public string Protect(string plaintext)
    {
        var key = LoadKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        …
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
```

`LoadKey`: `Pay:WrapKey` required outside Testing (32-byte base64). Testing may hash `"lazuar-pay-dev-wrap-key"`. Used to protect:

- Family B processor `Ciphertext` / `WebhookCiphertext`
- Family E per-org `OrgSettingsRow.OneWebhookCiphertext`

Not used to hash `lzr_sk_`. Not used as `Authorization`. A second app does not send the wrap key. If they did, Pay would treat it as a Bearer remainder and One would 401.

Do not store merchant `lzr_sk_` values in Pay wrapped “for convenience.” That would make Pay a secret vault for One keys — worse than HMAC webhooks (012 §7.4). Pay env holds **at most one** worker `lzr_sk_` for **one** tenant, later, when a job exists.

---

## 9. 012/08 vs live Pay after 018/002

012 (20 August 2026) was analysis, not an order. Binding decisions it left for implementers, versus this SHA:

| 012 said | Live Pay `6d730d15` after 018 + 002 |
|----------|-------------------------------------|
| One already mints `lzr_sk_`. Pay does not reimplement the table / pepper / SELECT | **Held.** Empty set in `apps/lazuar-pay`. IsolationTests bans `Modules.One`. |
| First-slice whoami is Mode U (forward user access_token) | **Held.** Whoami + MemberGate + merchant picker. |
| Slice step 5 still **mints** a scoped key (dogfood) | **Not done.** No mint helper, no curl in Pay README, no test. `NP-ONE-014` still `todo`. |
| Mode M later: `ONE_API_KEY=lzr_sk_…` for workers, never whoami fallback | **Not done.** No env, no worker. Fail-closed (no fallback) is accidentally correct. |
| Merchants mint Family A in One; Pay may later accept those Bearers on `/v1` by introspecting `GET /me` | **Forwarding exists; productization does not.** MemberGate `authz/check` 400/403s keys. |
| MemberGate must branch JWT vs key (`user_id` required on One for keys) | **013/08 named it. 018/002 did not build it.** Body still omits `user_id`; OrgReady **locks the omission**. |
| Empty scopes 400; never `*` / `admin` on Pay’s worker | One still 400s `[]`. Pay never mints, so the helper-refuse tests do not exist. |
| Never homemade `sk_test_` / `sk_live_` in new Pay | **Held** on the focused host. Museum remains in `apps/lazuar-api`. |
| Family B ≠ Family A | **Held.** Vault PUT vs One client. |
| `api_key.revoked` later if Pay caches | **N/A.** No cache. Inbound HMAC now per-org (002) but does not handle key revoke. |
| Pay holds one `lzr_sk_` in env, one tenant, not a god-key | **Held by absence.** Multi-merchant today is Mode U per request. |
| Never Zitadel PAT / FGA admin | **Held.** |
| Tests in 012 §12.2 (mint, Mode U vs M, Family C strings 400, no fallback, `authz:check` matrix) | **None of those exist under `Lazuar.Pay.Tests`.** Fake One 200 + `Bearer tok` is the entire matrix. |
| `o13-lzr-sk.md` later ticked “Fake One 200 on `Bearer lzr_sk_…`” | **Tick is false.** No such string in host tests. |

018 merchant-shell and 002 host-bugs made the **hosted cashier** production-shaped (vault, Test rail, occupancy, CORS, per-org HMAC, spec honesty). They did not open the **kernel door**. 019 already said that. Live files on `6d730d15` still say that, with a sharper MemberGate trace than 019 wrote.

`NP-API-004` (“Merchant ops is a client of `/v1` (One user JWT or `lzr_sk_`)”) — human half is live; `lzr_sk_` half is `todo`. `NP-SOON-007` (“M2M checkout for a second of *your* apps”) is `todo`. Do not flip those cells from this paper.

---

## 10. How to solve (analysis, not an order)

### 10.1 Hatch (preferred): another app mints via One; Pay documents and **actually** accepts `lzr_sk_` as Bearer

Product sentence: “Mint a One API key on the merchant’s workspace. Send `Authorization: Bearer lzr_sk_…` to Pay `/v1`. Pay replays that Bearer to One. If One says the key is valid and bound to the path `{orgId}`, Pay treats the caller as a **machine of that org**. Pay does not mint a second prefix.”

This is 012’s Family A presented to Pay, with an explicit MemberGate branch that 012/013 already required and 018/002 skipped.

**What the second app puts in `Authorization`:** `Bearer lzr_sk_` + base64url(32 random bytes), the secret shown **once** at One create. Same header shape as the staff JWT. Not `X-Api-Key`. Not Hub `sk_live_`. Not Stripe `sk_live_`. Not `whsec_`. Not the key **id** GUID.

**Mint path (human, once per merchant / per second app):**

```bash
# ACCESS_TOKEN = owner|admin JWT for that One tenant (Pay org_id)
curl -sS -X POST "$ONE/tenants/$TENANT_ID/api-keys" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"second-app-cashier","scopes":["tenant:read","authz:check"]}'
# copy secret once; never git; never VITE_*
```

UI equivalent: lazuar-app Settings → API keys. Pay merchant **must not** grow Hub `ApiKeysPage` (`payments.checkouts:*` / LHDN). A later Pay chrome that **calls One** with the user JWT is allowed; the store stays One.

**Scopes Pay needs on that key, and why they are not Pay product scopes:**

| Scope | Why |
|-------|-----|
| `authz:check` | Required for One `POST …/authz/check` **if** Pay keeps using that door for keys. Without it, live One 403s. |
| `tenant:read` | Honest least privilege that can `GET /tenants/{id}`; whoami works without it. Send explicitly; do not omit. |
| not `[]` | 400. Historical full-admin footgun. |
| not `*` / `admin` | Full tenant admin-equivalent (invite, mint more keys, …). Too much for a cashier. |
| not `payments.checkouts:write` | Unknown on One → 400. Family C. Pay must not wait for One to add money scopes. |

**MemberGate branch (the actual missing code, described not implemented):**

Do **not** send a key to `authz/check` with omitted `user_id` (400). Do **not** send the key id as `user_id` (400). Do **not** require the second app to know Ada’s Zitadel sub (impersonation). Do **not** use `/me.tenants[].role` as writer for keys (`member` unless `admin`/`*`).

A coherent hatch:

1. If the Bearer remainder starts with `lzr_sk_`, Pay calls One `GET /me` (already the whoami path). Fail closed on One 401/403/503.
2. Bound tenant: `tenants` length 1 (or find `id == path orgId`). Mismatch → 403. Empty tenants (deleted) → 403.
3. `status` must be `active` (same suspend fail-closed as writer overlay).
4. Treat a valid bound key as **member and writer of that one org** for Pay money doors. The minting human already bound the key to one tenant; that is the authorization. One scopes gate **One APIs**, not Pay APIs. There is no `payments.checkouts:write` in One’s catalog to reuse.
5. Optional later: require One `authz:check` on the key as a **signal of intent** — but Pay **cannot** read that from `/me.permissions` (ROLE-03 drop). Reading it means either calling a One route that 403s without the scope, or trusting mint-time documentation. Prefer (4) plus docs: “mint with `tenant:read` + `authz:check` because Pay may call `authz/check` for *humans*; machines are bound-tenant.”
6. JWT path unchanged: `authz/check` omit `user_id`, then `/me` role overlay for writer. Never fall back from missing JWT to `ONE_API_KEY` or to a merchant key in env.

`GET /me` for keys does not JIT-write. Do not hammer it from a hot loop (NP-ONE-006). Cache later, invalidate on `api_key.revoked` HMAC (`key_id`, not prefix uniqueness).

**Pay worker `ONE_API_KEY` (Mode M) is a different hatch.** That secret is Pay-the-process calling One for **one** dogfood tenant (jobs, health, maybe webhook register). It is **not** the credential a second app sends to Pay. Multi-merchant Pay must not put merchant B’s key in Pay `.env`. No god-key.

**OIDC `client_credentials` / One `OidcAppType.m2m`** is a third family (Zitadel app secret for the integrator’s **own** APIs). TypeSpec says it is not a One `lzr_sk_`. Do not tell the second app to send a Zitadel PAT to Pay.

### 10.2 Refuse: Pay-minted keys (second table, second prefix)

A Pay table of hashed `pay_sk_` / revived `sk_live_` would:

- Reintroduce Family C and the Stripe prefix collision, or invent a third prefix merchants must learn next to `lzr_sk_`.
- Duplicate revoke, listing, secret-once, rate limit — already built in One.
- Tempt IsolationTests bans (`Modules.One`, org/user tables). A `pay_api_keys` table is still a second IdP.
- Split “Ada revoked the key in lazuar-app” from “Pay still accepts it.”
- Require Pay to hold a pepper. `NP-ONE-020` is the opposite.

Steal Hub judgment (shown once, hashed, scoped) **into One**, which already did it. Pay introspects.

The only Pay-issued secrets that stay Pay-issued are Family B (processor) and Family E (HMAC receiver). Those are not caller identity.

### 10.3 Sequence (when someone implements; this paper does not)

1. **Document the hatch** on Pay README + `.env.example`: second apps mint on One; send `lzr_sk_` as Bearer; never `sk_*`, never PAT, never `VITE_*`. Merchant SPA still JWT-only.
2. **MemberGate branch** as §10.1. JWT path golden. No env fallback.
3. **Hermetic Fake One matrix** for `lzr_sk_` (section 10.4) **before** live One, so Pay’s mapping is locked even when One is down in CI.
4. **One live dogfood** (local, not CI if Pay cannot reference One’s factory): mint explicit scopes, whoami 200, checkout 201 on bound tenant, 403 on tenant B, revoke then 401.
5. **Mode M `ONE_API_KEY`** only when a Pay **worker** must call One without a user (not required for second-app cashier). Prefix-check `lzr_sk_` before any outbound. Refuse `sk_live_`, JWTs, PATs in that env slot.
6. **`api_key.revoked`** on the existing Plane A HMAC door when/if Pay caches. Not a blocker for uncached introspection.
7. **Sample** (program 09): a tiny Node/Go cashier that is not `:5178`, holds `lzr_sk_`, `POST /v1/checkouts`, verifies a future Pay outbound webhook. Out of this slice’s implementation; this slice names it as the dogfood that proves the door.
8. Still refuse: Pay key console with LHDN scopes, god-key in `.env`, Zitadel PAT, wrapping merchant keys in `SecretBox`.

### 10.4 Tests that would lock it

Hermetic (Fake One; Pay owns these):

1. `Whoami_forwards_machine_key_shape`: inbound `Authorization: Bearer lzr_sk_testfixture` → outbound same on `GET /me` → Fake 200 key-shaped `/me` (`user_id` GUID, `role: member`, one tenant) → Pay 200. **This is the o13 tick that is currently false.**
2. `Whoami_without_bearer_still_skips_one` (already exists).
3. `Member_gate_key_without_user_id_maps_one_400`: Fake One 400 `{"detail":"user_id is required when authenticating with an API key."}` → Pay 400 with that detail **until** the branch in §10.1 lands; **after** the branch, this fixture is replaced by “key `/me` bound tenant == path → 200 ready.”
4. After branch: `Key_bound_to_other_tenant_is_403` (`/me.tenants[0].id != path`).
5. After branch: `Key_member_role_can_create_checkout` — **must pass** if machines are writers despite synthetic `member`. If the product instead requires `admin`/`*` for writer, that is a **refuse** of least privilege; write it down. This paper recommends pass.
6. After branch: `Jwt_member_still_cannot_create_checkout` — human overlay must not regress.
7. `Missing_jwt_does_not_fall_back_to_env_ONE_API_KEY` even if the env is set in the factory.
8. `Vault_PUT_stripe_sk_live_does_not_call_One_POST_api-keys` and does not write `ONE_API_KEY`.
9. Isolation: still no `sk_test_` mint, no `Modules.One`, no `lzr_sk_` hasher, no pepper config.
10. Merchant unit: `pickApiBearerToken` still rejects opaque / `lzr_sk_`-shaped strings (SPA must not start sending keys).

Live / recorded against One (Pay must not import One’s test factory if that is a project reference into sibling; use HTTP):

11. User JWT `POST …/api-keys` `scopes: ["tenant:read","authz:check"]` → 201, `secret` starts with `lzr_sk_`, list has no secret.
12. Same with `scopes: []` → 400. Pay mint helper (if any) must not retry with `*`.
13. Same with `scopes: ["payments.checkouts:write"]` → 400 unknown. Family C dead.
14. `GET /me` as that secret → 200, `user_id` is key id, `is_platform_admin` false.
15. `POST /v1/checkouts` as that secret on bound org → **201 after hatch**; **400/403 today** (lock today’s failure until hatch, then flip the expected status in the same test name so the diff is honest).
16. Same secret on org B → 403.
17. `DELETE` key, then Pay whoami with old secret → 401.
18. Worker env `ONE_API_KEY=sk_live_…` → Pay fails closed **before** One (wrong family), when Mode M exists.

Do not write: hasher tests against One’s pepper; SQL against One `api_keys`; a test that empty scopes mean admin; a test that `id_token` is 200.

---

## 11. Ranked holes this slice

Classification: **bug** = live code disagrees with a lock Pay already claimed, or fail-open vs standing law. **missing feat** = kernel door not built; hosted cashier can still dogfood with a human JWT. **refuse** = would make Pay an IdP / mix families / god-key. **stale tick** = checklist green, tests red or absent (not a 011 flip).

| Rank | Kind | Hole | Evidence |
|------|------|------|----------|
| 1 | **missing feat** (kernel) | Second app cannot call Pay `/v1` money doors with One `lzr_sk_`. MemberGate `authz/check` omits `user_id` → live One 400/403. Writer overlay would then demand `owner\|admin` which typical keys do not project. | `OneClient.CheckMemberAsync` body; `AuthzEndpoints.RejectApiKeyAuthzSubject`; `GetMeForApiKey` role `member`; grep empty `lzr_sk_` under `apps/lazuar-pay`. |
| 2 | **stale tick** | `o13-lzr-sk.md` claims Fake One 200 on `Bearer lzr_sk_…`. No such test. `NP-ONE-014` / `NP-API-004` / `NP-SOON-007` remain `todo` on 011/11 — those cells are honest; O13 is not. | WhoamiTests `"Bearer tok"`; O13.3 `[x]`. |
| 3 | **missing feat** | No Mode M. Pay holds no one-tenant `lzr_sk_` for workers. `NP-ONE-020` half-true (no PAT; also no key). Fine until a job must call One without a user. | `OneOptions` two fields; `.env.example`. |
| 4 | **missing feat** | No docs/sample for “send `lzr_sk_` to Pay.” README live whoami is JWT-only. `pay-spec` has no machine-key sentence. | README curl `$ACCESS_TOKEN`; pay-spec grep empty. |
| 5 | **bug** (mapping, not authz) | Pay 403 map swallows One’s `API key lacks required scope authz:check.` into `"Not a member of this org"`. Fail closed is correct; the detail is a lie. Same swallow for SCIM 403. | `MemberGate` 403 arm + `SuspendedDetail`. |
| 6 | **missing feat** (precheck) | Wrong-family Bearer (`sk_live_`, PAT, `id_token`) is forwarded to One instead of rejected at Pay. 401 still, at One. 012 wanted Pay prefix-check so Stripe BYOK cannot ride Authorization. | `Bearer.TryGet` no family test. |
| 7 | **stale / incomplete analysis** | 019/07 implied writer overlay would run for keys. It does not, because hop 1 fails. Implementers copying 019 would “fix” `/me.role` and still 400. | this paper §4 vs 019 §15. |
| 8 | **refuse** | Pay-minted `sk_*` / second API-key table / `payments.checkouts:write` on One keys / god-key in Pay `.env` / Zitadel PAT / wrap merchant `lzr_sk_` in `SecretBox` / `VITE_*` machine key. | IsolationTests; 012 §3; standing law. |
| 9 | **not a hole** (keep) | Pay does not mint Family C. Vault Family B is separate. HMAC Family E is separate. WrapKey is wrap. Missing Bearer 401 skips One. No env fallback on interactive routes. Merchant SPA rejects non-JWT. Buyers have no Bearer. | live files cited above. |
| 10 | **not this slice** | Outbound `payment.completed` (paper 03). Plane A/B ops (paper 04). Host WrapKey production (paper 06). Spec/sample (paper 09). | 020 index. |

**Bug vs missing feat vs refuse, in one paragraph:** The hosted cashier is not wrong to use staff JWTs. The kernel claim “another app can integrate without cloning this repo” is **missing**. The only live **bug** in this slice is honesty of mapping/ticks (O13 green, 403 detail lie, 019’s hop-2 implication), not a fail-open that charges the wrong org. Cross-tenant keys still 403. Revoked keys still 401. The **refuse** list is the failure mode of “solving” the missing feat by rebuilding Hub K1 inside Pay.

---

## 12. What a second app should put in `Authorization` — cheat sheet

**Today (live `6d730d15`), if they want a 2xx on a money door:**

```http
Authorization: Bearer <Zitadel access_token JWT with jti>
```

Same as Ada’s browser. They must run a human OIDC session (or steal Ada’s token — do not). There is no supported machine path.

**Today, if they put One’s machine key:**

```http
Authorization: Bearer lzr_sk_<base64url-32-bytes>
```

- `GET /v1/whoami` → **200** (live One `/me` as key).
- `GET /v1/orgs/{orgId}/ready` and every MemberGate door → **400** (key has `authz:check`) or **403** (key lacks it, detail swallowed).
- `POST /v1/checkouts` → same, never hop 2.

**Today, if they put Hub or Stripe `sk_live_`:**

Pay forwards; One JWT 401; Pay 401. Wrong family.

**Today, if they put `Pay:OneWebhookSecret` / `whsec_`:**

Not a Bearer door. `POST /v1/one/webhooks` wants HMAC headers. Using it as Bearer 401s at One.

**After the hatch in §10.1 (not built):**

```http
Authorization: Bearer lzr_sk_<secret shown once at One POST /tenants/{id}/api-keys>
```

Minted by an owner/admin of **that** merchant’s One tenant, scopes explicit `["tenant:read","authz:check"]`, secret in the second app’s server env (not Vite). Path `{orgId}` = that tenant. One key, one tenant, no god-key in Pay.

---

## 13. Multi-merchant: no god-key in Pay `.env`

012 §7.4 still binds:

- One key = one workspace. It cannot see tenant B (`RequireApiKeyTenantAsync` 403).
- Pay process env may later hold **one** `ONE_API_KEY` for Pay’s dogfood / product tenant, for **Pay→One** jobs.
- N merchants ⇒ N keys, held by those merchants (or their apps), presented per request, introspected via One. Pay does not become a vault of N One secrets.
- If a job must call One for tenant B without a user: prefer the HMAC envelope’s `tenant_id`, or fail. Do not hold a PAT. Do not store tenant B’s `lzr_sk_` “just in case.”

002’s per-org One webhook ciphertext is the correct **inbound** scaling pattern (one HMAC per shop, wrapped). M2M **outbound-from-app** scaling is the same shape: one `lzr_sk_` per shop, **not** stored in Pay.

---

## 14. Residual honesty

| Residual | Where |
|----------|--------|
| One `/me.permissions` drops `tenant:read` and `authz:check` | `TenantPermissions` vs API-key catalog. Pay must not use permissions chrome as scopes. |
| JWT any member can list One keys (no `keys:read`) | Comment on `ApiKeyEndpoints.ListKeys`. One’s leak, not Pay’s to copy. |
| `o13` ticked | False vs tests. |
| 012 One SHA ≠ this paper’s One SHA | Mint contract still matches; always re-read One. |
| Domain `ApiKey.KeyHash` comment says “SHA-256 hex of peppered secret”; hasher is HMAC | Trust `ApiKeyHasher`. |
| Pay README still the best live dogfood, and it is JWT-only | Honest for cashier; silent on kernel. |
| Fake One 200 `allowed: true` will green a key fixture if someone only changes the inbound header | Do not add that test without teaching Fake One the 400. |
| Hub `apps/lazuar-api` still mints `sk_*` | Museum. IsolationTests is the wall. Do not add a project reference. |

None of these are reasons to mint Family C again.

---

## 15. Verdict

Pay on `6d730d15` is a **hosted cashier** whose merchant `/v1` doors proxy a **human** Bearer to One. One on `6b78e9d4` already mints, lists, revokes, hashes, and authenticates `lzr_sk_`. Pay does not mint `sk_*`. Pay does not hold `ONE_API_KEY`. Pay will **forward** a machine key if a caller sends one; live One will **200** whoami and **400/403** every MemberGate door because Pay’s `authz/check` body is JWT-shaped and Pay’s writer overlay is a human role list.

A second app, today, puts a **user access_token** in `Authorization` or it does not get a checkout. The hatch is: mint on One, send `lzr_sk_` as Bearer, teach MemberGate that a bound key is a machine of that one tenant — not a second Pay key table, not a Stripe-shaped `sk_live_`, not a god-key in `.env`, not a wrap-key, not a webhook HMAC.

This paper is analysis. It does not flip 011. It does not implement the hatch.
