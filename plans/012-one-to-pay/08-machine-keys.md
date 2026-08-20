# 08 — Machine keys (`lzr_sk_`)

**Date:** 20 August 2026  
**Type:** Uncondensed analysis. **Not** an implementation order. **Not** a commit to mint keys, wire middleware, or subscribe to webhooks in this tree.  
**Program:** 012 — One → Pay (Consumer-0). Companion of [011-new-lazuar-pay/02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) §Machines and apps / §Secrets.  
**Tracker IDs this paper owns the reading of:** `NP-ONE-014`, `NP-ONE-020`, `NP-XX-017`, `NP-XX-021` (plus the adjacent rows `NP-ONE-015`, `NP-ONE-017` `api_key.revoked`, `NP-API-004`, `NP-GW-001` / `NP-GW-009` so the families are not mixed).

**SHAs at time of writing (clean working trees):**

| Repo | Path | Branch | SHA | Subject |
|------|------|--------|-----|---------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-one-to-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `feat(pay): add TypeSpec package for the focused Pay host` (AuthorDate 2026-08-20 21:00:06 +0800) |
| One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `WIP: Thu Aug 20 21:24:22 +08 2026` (AuthorDate 2026-08-20 21:24:22 +0800) |

One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages (`NP-XX-021`: do **not** block Pay on npm publish). First-party Pay may import the workspace client; strangers use OpenAPI + `examples/`.

---

## Honesty (read this before copying env names)

1. **One already mints, lists, revokes, hashes, and authenticates `lzr_sk_` keys.** Pay does not reimplement that table. Pay does not copy `ApiKeys:Pepper`. Pay does not `SELECT` from One’s `api_keys`.
2. **A key is bound to one One tenant.** It is not a platform Management credential. It cannot become `is_platform_admin`. It cannot `POST /tenants`. “Pay holds one `lzr_sk_` in env” is true **and** that one secret only speaks for **one** workspace. Multi-merchant work uses the **user JWT** (per request) or a **per-merchant** key the merchant minted, not a god-key in Pay’s `.env`.
3. **Old Pay minted homemade `sk_test_` / `sk_live_` keys** (`GenerateApiCredentialCommand`, `one.ApiCredentials` / `lhdn.DeveloperApiKeys`) with product scopes like `payments.checkouts:write`. New Pay **does not** keep that mint. Merchants mint via One. The prefix collision with Stripe secrets is why we leave `sk_*` behind.
4. **Stripe / CHIP / Billplz BYOK keys are a different object** (`NP-GW-001`, S1). They live encrypted in Pay. They are never `Authorization: Bearer` to One.
5. **Docs in One are one sentence stale.** `apps/lazuar-docs/docs/integrations/api-keys.md` still says webhook lifecycle events are “**planned**”. Runtime already publishes `api_key.created` and `api_key.revoked` to the outbox (`ApiKeyService` + `WebhookEventCatalog`). The webhook **guide** (`integrations/webhooks.md`) lists both types. Trust the producer tests, not the “planned” line.
6. **Empty scopes used to mean full admin (P12 / D68).** They do **not** anymore. Explicit `[]` on create is **400**. Stored empty scopes deny privileged ops. `admin` and `*` still short-circuit. Pay must never request `*` or omit scopes and hope.
7. **First slice does not need the env key to call `GET /me` as a human.** The signed-in merchant already has an OIDC **access_token**. Machine key is for jobs / webhooks / M2M **later**. Slice step 5 still **mints** a scoped key (dogfood: prove the path) and uses `authz/check` `member` — that check can run with the **user JWT**. Putting `ONE_API_KEY` in env can wait until a worker exists.

This paper is analysis. No code lands in Pay from it.

---

## 1. Assigned slice (what 012 asked)

| Item | Verdict in One today |
|------|----------------------|
| `POST /tenants/{id}/api-keys` | Implemented. 201 + `secret` once. Admin/owner JWT (or `admin`/`*` key). Rate-limited. |
| `GET /tenants/{id}/api-keys` | Implemented. Metadata only. JWT any member; a **key** needs `keys:read`. |
| `DELETE /tenants/{id}/api-keys/{keyId}` | Implemented. 204. Idempotent if already revoked. Admin/owner (or `admin`/`*` key). Cross-tenant / missing → **403**, not 404 (D06). |
| Explicit scopes | Required for privilege. Omitted → `["tenant:read"]`. Empty array → 400. Unknown string → 400. |
| Empty / `*` | Empty is a footgun (now rejected). `*` is full-tenant admin-equivalent. Pay does not send either. |
| Pay holds one `lzr_sk_` in env | Yes, **later**, as `ONE_API_KEY`. Bound to **one** tenant. Not for first-slice whoami. |
| Examples in One repo | `examples/node-api-key/`, recipe R2, Postman, `@lazuar/one-cli whoami`, `@lazuar/one-client` `apiKeys.create/list`. |
| Merchant Stripe BYOK vs One API keys | Different families. Table in §3. |

Runtime base path is **`/api/v1`**. TypeSpec and OpenAPI are relative to that server (`http://localhost:8080/api/v1`). Full local URLs:

```text
POST   http://localhost:8080/api/v1/tenants/{tenantId}/api-keys
GET    http://localhost:8080/api/v1/tenants/{tenantId}/api-keys?page=&page_size=
DELETE http://localhost:8080/api/v1/tenants/{tenantId}/api-keys/{keyId}
```

TypeSpec: `packages/api-spec/modules/api-keys/{models,routes}.tsp`.  
Handlers: `apps/lazuar-api/src/Lazuar.One.Api/Features/ApiKeys/ApiKeyEndpoints.cs`.  
Mint/hash/revoke: `ApiKeyService.cs`.  
Bearer `lzr_sk_` scheme: `Infrastructure/Auth/ApiKeyAuthenticationHandler.cs`.

`@lazuar/one-client` wraps **create** and **list** only. Revoke is a raw `DELETE`. Pay can call either; do not wait for a client method (`NP-XX-021`).

---

## 2. Tracker rows this paper is the reading of

From [011-new-lazuar-pay/11-checklist.md](../011-new-lazuar-pay/11-checklist.md) and [12-first-slice-tracker.md](../011-new-lazuar-pay/12-first-slice-tracker.md). Status cells stay `todo` / `refuse` until new Pay actually has the job. The old C# tree does **not** count as `done`.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes from 011 |
|----|---------|------|-------|---------|--------|----------------|
| **NP-ONE-014** | Mint / list / revoke `lzr_sk_` with **explicit** scopes | S0 | both | Y | todo | No `*` / empty scopes |
| **NP-ONE-020** | Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC | S0 | Pay | — | todo | Never Zitadel PAT / FGA admin / masterkey |
| **NP-XX-017** | Pay holds Zitadel PAT, login PAT, or OpenFGA admin token | refuse | Pay | — | refuse | |
| **NP-XX-021** | Block Pay on npm publish of `@lazuar/one-client` | refuse | Pay | — | refuse | Workspace import is enough |

Adjacent rows that this paper must not steal, but must not confuse with keys:

| ID | Why it sits next to keys |
|----|--------------------------|
| NP-ONE-003 | Send **access_token** as Bearer (never `id_token`). The other Bearer. |
| NP-ONE-006 | `GET /me` — works with JWT **or** `lzr_sk_`; first slice uses JWT. |
| NP-ONE-015 | `authz/check` `member` / `admin` / `owner` before merchant admin routes. Keys that call this need `authz:check`. A key’s own `user_id` is the **key id** and is **rejected** as the check subject (400). |
| NP-ONE-017 | HMAC webhooks including `api_key.revoked`. First slice subscribes to `member.*` and `tenant.suspended`. Key-revoke cache drop is **later**. |
| NP-ONE-021 | VIEWER cannot charge, change **gateway** keys, or refund. Those “keys” are BYOK, not `lzr_sk_`. |
| NP-GW-001 / NP-GW-009 | Encrypted BYOK per workspace; paste/rotate in ops. S1 money. |
| NP-API-004 | Merchant ops is a client of Pay `/v1` (One user JWT or `lzr_sk_`). S1 door. Not a second mint in Pay. |
| NP-SOON-007 | M2M checkout for a second of *your* apps (same `/v1`). First extra consumer. After S1 is boring. |
| NP-XX-007 | Zitadel / OpenFGA / SCIM / password store **inside Pay** — refuse. |

Slice step 5 ([12](../011-new-lazuar-pay/12-first-slice-tracker.md)): “Mint a scoped `lzr_sk_`; `authz/check` `member` before merchant admin routes” → `NP-ONE-014`, `NP-ONE-015`. Explicit scopes.

---

## 3. Three (plus) credential families — do not mix

New Pay will fail the dogfood sentence the moment a Stripe secret, a Zitadel PAT, or an old Hub `sk_live_` is sent to One as Bearer, or a `lzr_sk_` is stuffed into the Stripe adapter.

### 3.1 Family A — One product API keys (`lzr_sk_`)

| Field | Value |
|-------|--------|
| Who mints | One (`POST /tenants/{id}/api-keys`), UI in **lazuar-app** Settings → API keys (also listed as Settings → Integrations → API keys in recipes). |
| Who holds the secret | The minter, **once**. Pay’s worker holds **one** of these in env (`ONE_API_KEY`). A merchant’s CI holds **theirs**. |
| Prefix | `lzr_sk_` (constant `ApiKeyDefaults.KeyPrefix`). |
| What it authorizes | Calls to **One** `/api/v1` as a machine of **that** workspace. Coarse catalog scopes. |
| Lookup | HMAC-SHA256(pepper, full secret) → `api_keys.key_hash`. Prefix is **not** the lookup key. |
| `GET /me.user_id` | The key’s GUID. Not a Zitadel human. |
| `GET /me.is_platform_admin` | Always `false`. |
| Bound tenants | 0–1. Always the row’s `tenant_id`. |
| Pay first-slice use | Mint (prove path). Do not require it for whoami. Use later for jobs. |
| Pay later use | Worker → One (`tenant:read`, `authz:check`, maybe `events:read` / `webhooks:*`). Merchant M2M into **Pay** `/v1` by **presenting** this same Bearer; Pay **introspects** via One `GET /me` (Pay does not hash it). |

This is the only machine credential this paper is about, except where it is named to **exclude** the others.

### 3.2 Family B — merchant Stripe / CHIP / Billplz BYOK (Pay S1)

| Field | Value |
|-------|--------|
| Tracker | `NP-GW-001` encrypted BYOK per workspace; `NP-GW-002` Stripe; `NP-GW-003` one MY rail; `NP-GW-009` paste/rotate in ops. |
| Who pastes | Merchant admin in **Pay** ops (VIEWER cannot — `NP-ONE-021`). |
| Prefixes | Stripe: `sk_test_` / `sk_live_` (same prefixes old Pay used for **Family C**). CHIP / Billplz: vendor-specific, not `lzr_sk_`. |
| Where stored | Pay database, encrypted at rest. Not One `api_keys`. |
| Where used | Pay → Stripe/CHIP/Billplz APIs to charge. Gateway webhook signing secrets (`whsec_` for Stripe) stay in the same vault family. |
| Authorization header to One | **Never.** |
| Authorization header to Pay `/v1` | **Never** as the merchant’s machine identity. A Stripe secret that 401s One or Pay is a paste error. Old Hub even documented: “This looks like a Stripe secret. Mint a Lazuar Pay key.” That sentence referred to Family C. New Pay’s sentence is: “This looks like a Stripe secret. Paste it in Payment settings, not in One API keys, and not as `ONE_API_KEY`.” |

Dogfood ([01-product.md](../011-new-lazuar-pay/01-product.md)): “a merchant signs in through One, opens Pay, **pastes CHIP or Stripe keys**, a buyer pays…”. Those pasted keys are Family B.

### 3.3 Family C — old Pay homemade integrator keys (`sk_test_` / `sk_live_`) — do not rebuild

Old Hub minted **K1** as `sk_test_` / `sk_live_` (`GenerateApiCredentialCommand`). Hash: **plain SHA-256** of the secret (`TokenGeneratorService.HashToken`) — **no pepper**. Stored on `lhdn.DeveloperApiKeys` then `one.ApiCredentials`. Middleware `ApiKeyAuthenticationMiddleware` cached `ApiKey_{keyHash}` for 5 minutes. Scopes (space-separated) from `PlatformApiScopes`:

- `lhdn.documents:read` / `lhdn.documents:write`
- `payments.checkouts:read` / `payments.checkouts:write`
- `webhooks.endpoints:manage`
- `commerce.subscriptions:read` / `commerce.subscriptions:write`

Aura and `examples/hub-cashier-next` still speak `LAZUAR_SK_TEST_KEY` / `LAZUAR_API_KEY=sk_test_…`. That env name is **poison** for new Pay: it means Family C, not Family A.

Old docs (`docs/payments-integration-quickstart.md` §8.1) locked “Prefix decision = B”: keep minting `sk_test_` / `sk_live_` and tell Aura to `GET /me` rather than regex the prefix, because Stripe K2 uses the same prefixes. That decision is **old Pay**. New Pay’s prefix decision is: **One already chose `lzr_sk_`**. Do not mint a third `sk_*` product. Do not teach merchants a Pay-specific key that collides with Stripe again.

Cutover papers in this repo (`plans/004-maintenance/api-key-cutover-design.md`, `plans/005-remaining/01-api-key-one-only-cutover.md`) moved **old Pay’s** K1 store from Lhdn → Pay’s `Modules.One` table. That is **not** sibling lazuar-one’s `api_keys` table. Sibling One is a different database, different hasher (HMAC + pepper), different prefix, different scope catalog. Do not migrate `sk_live_` rows into One `lzr_sk_` rows. Remint.

### 3.4 Nearby tokens that are also not Family A

| Token | Prefix / shape | Holder | Pay |
|-------|----------------|--------|-----|
| User OIDC **access_token** | JWT (not `lzr_sk_`) | Browser session → Pay backend per request | **First-slice Bearer to One** |
| User **id_token** | JWT | Browser | Never send as Bearer (`NP-ONE-003`) |
| Zitadel **Management PAT** | Zitadel PAT string | One seed / provisioner | **Never in Pay** (`NP-XX-017`) |
| Login-client PAT | PAT | `lazuar-login` only (`apps/lazuar-login/.secrets/`) | Never |
| OpenFGA store admin | token in One `deploy/dev/openfga/.env.local` | One ops | Never |
| Zitadel masterkey / first-instance | One ops | One ops | Never |
| SCIM token | `lzr_scim_` | Tenant IT admin | Never. Wrong prefix → 401 on SCIM host; SCIM on product APIs → 403. |
| One OIDC **m2m** app secret | Zitadel client_secret | Integrator’s **own** APIs / `{issuer}/oauth/v2/token` `client_credentials` | Not a One `lzr_sk_`. TypeSpec says so on `OidcAppType.m2m`. Pay SPA is `spa`/`web`, not this, for S0. |
| One webhook receiver secret | `whsec_…` | Pay (shown once at endpoint create) | Family of **HMAC verify**, not Bearer. `NP-ONE-020` allows Pay to hold it. |
| One `ApiKeys:Pepper` | config | One API | Never in Pay. Rotating it unverifies every `lzr_sk_` **and** `lzr_scim_` (runbook `rotate-api-key-pepper.md`). |
| Pay OIDC `client_id` | public | Pay | Allowed (`NP-ONE-020`). |

---

## 4. What One actually ships (runtime, not slides)

### 4.1 HTTP contract

TypeSpec `CreateApiKeyRequest`:

| Field | Rule |
|-------|------|
| `name` | required, 1–200 chars (endpoint also 400s if whitespace-only). |
| `scopes` | optional array. **Omitted / null** → `["tenant:read"]` (`ApiKeyScopeHelper.DefaultCreateScopes`, D69). **Explicit `[]`** → 400 with detail that empty is no longer full-admin (P12). Whitespace-only entries are skipped; if nothing valid remains → 400. Unknown token → 400 `Unknown scope '{s}'. Allowed: …`. Deduped case-insensitive. |
| `expires_at` | optional UTC. Null/omitted = no expiry. Past timestamp → 400. |

`ApiKey` (list/get metadata): `id`, `tenant_id`, `name`, `prefix`, `scopes`, `created_by`, `created_at`, optional `expires_at`, `revoked_at`, `last_used_at`. **Never** `secret`, **never** `key_hash`.

`ApiKeyCreatedResponse`: all of the above plus `secret` (`lzr_sk_…`). **Only this response** contains the secret. List JSON is asserted in tests to not contain `"secret"` or the raw value. DB `key_hash` is not the secret.

Pagination on list: `page` (default 1), `page_size` (default 20, clamp 1–100 in service; UI asks 50). Envelope: `data`, `total_count`, `current_page`, `total_pages`.

Create → **201** + `Location` `/api/v1/tenants/{tenantId}/api-keys/{id}`.  
Revoke → **204**. Already revoked → 204 no-op. Wrong tenant or unknown id → **403** `"Not allowed to revoke this API key."` (existence not leaked).

Suspended / deleted tenant cannot create keys (403). `POST …/api-keys` is rate-limited: policy `create-api-key`, default **20 per 60s** (`RateLimit:CreateApiKeyPerWindow`), ProblemDetails **429**. Store defaults to in-process Memory; multi-instance needs Redis.

Auth on all three routes: `Authorization: Bearer` (TypeSpec `@useAuth(BearerAuth)` — one scheme covering JWT, `lzr_sk_`, and SCIM; runtime `PolicyScheme` splits by prefix).

Role gates (`ApiKeyEndpoints`):

| Verb | JWT | API key |
|------|-----|---------|
| POST create | membership `minRole: admin` (admin\|owner) | `admin` or `*` (else 403 “Admin scope required”) |
| GET list | any member | `keys:read` (or `admin`/`*` via `HasAnyScope`) |
| DELETE revoke | membership `minRole: admin` | `admin` or `*` |

JWT members can **list** keys without a scope catalog (documented residual). That is One’s leak, not Pay’s to copy.

### 4.2 Header format — `Authorization: Bearer lzr_sk_…`

Runtime selector (`AuthenticationExtensions.AddLazuarJwtAuthentication`):

1. Header missing / not `Bearer ` → JWT scheme (then 401).
2. Token starts with `lzr_scim_` → SCIM handler.
3. Token starts with `lzr_sk_` → API key handler.
4. Else → JWT Bearer (Zitadel). Opaque leftovers 401.

**Pay sends exactly:**

```http
Authorization: Bearer lzr_sk_<base64url-32-random-bytes>
Accept: application/json
```

Optional, never authorizing: `X-Lazuar-Tenant-Id`. For a key, `GET /me` **ignores** a foreign hint and stays on the bound tenant (`MeTests.Api_key_header_of_other_tenant_stays_bound_tenant`). Path `{tenantId}` is SoT (`NP-ONE-007`).

Do not send:

- `Bearer <id_token>`
- `Bearer <Zitadel PAT>`
- `Bearer sk_test_…` / `sk_live_…` (Family B or C — selector treats them as JWT → 401)
- `Bearer lzr_scim_…` on product `/api/v1` (403 “SCIM tokens cannot call product APIs.”)
- The key **id** (GUID). The example warns: if the env value does not start with `lzr_sk_`, you pasted the id, not the secret.

Challenge body on bad/revoked/expired key is ProblemDetails 401: `"Authentication is required or the API key is invalid."` Logs may say revoked vs expired vs unknown; the client does not get a distinct code. Prefix in logs is truncated to 16 chars (`SafePrefix`). Full token is never logged.

### 4.3 Secret format, prefix vs hash (Pay must not reimplement)

Generation (`ApiKeyService.GenerateSecret`):

- 32 random bytes (`RandomNumberGenerator.Fill`)
- Base64 URL: `+`→`-`, `/`→`_`, strip `=`
- Concatenate `lzr_sk_` + payload

Example shape (not a real key): `lzr_sk_` + ~43 chars ≈ 50-char secret.

**Prefix (public, stored, returned forever):** first **16 characters** of the secret (`secret[..16]`). That is `lzr_sk_` (7) + 9 chars of payload. Column `api_keys.key_prefix` `varchar(32)`. OpenAPI description: “Public prefix hint (e.g. `lzr_sk_abcd1234`) for support / UI.” Used in list UI, webhook payloads, audit logs. **Not unique. Not a lookup key. Not enough to authenticate.**

**Hash (secret, stored, never returned):**

```text
key_hash = lowercase_hex( HMAC-SHA256( key = UTF8(ApiKeys:Pepper), message = UTF8(full_secret) ) )
```

64 hex chars. Unique index `ix_api_keys_key_hash`. `ApiKeyHasherTests` lock: this is **not** `SHA256(pepper + secret)` concatenation; old Pay **was** `SHA256(secret)` with no pepper. Verifier uses `CryptographicOperations.FixedTimeEquals` on the hex strings.

Auth path: compute hash → `FirstOrDefault(k => k.KeyHash == hash)` → `Verify` again → reject if `RevokedAt != null` or `ExpiresAt <= now`. Then claims:

| Claim | Value |
|-------|--------|
| `sub` / `NameIdentifier` | key `Id` (GUID string) |
| `tenant_id` | bound tenant GUID |
| `auth_type` | `api_key` |
| `scope` (repeatable) | each stored scope |

`last_used_at` is best-effort `Task.Run` and must not fail the request.

Pepper: `ApiKeys:Pepper`. Empty allowed in Development/Testing; **required** in Production/Staging (`ApiKeysOptionsValidator`). Tests use `"test-pepper"`. Dual-pepper rotation is **not** in One MVP; rotate = treat every `lzr_sk_` and `lzr_scim_` as compromised.

**Pay consequences:**

- Pay **cannot** validate a merchant `lzr_sk_` locally. Pay has no pepper and no table.
- Introspection = HTTP to One with that Bearer (`GET /me` is enough to learn `user_id` = key id, `active_tenant_id`, projected permissions).
- Caching that introspection is optional later; the invalidation event is `api_key.revoked` (id + prefix, never secret).
- Do not store the One hash. Do not store the secret in Pay’s merchant table “for convenience” beyond the **one** worker env var. Merchant keys stay with the merchant.

### 4.4 Scope catalog (closed)

`ApiKeyScopeHelper.KnownScopes` (create validation uses the same set, case-insensitive):

| Scope | API key may | JWT still needs |
|-------|-------------|-----------------|
| `tenant:read` | `GET /tenants`, `GET /tenants/{id}` only | membership |
| `members:read` | `GET /tenants/{id}/members` only — **does not** list API keys | membership |
| `apps:read` | `GET …/apps`, `GET …/apps/{appId}` | membership |
| `keys:read` | `GET …/api-keys` (metadata; no secrets) | membership (JWT any member) |
| `authz:check` | `POST …/authz/check`, `/batch-check`, `/list-objects` | membership |
| `webhooks:read` | list/get endpoints and deliveries | **admin\|owner** for webhook routes (roles, not catalog) |
| `webhooks:write` | create/update/delete/rotate/test — **does not imply read** | admin\|owner |
| `events:read` | `GET /tenants/{id}/events` | membership + permission |
| `audit:read` | `GET /tenants/{id}/audit` | membership + permission |
| `admin` | all of the above **plus** `minRole: admin` writes (invite, apps mutate, keys create/revoke) | — |
| `*` | same as `admin` | — |

`HasAnyScope`: JWT principals **always pass** (membership remains SoT). Empty key scopes **deny** when any scope is required (P12). `admin` and `*` short-circuit, case-insensitive.

`GET /me` **does not require** `tenant:read`. Identity snapshot of the bound tenant. `active_tenant_id` is set without `X-Lazuar-Tenant-Id`.

Chrome quirk: `ProjectCatalogPermissions` maps key scopes through `TenantPermissions.IsKnown`, and that ROLE-03 catalog is `{ tenant:update, tenant:delete, domains:manage, roles:manage, events:read, audit:read, sso:manage, scim:manage, streams:manage }`. **`tenant:read` is not in that list.** So a key with only `tenant:read` returns `permissions: []` on `/me` and tenant list, even though the key can `GET /tenants/{id}`. A key with `events:read` + `tenant:read` projects **only** `events:read`. `admin`/`*` project the **admin** catalog (includes `events:read`, `sso:manage`, **not** `tenant:delete`). Pay must **not** treat `/me.tenants[].permissions` as the API-key scope list. Use `authz/check` and the known catalog. Do not parse Zitadel project-role claims (`NP-XX-024`).

lazuar-app UI chips (`WorkspaceApiKeysPage.tsx`) default to `['tenant:read']` and group Read / Act / Full access. **UI chips omit `events:read` and `audit:read`.** The API still accepts them. Pay minting from curl/client can pass them; the UI cannot click them until One adds chips. Do not block Pay on that.

### 4.5 What every API key is forbidden to do (including `admin` / `*`)

`TenantAccessService.RejectApiKey` → 403 `"This operation requires a user session, not an API key."`

Confirmed in `ApiKeyTests` and call sites:

- `POST /tenants` (create workspace) — even `admin`/`*`
- accept-invite
- leave
- transfer-ownership
- `GET /me/invites`
- domain routes, custom roles, SCIM token routes, audit-stream, SSO connection routes (enterprise — not Pay v1)

Keys also cannot:

- List other workspaces (`GET /tenants` returns the bound tenant only, 0–1). Isolation test: other tenant ids absent.
- `GET /tenants/{other}` → **403** (ISO / D06).
- Become platform admin (`PlatformAdminAuthorizer` returns false for API keys; `/me.is_platform_admin` false even with `admin` scope).
- Call `POST /platform/tenants` (`NP-XX-023`).
- Be used as `user_id` on `authz/check` — 400 `"user_id must be a user subject, not the API key id."` Omit `user_id` as a key → 400 `"user_id is required when authenticating with an API key."` Pass a **real member’s Zitadel sub**. Recipe R2 uses the minting user’s `/me.user_id`.

Limited-scope examples from tests (Pay should copy these as lock tests when it calls One):

| Key scopes | Call | Status |
|------------|------|--------|
| `tenant:read` | `GET …/members` | 403 (mentions `members:read` or scope) |
| `tenant:read` | `GET …/apps` | 403 |
| `tenant:read` | `GET …/api-keys` | 403 |
| `tenant:read` | `POST …/authz/check` | 403 (needs `authz:check`) — ISO-22 |
| `tenant:read` | `POST …/members/invite` | 403 |
| `members:read` | `GET …/api-keys` | 403 |
| `members:read` | `GET …/tenants/{id}` | 403 (needs `tenant:read`) |
| `keys:read` | `GET …/api-keys` | 200, no `"secret"` |
| `apps:read` | `GET …/apps` and get-by-id | 200 |
| `authz:check` | `POST …/api-keys` | 403 (not admin) |
| `admin` | members, apps, keys list | 200 |
| `admin` | `POST /tenants` | 403 |

Revoke: subsequent `GET /tenants/{id}` with the old secret is 401 or 403 (`ApiKeyTests.Revoke_key_subsequent_auth_fails`, `ISO-21`).

### 4.6 `GET /me` as a key (whoami)

Expected 200 (recipe R2 / `examples/node-api-key`):

```json
{
  "user_id": "<api-key-id>",
  "email": null,
  "name": null,
  "tenants": [
    {
      "id": "<bound-tenant-id>",
      "slug": "…",
      "name": "…",
      "role": "member",
      "status": "active"
    }
  ],
  "is_platform_admin": false,
  "active_tenant_id": "<bound-tenant-id>",
  "active_role": "member"
}
```

`role` is `"admin"` only when the key has `admin` or `*`; otherwise `"member"`. That role is a **projection**, not a membership row. The key is not in `memberships`. Do not use it as a human in invites.

JWT `/me` **can write** (domain auto-join, SSO JIT). Key `/me` does **not**. Still: do not hammer `/me` from a hot loop (`NP-ONE-006`).

### 4.7 Webhooks already produced (cache drop is later for Pay)

`WebhookEventCatalog`:

- `api_key.created`
- `api_key.revoked`

Producer (`ApiKeyService`): outbox payload is metadata only:

```json
{
  "key_id": "…",
  "name": "Pay production",
  "prefix": "lzr_sk_abcd1234",
  "scopes": ["tenant:read", "authz:check"],
  "created_by": "…",
  "revoked_at": "…"
}
```

`created_by` on create; `revoked_at` on revoke. **Never** `secret` or `key_hash`. Test `I_PRD_04_api_key_create_revoke_without_secret` asserts the secret is absent from both outbox rows and `key_id` is present.

Envelope (all One webhooks): `id`, `type`, `created_at`, `tenant_id`, `api_version` (`v1`), `data`. Headers: `X-Lazuar-Event-Id`, `X-Lazuar-Event-Type`, `X-Lazuar-Tenant-Id`, `X-Lazuar-Timestamp`, `X-Lazuar-Signature` (`v1=<hex HMAC-SHA256>`), `X-Lazuar-Delivery-Id`.

Pay subscribe filter later should include `api_key.revoked` if Pay caches introspection of **merchant** keys. First slice (`NP-ONE-017` dogfood) is `member.*` and `tenant.suspended`, not key revoke.

Audit log names match: `AuditLog.Events.ApiKeyCreated` / `ApiKeyRevoked`.

---

## 5. How to mint a key in One locally

Prerequisites (One README, e2e README, examples README):

| Service | Local URL |
|---------|-----------|
| API health | `http://localhost:8080/health` |
| API product | `http://localhost:8080/api/v1` |
| lazuar-app (customer product) | `http://localhost:5174` |
| Product login (universal) | `http://localhost:5175` |
| Stock Login V2 | `:3005` break-glass only — do not ship merchants here |
| lazuar-admin | `:5173` — **Pay never sends merchants here** (`NP-XX-018`) |

`011` product login is `:5175`. Workspace chrome is lazuar-app `:5174`. Pay’s own origin will be a **different** SPA registered via `POST /tenants/{id}/apps` (`NP-ONE-001`), not a Console click.

You must already be **owner or admin** of `tenantId`. MEMBER/VIEWER cannot mint (403). First-slice invite (step 4) is a different human; they do not mint Pay’s worker key unless they are admin.

### 5.1 Path A — UI (lazuar-app)

1. Sign in via `:5175`, land in lazuar-app `:5174`.
2. Open the workspace → **Settings → API keys** (Integrations subnav). Owner/admin only; others redirect `/forbidden`.
3. Create: name + scopes. Default chip `tenant:read`. Quick-add chips for the UI catalog (not the full API catalog — see §4.4). Client-side: empty chips → “Choose at least one permission.” Suspended workspace refuses create.
4. Secret modal: copy `secret` (`lzr_sk_…`) **once**. List afterwards shows `prefix`, name, scopes, timestamps — no secret.
5. Revoke from the same page (`DELETE`).

Staff dual-run exists on lazuar-admin. Pay does not use it.

### 5.2 Path B — curl with a user JWT (recipe R2)

Get `ACCESS_TOKEN` from the SPA (Network tab / storage). Must be the **access_token** JWT, not `id_token`, not opaque.

```bash
export API_BASE=http://localhost:8080/api/v1
export TENANT_ID='…'          # One tenant id = Pay org_id (NP-ONE-009)
export ACCESS_TOKEN='…'       # user JWT, admin|owner

curl -sS -X POST "$API_BASE/tenants/$TENANT_ID/api-keys" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"name":"pay-worker","scopes":["authz:check","tenant:read"]}'
```

Copy `secret`. Then:

```bash
export API_KEY='lzr_sk_…'     # never commit
# Pay’s name for the same value in *Pay* env is ONE_API_KEY — see §7.

curl -sS "$API_BASE/me" \
  -H "Authorization: Bearer $API_KEY" \
  -H "Accept: application/json"
```

Omitted body scopes (name only) → stored `["tenant:read"]`. Explicit `"scopes":[]` → 400.

List / revoke still use the **user JWT** (or an `admin`/`*` key):

```bash
curl -sS "$API_BASE/tenants/$TENANT_ID/api-keys" \
  -H "Authorization: Bearer $ACCESS_TOKEN"

curl -i -X DELETE "$API_BASE/tenants/$TENANT_ID/api-keys/$KEY_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

Postman collection (`examples/postman/Lazuar-One-API.postman_collection.json`): same POST, example body `{"name":"postman-bot","scopes":["authz:check","tenant:read"]}`, description “Empty scopes → 400.” Env vars `accessToken` **or** `apiKey`; pre-request prefers accessToken.

### 5.3 Path C — `@lazuar/one-client` (workspace, unpublished)

```ts
import { createClient } from '@lazuar/one-client'

const client = createClient({
  baseUrl: 'http://localhost:8080/api/v1',
  getAccessToken: () => accessToken, // JWT to mint; later () => process.env.ONE_API_KEY
  getTenantId: () => tenantId,       // X-Lazuar-Tenant-Id hint only
})

const created = await client.apiKeys.create(tenantId, {
  name: 'pay-worker',
  scopes: ['authz:check', 'tenant:read'],
})
// created.secret shown once
const listed = await client.apiKeys.list(tenantId)
```

`getAccessToken` is just the Bearer source. It can return a JWT **or** a `lzr_sk_`. The README sentence “JWT access_token only — never id_token” is the **human** path. For machine mode, return `ONE_API_KEY`. Do not put the minting JWT in Pay’s long-lived env.

No `apiKeys.revoke` helper. `DELETE` with fetch.

Do not `npm i @lazuar/one-client` expecting npmjs.com (`NP-XX-021`). `file:` / workspace / tarball.

### 5.4 Path D — runnable sample `examples/node-api-key/`

Zero-install Node ≥ 18. Does **not** mint; it **uses** a key already minted.

`.env.example`:

```bash
LAZUAR_API_BASE=http://localhost:8080/api/v1
LAZUAR_API_KEY=lzr_sk_replace_me
```

`index.mjs` warns if the value does not start with `lzr_sk_`. Calls `GET {base}/me` with `Authorization: Bearer ${apiKey}`. On 200, prints that `user_id` is the key id.

Pay should not copy the env **name** `LAZUAR_API_KEY` into Pay (old Hub cashier sample uses that name for `sk_test_`). Copy the **protocol**.

CLI equivalent:

```bash
export LAZUAR_ONE_API_URL=http://localhost:8080/api/v1
export LAZUAR_ONE_API_KEY=lzr_sk_…
pnpm --filter @lazuar/one-cli exec lazuar-one whoami
pnpm --filter @lazuar/one-cli exec lazuar-one tenants   # needs tenant:read
```

Missing key exits `2`.

### 5.5 Path E — seed in One tests (not for Pay prod)

`SeedHelpers.SeedApiKeyAsync` mints with the test pepper, default scopes **`admin`** if omitted. Pay must not copy that default. Explicit scopes in every Pay fixture.

---

## 6. Scopes Pay should request

Binding from `011` §Machines: “Request **explicit** key scopes. Empty/`*` is a footgun. Prefer `tenant:read` plus the routes Pay actually hits.” One’s dogfood-then-serve §6.6 is the same sentence.

### 6.1 Whoami (`GET /me`) — first slice does not need a key

If Pay *does* call whoami with a machine key (worker health, later):

| Need | Scope |
|------|-------|
| `GET /me` | **none** of the catalog (any valid non-revoked key works) |
| Honest least privilege that can also list the bound workspace | `tenant:read` |

Do **not** mint a key with omitted scopes “because default is fine” in Pay automation. Always send an explicit array so a future One change cannot widen it. Do **not** send `[]`. Do **not** send `*`.

Recommended first mint (dogfood step 5, matches R2 / Postman / 011 example `"pay-worker"`):

```json
{
  "name": "pay-worker",
  "scopes": ["tenant:read", "authz:check"]
}
```

Why both:

- `tenant:read` — `GET /tenants`, `GET /tenants/{id}` as the worker. Whoami works without it; tenant profile does not.
- `authz:check` — `POST …/authz/check` `member` / `admin` / `owner` (`NP-ONE-015`). **Without this, ISO-22 is 403.** Pass `user_id` of the **human** on the Pay request (Zitadel sub from the forwarded JWT `/me`), never the key id.

That pair is enough for: worker whoami, bound-tenant read, permission checks. It cannot list members, apps, or keys; cannot invite; cannot mint more keys; cannot manage webhooks.

### 6.2 Later — tenant read (still not `*`)

When Pay’s worker must read workspace profile without a user session (billing-owner name, suspend status if webhook missed):

| Call | Scope |
|------|-------|
| `GET /tenants/{id}` | `tenant:read` |
| `GET /tenants` | `tenant:read` (returns 0–1) |
| Roster | `members:read` — only if a job truly lists humans without a JWT. Prefer user JWT for ops UI. |
| Events pull if no webhook | `events:read` |
| Audit pull | `audit:read` — probably never; Pay’s own audit is in Pay (`NP-AUD`, `NP-XX-019`) |

Do not add `members:read` “just in case.” Default / `tenant:read` **cannot** list members (locked in `ApiKeyTests.Tenant_read_cannot_list_members` and R2).

### 6.3 Later — jobs / webhooks (machine key earns its keep)

| Job | Scopes to **add**, still explicit |
|-----|----------------------------------|
| Register / rotate Pay’s receiver on **this** tenant | `webhooks:write` **and** `webhooks:read` (write does not imply read) |
| Pull `GET …/events` if push is down | `events:read` |
| Mint keys as a machine | `admin` or `*` — **refuse** unless a written Pay job cannot use a user JWT. First slice mints with the **human** admin JWT. |
| Invite / apps mutate as a machine | same refuse |

First-slice webhook subscribe (`NP-ONE-017`) happens at workspace-create time with the **creating user’s JWT**, not `ONE_API_KEY`. Chicken-and-egg: `tenant.created` fires on a tenant that does not yet have Pay’s endpoint; Pay should create the endpoint in the same user-JWT session that created the tenant (or immediately after `POST /tenants`).

### 6.4 What Pay must never put on the worker key

| Scope / shape | Why |
|---------------|-----|
| omitted `scopes` | Implicit default can be misread as “broad”. Send the array. |
| `[]` | 400 today; historically full admin. Footgun even if One stays strict. |
| `*` | Full tenant admin-equivalent. 011: “Pay’s key should not be `*`.” |
| `admin` | Same blast radius as `*` for minRole writes. Not needed for whoami or `authz/check`. |
| One catalog + old Pay `payments.checkouts:write` | Unknown scope → 400. Those strings are Family C. |
| A second key per merchant stored in Pay env | Env holds **one** worker key for **one** tenant. Merchant keys stay with merchants. |

### 6.5 JWT vs key for the same route (Pay’s rule of thumb)

| Situation | Credential |
|-----------|------------|
| Browser merchant in Pay ops | Forward **user access_token**. Scopes N/A; membership + `authz/check`. |
| Pay worker, no user, **that** tenant | `ONE_API_KEY` with explicit scopes. |
| Pay worker, **other** tenant | **Cannot** use `ONE_API_KEY`. Use a webhook already subscribed, or fail. Do not hold a PAT to impersonate. |
| Merchant’s CI calling **Pay** `/v1` | Merchant’s own `lzr_sk_` (Family A) presented to Pay; Pay introspects One. **Later** (`NP-API-004`, `NP-SOON-007`). Not S0 whoami. |

---

## 7. Pay env: `ONE_API_KEY` vs user JWT forwarding (two modes)

`NP-ONE-020`: Pay holds only OIDC `client_id` (public), `lzr_sk_` (secret, once), One-webhook HMAC (`whsec_`, shown once). Nothing else from One’s vault.

### 7.1 Mode U — user JWT forwarding (first slice)

Per HTTP request from the signed-in merchant:

1. Pay SPA completes OIDC code + PKCE against Zitadel authority with **Pay’s** `client_id` (`NP-ONE-002`).
2. Browser sends Pay the **access_token** (not the id_token).
3. Pay backend, when it must ask One (`GET /me`, `POST /tenants`, invite, `authz/check`, mint key), sets:

```http
Authorization: Bearer <access_token>
```

optionally `X-Lazuar-Tenant-Id: <one tenant uuid>` as a hint.

4. One authenticates via JWT scheme (selector: token does not start with `lzr_sk_` / `lzr_scim_`).
5. Membership on `{tenantId}` is SoT.

There is **no** long-lived user token in Pay’s server env. Do not stuff `ACCESS_TOKEN` next to `ONE_API_KEY`. Tokens expire; refresh is the SPA’s problem (or Pay BFF if Pay chooses a BFF — out of this paper).

This mode is enough for:

- Slice steps 2–5 whoami, create workspace, invite, mint a key, `authz/check`
- Merchant ops UI as a client of Pay `/v1` (`NP-API-004` human path)

### 7.2 Mode M — `ONE_API_KEY` (later workers / cron / webhook-side One calls)

Pay process env (names **in Pay**; do not reuse old Hub names):

```bash
ONE_API_BASE=http://localhost:8080/api/v1
ONE_API_KEY=lzr_sk_…          # Family A, one tenant, explicit scopes, never commit
ONE_WEBHOOK_SECRET=whsec_…    # receiver HMAC; not a Bearer
# public:
ONE_ISSUER=http://localhost:8085   # or whatever authority lazuar-app uses locally
ONE_CLIENT_ID=…                    # Pay SPA client_id
```

Do **not** set:

| Name | Why |
|------|-----|
| `LAZUAR_API_KEY` / `LAZUAR_SK_TEST_KEY` | Old Hub Family C (`sk_test_`) |
| `LAZUAR_ONE_API_KEY` | One’s example/CLI name; Pay should not collide when both processes share a shell |
| `ZITADEL_PAT` / `ZITADEL_TOKEN` / login PAT | `NP-XX-017` |
| OpenFGA admin / `FGA_CLIENT_SECRET` store admin | `NP-XX-017` |
| `ApiKeys__Pepper` | One’s hasher. Useless and dangerous in Pay. |
| Stripe `sk_live_` as `ONE_API_KEY` | Family B |

Mode M request:

```http
Authorization: Bearer ${ONE_API_KEY}
```

`createClient({ getAccessToken: () => process.env.ONE_API_KEY })`.

When to use Mode M: no user on the stack — billing job, inbound One webhook handler that needs to **call back** into One (rare; prefer trusting the HMAC payload), health “can I still speak to One”, later `events:read` pull.

When **not** to use Mode M: answering a signed-in human. Forward their JWT. Mixing “Pay’s worker key” with “Ada’s session” hides VIEWER vs MEMBER (`NP-ONE-021`). The worker key’s projected role is `member` or `admin` based on **scopes**, not Ada’s membership.

### 7.3 Both modes in one process

Pay’s One HTTP client should take the Bearer as a **parameter** (request-scoped), not a process global:

- Ops handler: Bearer = inbound user access_token.
- Worker: Bearer = `ONE_API_KEY`.
- Merchant M2M into Pay (later): Bearer = caller’s `lzr_sk_`; Pay may **replay** that same Bearer to One `GET /me` to introspect (do not confuse it with `ONE_API_KEY`).

Never fall back from missing user JWT to `ONE_API_KEY` on an interactive route. That would let a logged-out caller act as the worker’s tenant.

### 7.4 “Pay holds one `lzr_sk_`” vs many merchants

011 secrets table: “Pay `lzr_sk_` — Pay (once, secret).” That is **one row in env**.

One’s product law: a key is a **workspace** credential. It cannot see tenant B.

So the one env key is bound to **Pay’s dogfood / first workspace** (the tenant created in slice step 3), **or** to a dedicated “Pay product” tenant if Pay ever has one. It is **not**:

- a key for every merchant
- a platform staff credential
- a substitute for `POST /platform/tenants`

Multi-tenant Pay (the real product) drives One with:

- Mode U for humans in each workspace
- One webhooks (HMAC) for `tenant.suspended` / `member.*` on each workspace Pay subscribed to **with that workspace’s user JWT at provision time**
- Mode M only for the env tenant, or not at all if every job is a webhook handler that already received `tenant_id` in the envelope

If a future job must call One for tenant B without a user, the options are: store **tenant B’s** `lzr_sk_` in Pay (now Pay is a secret vault for One keys — worse than HMAC webhooks), or add a **platform** M2M in One (out of product; docs say use a staff JWT allowlisted in `Platform:AdminEmails`, never a key). Prefer webhooks.

---

## 8. First slice: user JWT for whoami is enough; machine key for jobs/webhooks later

Ordered S0 from [03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) / [12](../011-new-lazuar-pay/12-first-slice-tracker.md):

| Step | Credential to One |
|------|-------------------|
| 1 Register Pay SPA `POST …/apps` | User JWT of an owner/admin on a seed tenant (or seed like `lazuar-app`) |
| 2 Sign-in `:5175`. `GET /me` | **User access_token**. This **is** whoami. No `ONE_API_KEY`. |
| 3 Create workspace `POST /tenants` | **User JWT**. Keys are **rejected** on this route (403), including `admin`/`*`. |
| 4 Invite second engineer | **User JWT**. Keys cannot accept/leave/transfer. |
| 5 Mint scoped `lzr_sk_`; `authz/check` `member` | Mint with **user JWT**. Store secret in env **if** a worker exists; otherwise copy into a local `.env` for the sample and prove `GET /me` as the key **once**. `authz/check` on merchant admin routes can use the **user JWT** (JWT bypasses scope catalog). A worker-side check later uses the key + `authz:check` + **human** `user_id`. |
| 6 Subscribe `member.*` + `tenant.suspended` | **User JWT** to `POST …/webhooks`. HMAC secret stored as `ONE_WEBHOOK_SECRET`. No Bearer needed to **receive**. |
| 7 Stop | No SCIM, no custom FGA types, no npm, no hosted SKU. |

**Whoami lock:** Pay’s first living `GET /me` is Mode U. A test that **only** passes with `ONE_API_KEY` has skipped the human path and will not catch `id_token` mistakes (`NP-ONE-003`).

**Mint lock:** Step 5 still **mints**. Dogfood Y on `NP-ONE-014`. The secret may sit unused in env until a job exists. That is cheaper than discovering at S1 that empty scopes 400 and `*` was pasted from the UI “Everything” chip.

**S1 money** does not need the machine key: BYOK paste, product, pay link, buyer hosted page, provider webhook, journal + `RCPT-`. Merchant ops UI is still Mode U. Machine key on Pay’s **own** `/v1` is `NP-API-004` / `NP-SOON-007` after the human door works.

---

## 9. Never a Zitadel PAT in Pay (`NP-XX-017`, `NP-ONE-020`)

Refuse row text: “Pay holds Zitadel PAT, login PAT, or OpenFGA admin token.”

That includes:

- `ZITADEL_PAT` used by One seed / provisioner to talk to Zitadel Management
- Login-client PAT under `apps/lazuar-login/.secrets/`
- OpenFGA store admin from One’s compose
- Zitadel masterkey / first-instance
- Any “just this once” Console token in Pay’s Taskfile / CI

Pay authenticates to One as:

- a **user** (Bearer JWT access_token), or
- a **machine of one tenant** (Bearer `lzr_sk_`)

Pay never authenticates to **Zitadel** or **OpenFGA** directly. Role SoT is `/me` + `authz/check`, not `urn:zitadel:iam:org:project:roles` (`NP-XX-024`, `NP-XX-008`).

If a Pay engineer is tempted to call Zitadel InviteUser: One membership is SoT; issue 018 closed that façade on One’s side already (`02-one-integration.md` §People).

CI for Pay may call One’s **HTTP** with a test `lzr_sk_` minted in the test fixture (or TestAuth on One — One’s problem). CI may not ship a PAT in Pay’s `deploy/` env examples.

`NP-XX-007` is the sibling refuse: do not put Zitadel, OpenFGA, SCIM, or a password store **inside Pay**. Holding a PAT is how that museum re-enters through the side door.

---

## 10. Merchants mint via One, not Pay’s homemade keys

011 product: “Merchant login, orgs, staff invites, and `lzr_sk_` live in **lazuar-one**.”

Consequences for new Pay:

1. **No** `GenerateApiCredentialCommand` in Pay.
2. **No** `sk_test_` / `sk_live_` mint, prefix, or `IsTestMode` claim derived from prefix.
3. **No** `PlatformApiScopes` catalog (`payments.checkouts:*`, `lhdn.documents:*`) on One keys. One will 400 those strings.
4. **No** `ApiKeyAuthenticationMiddleware` that SHA-256s a Hub key against Pay SQL as the SoT for **One** identity. If Pay later authenticates merchant M2M on **Pay** `/v1`, the SoT is One `GET /me` (or a future introspect One adds). Cache in Pay is a cache.
5. Ops UI “API keys” for **identity** deep-links to lazuar-app or calls One’s POST/GET/DELETE with the user JWT. Pay may wrap that in Pay chrome; the store is One.
6. Ops UI “Payment settings” is Family B (Stripe/CHIP/Billplz). Different page. Different vault. VIEWER blocked.

Bezos door (`NP-API-004`): merchant ops is a client of Pay `/v1` with **One user JWT or `lzr_sk_`**. That `lzr_sk_` is Family A, minted in One, presented to Pay. Pay does not mint a parallel door key.

Old glossary “Machine key = `sk_test_` / `sk_live_` server credential” is **wrong for new Pay**. New sentence: machine key = `lzr_sk_` minted by One; gateway secret = BYOK in Pay.

---

## 11. Revoke: `api_key.revoked` webhook later

### 11.1 How revoke works in One today

`DELETE /api/v1/tenants/{tenantId}/api-keys/{keyId}` with admin JWT (or `admin`/`*` key):

- Sets `revoked_at = now`
- Enqueues `api_key.revoked` (no secret)
- 204 even if already revoked
- Next Bearer with that secret → 401 (handler `Fail("API key has been revoked.")`)

There is no “un-revoke.” Remint.

UI: lazuar-app Settings → API keys. Cleanup in R2 is the same DELETE.

Pay should revoke **Pay’s worker key** with a user JWT when it leaks. Pay should not need Mode M to revoke Mode M (chicken-and-egg): use a human admin.

### 11.2 What Pay should do later (not first slice)

If Pay caches “this `lzr_sk_` → tenant id / scopes” for merchant M2M into Pay `/v1`:

- Subscribe (per tenant, at provision, user JWT) to `api_key.revoked` (and probably `api_key.created` only if Pay lists keys in its own UI cache).
- On delivery: HMAC verify (`R5` / `examples/node-webhook-verify`), idempotent on `X-Lazuar-Event-Id`, drop cache by `data.key_id` and/or `data.prefix`.
- Do not treat prefix as unique. Prefer `key_id`.
- If the webhook is late, a revoked key might still pass Pay’s cache until TTL. Fail **closed** on One 401 if Pay re-introspects. 011 money rule: if the webhook is late, **money in Pay is still true**; staff access may lag. A revoked **machine** key is closer to staff access than to money. Do not grant buyer entitlement from One (`02` two planes).

If Pay does **not** cache (first slice, Mode U only): ignore `api_key.revoked` until there is a cache. Still fine to include it in the endpoint’s event filter (`[]` means all types).

Pull path: `GET /tenants/{id}/events` with `events:read` if push is down (`NP-ONE-017` notes). Do not tail Zitadel.

### 11.3 What revoke is not

- Not Family B: rotating Stripe keys is Pay ops + `NP-AUD-003` audit row in Pay’s DB transaction.
- Not Family C cutover.
- Not pepper rotation (that unverifies **all** keys at once; One runbook).
- Not SCIM token revoke (`lzr_scim_`).

---

## 12. Tests

Do not implement these here. When Pay grows an One client, lock the following. Prefer HTTP against a running One (or One’s own `LazuarApiFactory` if Pay’s repo is allowed to reference it — it is not, today; use recorded/live local). Hermetic Pay tests should **fake One HTTP**, not fake Zitadel.

### 12.1 One already locks (do not regress by wrapping badly)

Live in `lazuar-one` — Pay may treat these as the oracle:

| Test | File | Lock |
|------|------|------|
| Secret once; list has no secret; DB is hash | `ApiKeyTests.Create_returns_secret_once_list_has_no_secret_db_hash_only` | `lzr_sk_` prefix; hash ≠ secret |
| Omitted scopes → `tenant:read` | `Create_omitted_scopes_defaults_to_tenant_read` | |
| Empty scopes → 400 | `Create_explicit_empty_scopes_returns_400` | |
| Unknown scope → 400 | `Create_unknown_scope_returns_400` (`members:write`) | |
| `/me` + list project scopes | `Me_and_tenant_list_project_key_scopes_into_permissions` | `tenant:read` **drops** from permissions chrome |
| Limited key cannot create keys | `Limited_scope_key_cannot_create_keys` | |
| Raw key on tenant GET 200 | `Authenticate_with_raw_key_on_tenant_route_succeeds` | |
| Revoke then auth fails | `Revoke_key_subsequent_auth_fails` | |
| List tenants = bound only | `Api_key_list_tenants_returns_bound_tenant` / `_excludes_other_tenants` | |
| Cross-tenant GET 403 | `Api_key_get_other_tenant_403` | |
| Scope matrix members/apps/keys | `Tenant_read_cannot_list_*`, `Members_read_*`, `Keys_read_*`, `Apps_read_*`, `Admin_key_can_list_*` | |
| JWT member lists members without scopes | `Jwt_member_can_list_members_without_scopes` | |
| Key `POST /tenants` 403 even admin | `Api_key_post_tenants_403` | |
| Accept / leave / transfer 403 | `Api_key_accept_invite_403`, `_leave_403`, `_transfer_403` | |
| `/me` key id, not platform admin | `MeTests.Api_key_me_returns_key_id_and_bound_tenant` | |
| `admin` scope → `/me` role admin, still not platform admin | `Api_key_admin_scope_me_role_is_admin` | |
| Key `/me` sets active tenant without header | `Api_key_me_sets_active_tenant_without_header` | |
| Hasher HMAC ≠ concat SHA256 | `ApiKeyHasherTests` | |
| Empty scopes deny; JWT always passes; `*`/`admin` short-circuit | `ApiKeyScopeHelperTests` | |
| Outbox create/revoke without secret | `WebhookProducerTests.I_PRD_04_*` | |
| ISO-21 revoked key | `IsolationExpansionTests` | |
| ISO-22 insufficient scopes 403 | `IsolationExpansionTests` | |
| ISO-07/08/12 cross-tenant keys | `TenantIsolationTests` | |
| Authz key without `authz:check`; key id as `user_id` 400 | `AuthzFacadeTests` | |
| Create 429 | `RateLimitTests` with `CreateApiKeyPerWindow=2` | |

Pay should not duplicate this suite inside Pay. Pay should **not** be able to turn a 403 into a 200 by sending a PAT.

### 12.2 Tests Pay must own (when the client exists)

**Mint / header / scopes (NP-ONE-014)**

1. Local mint script or test: user JWT `POST …/api-keys` with `scopes: ["tenant:read","authz:check"]` → 201, `secret` starts with `lzr_sk_`, `prefix` is first 16 chars, body has no `key_hash`.
2. Same with `scopes: []` → 400. Pay’s mint helper must not swallow this into a retry with `*`.
3. Same with omitted scopes → 201 `["tenant:read"]`. Pay’s helper should still **send** explicit scopes so this path is unused in production code.
4. Same with `scopes: ["*"]` → 201 in One, but **Pay’s helper refuses to send `*` / `admin`** (unit test on the helper, not on One).
5. Same with `scopes: ["payments.checkouts:write"]` → 400 unknown. Proves Family C strings are dead.
6. `GET …/api-keys` as the user → no `secret` field.
7. `DELETE` then `GET /me` with the old secret → 401.

**Whoami modes (first slice)**

8. Mode U: Pay whoami handler forwards **access_token**, not `id_token`, not `ONE_API_KEY`. Fixture: inbound session has both id_token and access_token; spy on outbound Authorization; must equal access_token.
9. Mode U: missing user token on an interactive route **does not** fall back to `ONE_API_KEY`.
10. Mode M (later, or a sample worker): env `ONE_API_KEY=lzr_sk_…` → outbound `Authorization: Bearer lzr_sk_…` → One `GET /me` 200, `user_id` is a GUID, `is_platform_admin` false, `tenants.length` ∈ {0,1}.
11. Mode M: env value `sk_test_…` or `sk_live_…` → Pay **fails closed** before calling One (wrong family). Optional: if it does call, One 401; still assert Pay’s precheck so Stripe BYOK cannot leak into Bearer.
12. Mode M: env value a Zitadel PAT / JWT → fail closed. `NP-XX-017`.
13. Example port: running `examples/node-api-key` against local One is the manual oracle; Pay’s worker whoami should match that JSON shape.

**Authz (NP-ONE-015, adjacent)**

14. Worker key with only `tenant:read` → `authz/check` 403.
15. Worker key with `authz:check`, `user_id` = key id → 400.
16. Worker key with `authz:check`, `user_id` = minting human → 200 `{ allowed: true }` when that human is a member.
17. Interactive route: `authz/check` uses **forwarded JWT**, not the env key, so VIEWER cannot ride the worker’s `admin` mistake.

**Isolation**

18. `ONE_API_KEY` bound to tenant A: Pay must not call `/tenants/{B}/…` with it and treat 403 as “empty members.” Surface the 403.
19. `X-Lazuar-Tenant-Id: B` with a key for A must not authorize B (`NP-ONE-007`).

**BYOK vs One (NP-GW-001 vs NP-ONE-014)**

20. Saving a Stripe `sk_live_…` in Pay payment settings does **not** write `ONE_API_KEY` and does not call One `POST …/api-keys`.
21. `ONE_API_KEY` is not passed to the Stripe SDK.
22. VIEWER can neither rotate BYOK nor mint One keys (One 403; Pay chrome hides both).

**Revoke webhook later (NP-ONE-017 subset)**

23. Given a cache entry for `key_id`, a signed `api_key.revoked` delivery drops it. Unsigned / wrong HMAC → 401/400, cache remains.
24. Payload containing a secret-looking string: producer already forbids it; Pay must not **log** `data` at info if it ever included a secret (defense).
25. First slice: **no fail** if `api_key.revoked` is not handled yet, as long as Mode U is the only interactive path.

**Client / DX (NP-XX-021)**

26. Pay builds against workspace `@lazuar/one-client` or raw fetch. CI does not `npm view @lazuar/one-client`.
27. Pay’s `.env.example` documents `ONE_API_KEY=lzr_sk_…` and says never `ZITADEL_PAT`, never Stripe, never `LAZUAR_API_KEY`.

**Runnable sample alignment**

28. Document in Pay’s later ops README: mint via R2, then:

```bash
export LAZUAR_API_BASE=http://localhost:8080/api/v1
export LAZUAR_API_KEY="$ONE_API_KEY"
node /Users/akmalfirdaus/Code/lazuar/lazuar-one/examples/node-api-key/index.mjs
```

Expect 200. That is the dogfood that One already ships; Pay should not rewrite it.

### 12.3 Tests Pay must not write

- Hasher unit tests against One’s pepper.
- SQL against One `api_keys`.
- Zitadel Management “create PAT then call One.”
- A test that sends `id_token` and asserts 200.
- A test that empty scopes mean admin (stale `deploy/runbooks/isolation-incident.md` still says “Empty API key scopes = broad within that tenant only (v1)” — **false** as of P12; do not copy into Pay).

---

## 13. Binding decisions (for later implementers)

1. **Mint in One, hold in Pay env at most one worker secret.** Name it `ONE_API_KEY`. Value starts with `lzr_sk_`. Scopes explicit: first mint `["tenant:read","authz:check"]`.
2. **First-slice whoami is Mode U** (user access_token). Mode M is jobs/webhooks later. Slice step 5 still mints.
3. **Never `*` / empty / omitted-by-accident.** Helper refuses `*` and `admin` unless a later paper writes why.
4. **Never Zitadel PAT, login PAT, OpenFGA admin, One pepper** in Pay (`NP-XX-017`, `NP-ONE-020`).
5. **Never homemade `sk_test_` / `sk_live_` integrator keys** in new Pay. Prefix collision with Stripe is a closed incident, not a tradition.
6. **Stripe/CHIP/Billplz BYOK is Family B**, encrypted in Pay, S1, VIEWER cannot change. Not Bearer to One.
7. **Merchants mint Family A in One** (UI or Pay chrome that calls One). Pay may later accept those Bearers on Pay `/v1` by introspecting One `GET /me`, not by copying hashes.
8. **`api_key.revoked` is real in One today.** Pay handles it when Pay caches; not a blocker for Mode U whoami.
9. **Do not block on npm** (`NP-XX-021`). `examples/node-api-key` and recipe R2 are the mint/use proof.
10. **One key ≠ platform admin ≠ all merchants.** Webhooks + user JWT scale to many tenants; a single env key does not.
11. **Do not send the key id as `authz/check` `user_id`.** Send the human.
12. **`GET /me.permissions` on a key is not the scope list.** `tenant:read` vanishes there. Believe the catalog and 403 bodies.
13. **Header is `Authorization: Bearer lzr_sk_…`.** Same header shape as JWT; prefix selects the scheme. Pay’s client should not invent `X-Api-Key`.
14. **Hash vs prefix:** prefix is a 16-char hint; hash is HMAC-SHA256(pepper, secret). Pay uses neither internally except displaying prefix if One list is shown in Pay chrome.

---

## 14. Residual honesty (One side, not Pay tickets)

These are true in One at SHA `0f79fe4` and will surprise Pay if ignored:

| Residual | Where |
|----------|--------|
| `integrations/api-keys.md` “Webhooks for key lifecycle events (**planned**)” | False; producer live. Webhook guide is correct. |
| `deploy/runbooks/isolation-incident.md` empty scopes = broad | Stale vs P12. |
| JWT any member can list keys (no `keys:read`) | Comment on `ApiKeyEndpoints.ListKeys`. |
| lazuar-app chips omit `events:read` / `audit:read` | API accepts them. |
| `@lazuar/one-client` has no `revoke` | Raw DELETE. |
| `examples/node-api-key` README advertises `.env.example` | File exists; sample does not mint. |
| `/me.permissions` drops `tenant:read` | `TenantPermissions` vs API-key catalog mismatch. |
| TypeSpec single `BearerAuth` | Runtime three schemes by prefix. |
| No dedicated expired-key integration test found | Handler implements expiry; tests cover revoke more than expiry. |
| Rate limit Memory default | Multi-instance One needs Redis; Pay’s local compose is fine. |
| Docs UI path “Integrations → API keys” vs page “Settings → API keys” | Same feature, two phrasings. |

None of these are reasons for Pay to mint Family C again.

---

## 15. Map back to 011 §Machines and apps / §Secrets

Restated from Pay’s side with the facts above:

| 011 line | This paper |
|----------|------------|
| `POST /tenants/{id}/api-keys` worker / cron / Pay API → One | Mode M later; mint at slice step 5 with Mode U |
| `GET` list / `DELETE` revoke | Mode U (human admin). Key needs `keys:read` / `admin` |
| Explicit scopes; empty/`*` footgun; prefer `tenant:read` + routes hit | §6: `["tenant:read","authz:check"]` first |
| `api_key.revoked` → drop cached secrets | §11 later |
| Pay `lzr_sk_` once, secret | `ONE_API_KEY`, one tenant |
| Never Zitadel PAT / FGA admin / masterkey | §9 |
| `@lazuar/one-client` wraps `apiKeys.create/list` | Yes; no revoke; unpublished |

First Pay slice from 011 §6.11 step 4 “Mint a scoped key for Pay API” is **NP-ONE-014**. Step 2 `GET /me` remains the user JWT. This paper refuses to merge those two Bearers into one env var.
