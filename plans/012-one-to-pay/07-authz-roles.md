# 07 — Authorization: one `authz/check`, membership roles, VIEWER vs MEMBER vs owner, path SoT

**Date:** 20 August 2026  
**Status:** analysis only. Do not implement from this file.  
**Assigned slice:** `POST /tenants/{id}/authz/check` allow-list `{tenant, app}`. Pay must not add FGA types `payment` / `document` yet. VIEWER cannot charge. Do not parse Zitadel project roles. `batch-check` later.

**Repos / SHAs (this paper’s evidence cut):**

| Repo | Path | Branch | Full SHA | Short | Tip subject |
|------|------|--------|----------|-------|-------------|
| Pay | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `feat/012-one-to-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `6ca8f19f` | `feat(pay): add TypeSpec package for the focused Pay host` |
| One | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `main` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` |

One is **not** a git submodule of Pay. The sibling path is the one named in [011 `02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md). If either SHA moves, re-read `AuthzObjectRules`, `MembershipRoles`, and `model.fga` before trusting the allow-list and role tables below.

Focused Pay HTTP on this Pay SHA is still only `GET /v1/health` (`packages/pay-spec/main.tsp`). There is no merchant admin route yet. This paper specifies the **first dummy** one: path `{tenantId}`, One `authz/check` `member` on `type=tenant`, then a 200 fixture. Money routes are not that dummy.

---

## 0. What this paper is for

Pay is Consumer-0 of Lazuar One. Authorization for merchant staff is **not** a Pay OpenFGA store, not a Pay membership table, not a Zitadel project-role claim, and not the inbound header `X-Lazuar-Tenant-Id`.

The live One façade is one POST:

```http
POST /api/v1/tenants/{tenantId}/authz/check
```

That is the only check Pay should call in S0. `batch-check` is chrome later (NP-ONE-016). `list-objects` is not workspace inventory. There is no public `authz/write`. The object-type allow-list on One today is `{ tenant, app }`. Pay must not ask One to grow `payment` or `document` until Pay has a written check site that actually posts those types — and this slice does not have that site.

The 011 papers talk about VIEWER as if it were a One membership role. **It is not.** One’s product roles are `owner` | `admin` | `member`. `viewer` exists only as an OpenFGA relation on type `app` (OIDC registry). Old Pay’s `VIEWER` (uppercase, `WorkspaceStaffRoles`) is a different vocabulary. Section 10 is honest about that mismatch and what Pay should do on money routes anyway.

---

## 1. Sources (must-read, then One runtime)

### 1.1 Pay 011 papers

- [011 `02-one-integration.md`](../011-new-lazuar-pay/02-one-integration.md) — Authz table, allow-list `{ tenant, app }`, no FGA types `payment` / `document`, Pay does not get `authz/write`, path SoT, header hint, do not parse Zitadel project roles.
- [011 `03-first-slice.md`](../011-new-lazuar-pay/03-first-slice.md) — step 5: `authz/check` `member` before merchant admin routes; step 12: VIEWER cannot change keys or refund.
- [011 `11-checklist.md`](../011-new-lazuar-pay/11-checklist.md) — NP-ONE-015, 016, 021, 022; NP-XX-015, 016, 024 (and 007, 008 for path + roles).
- [011 `12-first-slice-tracker.md`](../011-new-lazuar-pay/12-first-slice-tracker.md) — ordered loop; step 5 IDs NP-ONE-014 / 015.

### 1.2 One dogfood paper §6.7 (and the nearby kernel)

One `plans/017-evals/08-dogfood-then-serve.md`:

- **§4.9** Authz façade — check and batch-check are real; allow-list `{ tenant, app }`; named consumer is in-repo OIDC apps, not Pay documents.
- **§6.3** Session — `GET /me`; `X-Lazuar-Tenant-Id` hint only; path + membership is SoT; do not parse `urn:zitadel:iam:org:project:roles`.
- **§6.7 Authz** — the Pay-call table this slice is named after.
- **§6.11** Suggested first Pay slice step 5: `check` `member` before Pay-side admin routes; **stop** — no custom FGA types.
- **§11.1–11.3** 403 law, persist-then-FGA, do not cache `/me` for authorization.

### 1.3 One runtime (evidence, not docs)

| Piece | Path |
|-------|------|
| HTTP | `apps/lazuar-api/src/Lazuar.One.Api/Features/Authz/AuthzEndpoints.cs` |
| Service | `…/Features/Authz/AuthzService.cs` |
| Allow-list | `…/Features/Authz/AuthzObjectRules.cs` |
| Interface | `…/Features/Authz/IAuthzService.cs` |
| Membership roles | `…/Domain/Tenants/MembershipRoles.cs` |
| Membership row | `…/Domain/Tenants/Membership.cs` |
| Path membership gate | `…/Infrastructure/Tenancy/TenantAccessService.cs` |
| Header hint | `…/Infrastructure/Tenancy/ActiveTenantHint.cs` |
| Custom-role overlay | `…/Infrastructure/Tenancy/TenantPermission.cs`, `…/Domain/Tenants/TenantPermissions.cs` |
| `/me` | `…/Features/Platform/MeEndpoints.cs` |
| FGA model | `deploy/dev/openfga/model.fga` |
| FGA ids | `…/Infrastructure/OpenFga/FgaIds.cs` |
| Exception → RFC 7807 | `…/Infrastructure/Http/ServiceExceptionHandler.cs` |
| TypeSpec | `packages/api-spec/modules/authz/{models,routes}.tsp` |
| `/me` TypeSpec | `packages/api-spec/modules/platform/models.tsp` |
| MembershipRole enum | `packages/api-spec/modules/tenants/models.tsp` |
| Client | `packages/one-client/src/{authz,createClient}.ts` |
| Docs | `apps/lazuar-docs/docs/integrations/authz.md`, `docs/recipes/authz-check.md` |
| Tests | `apps/lazuar-api/tests/Lazuar.One.Api.Tests/Integration/AuthzFacadeTests.cs`, `TenantIsolationTests.cs` ISO-09 |

### 1.4 Old Pay (contrast only — not the new host)

Old Pay authorized merchant staff from **header** `X-Tenant-Id` plus cookie JWT role claims `ADMIN` / `MEMBER` / `VIEWER` (`TenantSecurityMiddleware`, `AuthAndCorsExtensions`, `WorkspaceStaffRoles`). New Pay must not copy that. Path `{tenantId}` + One `authz/check` replaces it.

---

## 2. Checklist rows this slice owns

Copied from [011 `11-checklist.md`](../011-new-lazuar-pay/11-checklist.md) so the mapping cannot drift by paraphrase.

| ID | Feature | Wave | Owner | Dogfood | Status | Notes |
|----|---------|------|-------|---------|--------|-------|
| NP-ONE-007 | Path `{tenantId}` + membership is authz SoT | S0 | Pay | Y | todo | `X-Lazuar-Tenant-Id` is a hint only |
| NP-ONE-008 | Roles from `/me` + `authz/check`, not Zitadel project-role claims | S0 | Pay | — | todo | Do not parse `urn:zitadel:iam:org:project:roles` |
| NP-ONE-015 | `authz/check` `member` / `admin` / `owner` before merchant admin routes | S0 | both | Y | todo | Allow-list `{ tenant, app }` only |
| NP-ONE-016 | `authz/batch-check` for permission chrome | S0 | both | — | todo | No `authz/write` |
| NP-ONE-021 | VIEWER cannot charge, change keys, or refund | S0 | Pay | Y | todo | Enforce in Pay using One role + `authz` |
| NP-ONE-022 | Invited MEMBER can see merchant ops | S0 | Pay | Y | todo | Dogfood second engineer |
| NP-XX-008 | Dual JWT vs membership roles | refuse | Pay | — | refuse | `/me` + `authz/check` |
| NP-XX-015 | Add FGA types `payment` / `document` with no written check call | refuse | both | — | refuse | AUTHZ-05 only with Pay as named consumer |
| NP-XX-016 | Pay calls One `authz/write` | refuse | Pay | — | refuse | |
| NP-XX-017 | Pay holds Zitadel PAT, login PAT, or OpenFGA admin token | refuse | Pay | — | refuse | |
| NP-XX-024 | Parse Zitadel `urn:zitadel:iam:org:project:roles` | refuse | Pay | — | refuse | |

NP-ONE-015’s **first** Pay call is `relation=member` on `object.type=tenant` (first-slice step 5, dogfood §6.11 step 5). `admin` / `owner` checks are the same endpoint with a different `relation`, used later for keys / ownership chrome — not for the dummy 200-fixture route.

NP-ONE-016 is **later**. Do not block the dummy route on batch-check.

NP-ONE-021 is **Pay-enforced on money routes**. It is not satisfied by `check(member)` alone. See §10.

---

## 3. Exact check request / response as implemented

This section is the contract Pay must speak. It is TypeSpec + generated OpenAPI types + runtime JSON (snake_case). It is not a sketch.

### 3.1 HTTP

```http
POST /api/v1/tenants/{tenantId}/authz/check
Authorization: Bearer <access_token | lzr_sk_…>
Content-Type: application/json
Accept: application/json
```

Optional, never authorizing:

```http
X-Lazuar-Tenant-Id: <guid>
```

`{tenantId}` is a GUID. The group is mapped as `/tenants/{tenantId:guid}/authz` (`AuthzEndpoints.MapAuthzEndpoints`). A non-GUID path does not hit this handler.

The route requires `AuthorizationPolicies.AuthenticatedUser`. Unauthenticated → 401 before membership.

### 3.2 Request body (`AuthzCheckRequest`)

TypeSpec (`packages/api-spec/modules/authz/models.tsp`):

```tsp
model AuthzCheckRequest {
  user_id?: string;
  relation: string;          // @minLength(1) @maxLength(64)
  object: AuthzObjectRef;
}

model AuthzObjectRef {
  type: string;              // runtime: "tenant" | "app"
  id: string;
}
```

Generated TypeScript (`packages/one-client/src/generated.ts`, schema `Authz.AuthzCheckRequest`):

```ts
{
  user_id?: string;
  relation: string;
  object: { type: string; id: string };
}
```

Generated C# (`packages/api-type-dotnet/Lazuar.One.ApiContracts.cs`): `[JsonPropertyName("user_id")]`, `"relation"`, `"object"`.

Wire JSON for the **Pay dummy admin** call (user JWT, check self as member of the path tenant):

```json
{
  "relation": "member",
  "object": {
    "type": "tenant",
    "id": "{tenantId}"
  }
}
```

`object.id` **must equal** the path `{tenantId}` when `type` is `tenant`. That is `AuthzObjectRules.ValidateObject`. A mismatch is **400**, not 403:

> `object.id must equal the path tenantId for type "tenant" (cross-tenant checks are rejected).`

`user_id` is omitted on user JWT so the subject is the token `sub`. Setting `user_id` to another human requires the **caller** to be tenant admin/owner or platform admin (`RejectForeignSubject` / `AuthzService.ResolveSubject`). A JWT `member` checking another user is **403** `"Cannot check authorization for another user unless you are an admin."` (test `Jwt_member_check_as_other_user_is_403`).

API keys are the exception:

- `user_id` is **required**. Omit → 400 `"user_id is required when authenticating with an API key."`
- `user_id` equal to the key id → 400 `"user_id must be a user subject, not the API key id."`
- Key needs scope `authz:check` (or `admin` / `*`). Missing scope → 403 `"API key lacks required scope authz:check."`
- A key with that scope **may** pass any user subject; tenant admin role is not required. The key GUID is never an FGA user.

Pay’s dummy merchant-admin route is a **human** session. It forwards the user’s access_token. It does not use `lzr_sk_` for this check. Pay’s worker key is a different caller (NP-ONE-014) and must send `user_id` if it ever hits this façade.

### 3.3 Success response (`AuthzCheckResponse`)

```json
{ "allowed": true }
```

or

```json
{ "allowed": false }
```

HTTP status on this body is **200**. `allowed: false` is not 403. Pay must treat `allowed: false` as deny (see §8). Tests that prove One’s own shape: `Owner_check_can_view_allowed` (200 + `allowed: true`), `Api_key_authz_check_user_id_non_member_allowed_false` (200 + `allowed: false`).

C#: `AuthzCheckResponse.Allowed` with `[JsonPropertyName("allowed")]`. One Program.cs sets `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`, so the wire name is `allowed` either way.

### 3.4 Error responses (RFC 7807)

One maps `ServiceException` and `TenantAccessException` through `ServiceExceptionHandler` to ProblemDetails (`type`, `title`, `status`, `detail`, `instance`, `request_id`).

| When | Status | Detail (as implemented) |
|------|--------|-------------------------|
| No / invalid bearer | 401 | token missing subject, or not authenticated |
| Caller is not an active member of path `{tenantId}` (and not platform admin / provisioning owner) | **403** | `"Not a member of this tenant."` (`TenantAccessService.Forbidden`) |
| Tenant suspended, caller not platform admin | 403 | `"Tenant is suspended."` |
| Tenant provisioning/failed and caller is not owner / platform admin | 403 | `"Tenant is not active."` |
| API key lacks `authz:check` | 403 | `"API key lacks required scope authz:check."` |
| JWT member sets `user_id` ≠ self | 403 | `"Cannot check authorization for another user unless you are an admin."` |
| Missing `relation` or `object` | 400 | `"relation and object are required."` |
| Unknown `object.type` (`payment`, `document`, …) | 400 | `"Authz only supports object type(s): \"app\", \"tenant\"."` (order is sorted, `FormatSupportedTypes`) |
| `type=tenant` but `object.id` ≠ path | 400 | path-id binding message above |
| `type=app` but id not an active app in this tenant | 400 | `"object.id must be an active application in this tenant."` |
| Unsupported relation for type | 400 | `"Unsupported relation for type \"{type}\". Allowed: …"` |
| API key omits `user_id` or uses key id | 400 | user_id messages above |
| OpenFGA enabled and Check throws | **503** | `"Authorization service is unavailable. Fail-closed."` |
| Authz POSTs over window | **429** | `RateLimitPolicies` policy `"authz"`; default `AuthzPerWindow = 30` / 60s |
| Batch > 50 | 400 | `"checks must not exceed 50 items (got N)."` (`AuthzService.MaxBatchChecks`) |

Isolation test ISO-09: user A `POST /tenants/{TenantB}/authz/check` → **403**, not 404, not 200 `{allowed:false}`. The membership gate runs **before** OpenFGA. Recipe R6. Pay must inherit 403-not-404 (dogfood §11.2): a guessed merchant UUID is not an existence oracle.

Cross-tenant **object.id** on type `tenant` (caller *is* a member of the path tenant, but posts a different tenant GUID as `object.id`) is 400 malformed, not 403. Pay should never construct that body: `object.id` is always the path id.

### 3.5 `batch-check` (same types, later)

```http
POST /api/v1/tenants/{tenantId}/authz/batch-check
```

```json
{
  "checks": [
    { "relation": "member", "object": { "type": "tenant", "id": "{tenantId}" } },
    { "relation": "admin",  "object": { "type": "tenant", "id": "{tenantId}" } }
  ]
}
```

```json
{
  "results": [{ "allowed": true }, { "allowed": false }]
}
```

Max 50. Shared optional `user_id`. Same allow-list, same membership gate, same fail-closed 503. **Not** the dummy admin route. NP-ONE-016: permission chrome (hide Refund vs Rotate key in one round trip). Do not implement in this slice.

### 3.6 `list-objects` (do not teach Pay to inventory workspaces)

```http
POST /api/v1/tenants/{tenantId}/authz/list-objects
```

`type=tenant` is a **disguised 0/1 Check**: returns `[tenantId]` or `[]`. It never lists other tenants. `type=app` is real ListObjects filtered to non-revoked apps whose `TenantId == path`. Pay lists workspaces with `GET /me` / `GET /tenants`, not this endpoint.

### 3.7 Client helper (unpublished)

`@lazuar/one-client` (`packages/one-client/src/authz.ts`):

```ts
await check(client, tenantId, {
  relation: 'member',
  object: { type: 'tenant', id: tenantId },
})
```

`createClient({ getTenantId })` sets `X-Lazuar-Tenant-Id` when present. Comment on the option: **“Never authorizes by itself.”** First-party Pay may import the workspace package (NP-XX-021: do not block on npm). Pay may also raw-fetch; the body must still be the JSON above.

Hermetic client test (`packages/one-client/tests/createClient.test.ts`): posts `/tenants/t1/authz/check` with `{ relation: 'member', object: { type: 'tenant', id: 't1' } }`, expects `{ allowed: true }`.

---

## 4. One implementation (evidence)

### 4.1 Endpoint algorithm (`AuthzEndpoints.Check`)

Order of operations, as written:

1. `access.RequireMembershipAsync(user, tenantId)` — SQL membership (or API-key bound tenant, or platform-admin break-glass, or provisioning-owner). Throws `TenantAccessException` 403/401.
2. `DenyApiKeyScope` — keys need `authz:check`.
3. Body required: `relation` and `object`.
4. `RejectApiKeyAuthzSubject` — keys must pass a real `user_id`.
5. `RejectForeignSubject` — JWT members cannot check someone else unless admin/owner/platform admin.
6. `authz.CheckAsync(...)` — allow-list + FGA or local fallback.
7. `Results.Ok(new AuthzCheckResponse { Allowed = allowed })` — **always 200** if we got this far, including `allowed: false`. Denied checks also emit `AuditLog.Events.AuthzDenied`.

`IAuthzService` is registered in `Program.cs` and consumed **only** by `AuthzEndpoints`. One’s own product routes (invite, keys, apps) do **not** call Check. They use `TenantAccessService` + `TenantPermission`. Pay cannot “ride” those internal helpers. Pay is an HTTP consumer.

### 4.2 Allow-list (`AuthzObjectRules`) — runtime SSoT

TypeSpec keeps `type` a **string** on purpose so One never ships a public enum of fictional types. Runtime rejects unknown types.

```csharp
SupportedObjectTypes = { "tenant", "app" }

RelationsByType["tenant"] = {
  "owner", "admin", "member",
  "can_view", "can_manage_members", "can_manage_tenant"
}

RelationsByType["app"] = { "viewer", "admin" }

ObjectIdMustEqualPathTenant = { "tenant" }   // id == path tenantId
RequiresParentTenant("app") == true          // id is OIDC app UUID in that tenant
```

Unknown type → 400 listing `"app", "tenant"`. Tests: `Object_type_not_tenant_rejected`, `ISO_Z_unknown_type_rejected` both POST `type: "document"` and expect 400 with body containing `"tenant"`.

**Pay must not POST `type: "payment"` or `type: "document"`.** One will 400. Adding those types in One is NP-XX-015 / AUTHZ-20 until Pay has a named check site. This slice’s check site is `tenant` + `member` only.

`viewer` as a **relation** is allowed only for `type=app`. Posting `{ relation: "viewer", object: { type: "tenant", id } }` is 400 unsupported relation. Pay cannot use `check(viewer)` on a tenant to mean old-Pay VIEWER.

### 4.3 `AuthzService.CheckAsync`

1. `ResolveSubject` (JWT sub vs `user_id` vs API-key rules).
2. `ValidateObject` + `ValidateRelation`.
3. `BindParentObjectAsync` for `app` (must be active, same tenant).
4. If `OpenFga:Enabled`:
   - `IOpenFgaClient.CheckAsync(user:{sub}, relation.lower, tenant:{id} | app:{id})`.
   - Transport / invalid-op / cancel → log + **503 fail-closed**. Never `{allowed:true}` on FGA down. Test `Fake_throw_on_check_fail_closed`.
5. If FGA disabled: `EvaluateLocalAsync` — SQL active membership + `RelationSatisfiedByRole`.

Local tenant relations (`RelationSatisfiedByRole`):

| relation | SQL role that satisfies |
|----------|-------------------------|
| `owner` | `owner` only |
| `admin` | `owner` or `admin` (`IsAdminOrOwner`) |
| `member` | any valid role (`owner` \| `admin` \| `member`) (`IsValid`) |
| `can_view` | any valid role |
| `can_manage_members` | admin or owner |
| `can_manage_tenant` | admin or owner |
| anything else | false |

Local app relations: `viewer` ← any valid membership; `admin` ← admin or owner. That mirrors `model.fga` `viewer: member from tenant`.

Development default (`appsettings.Development.json`): `"OpenFga": { "Enabled": false }`. Local DX therefore uses the SQL fallback. Docs call this “fail-open for local DX” meaning **members** evaluate from SQL; strangers still 403 at the membership gate. Production/Staging forbid `Enabled=false` unless `AllowDisabledInStrictEnvironments` (validator). Pay dogfood on a laptop with FGA off will see `check(member)` track SQL membership. Pay dogfood against Staging with FGA on will see the graph, including persist-then-FGA lag (§15).

### 4.4 OpenFGA model v2 (`deploy/dev/openfga/model.fga`)

```text
type user

type tenant
  relations
    define owner: [user]
    define admin: [user] or owner
    define member: [user] or admin
    define can_view: member
    define can_manage_members: admin
    define can_manage_tenant: admin

type app
  relations
    define tenant: [tenant]
    define viewer: [user] or member from tenant
    define admin: [user] or admin from tenant
```

Hierarchy (One 017-07 / 015-04 ceiling sentence): **`owner` ⊂ `admin` ⊂ `member`**. Checking `member` allows owner, admin, and member. Checking `admin` allows owner and admin. Checking `owner` allows owner only. Checking `can_view` is currently the same set as `member`.

App `viewer` is **not** a staff role. Every tenant member is an app viewer via `member from tenant`. Pay must not read `app.viewer` as “this human is a read-only merchant.”

Tuple writes are **platform dual-write** on membership / app lifecycle (`MembershipService.WriteFgaOrUnavailableAsync`, `OidcAppService`). Subjects are `user:{zitadelSub}` (`FgaIds.User`). Pay never writes tuples.

### 4.5 Membership gate vs façade (two planes)

From One 017-07 §2.2, still true on this SHA:

```text
Product HTTP (invite, keys, apps, …)
    → TenantAccessService.RequireMembershipAsync   (SQL)
    → TenantPermission.Require                     (owner/admin win; custom catalog)

Public façade  POST …/authz/{check,batch-check,list-objects}
    → TenantAccessService.RequireMembershipAsync   (caller must be a member first)
    → AuthzObjectRules
    → AuthzService                                 (FGA if Enabled, else local)
```

Pay talks to the **façade**. Pay does not get `TenantPermission` over HTTP except as `/me.tenants[].permissions` chrome (hint only). Those strings are One settings (`sso:manage`, `tenant:update`, …), not Pay money verbs.

---

## 5. Membership roles as implemented (One)

### 5.1 The only product roles

`MembershipRoles.cs`:

```csharp
public const string Owner = "owner";
public const string Admin = "admin";
public const string Member = "member";

IsValid(role)        => role is Owner or Admin or Member
IsAdminOrOwner(role) => role is Owner or Admin
ToFgaRelation(role)  => owner | admin | member   // else ArgumentOutOfRangeException
```

TypeSpec enum `MembershipRole { owner, admin, member }` (`packages/api-spec/modules/tenants/models.tsp`). There is **no** `viewer` value.

Invite (`MembershipService.NormalizeInviteRole`):

- `owner` → 400 `"Cannot invite as owner. Use transfer-ownership after the user joins."`
- not `admin` or `member` → 400 `"Role must be admin or member."`

You cannot invite a VIEWER on One. You cannot store `role=viewer` on `memberships.role` without failing `IsValid` / `ToFgaRelation`.

Creator of `POST /tenants` becomes **owner**. Transfer-ownership is the only owner change.

### 5.2 Custom roles are not a fourth FGA role

`Membership.cs`:

> Optional custom role. When set, `Role` stays `member` and FGA is member only.

Invite with `custom_role_id` returns `(MembershipRoles.Member, custom.Id)`. FGA tuple is still `member`. `TenantPermission.Allows` is a **closed SQL overlay** on One’s own settings routes. Catalog (`TenantPermissions.All`):

```
tenant:update, tenant:delete, domains:manage, roles:manage,
events:read, audit:read, sso:manage, scim:manage, streams:manage
```

No `payments:charge`, no `keys:write`, no `refund`. `/me.tenants[].permissions` is that catalog projected for chrome. TypeSpec: **“Hint only — never authorize from this field alone.”**

Pay must not treat a custom role named “Viewer” as old-Pay VIEWER. FGA `check(member)` will still allow that human.

### 5.3 `viewer` in One means something else

| String | Where | Meaning |
|--------|-------|---------|
| `viewer` (lowercase) | FGA relation on **type `app`** | Any tenant member can view that OIDC app (`member from tenant`), plus direct `viewer` tuples if any |
| `VIEWER` (uppercase) | **Old Pay** `WorkspaceStaffRoles.Viewer` | Staff role: OrgRead yes, OrgMember/OrgAdmin no — cannot refund, cannot rotate keys |
| `viewer` as membership role | **Does not exist** on One | Invite rejects it |

Confusing these three is how NP-ONE-021 gets implemented as a lie.

### 5.4 Old Pay vocabulary (museum)

`WorkspaceStaffRoles`: `ADMIN`, `MEMBER`, `VIEWER`, plus JWT `SUPER_ADMIN` / `CLIENT`.

`AuthAndCorsExtensions`:

| Policy | Roles | Old intent |
|--------|-------|------------|
| `OrgAdmin` | SUPER_ADMIN, ADMIN | keys, gateways, members |
| `OrgMember` | SUPER_ADMIN, ADMIN, MEMBER | commerce writes, refunds — **not VIEWER** |
| `OrgRead` | SUPER_ADMIN, ADMIN, MEMBER, VIEWER | GET / list |

`TenantSecurityMiddleware` bound tenant from **`X-Tenant-Id`** (or slug) and injected membership as `ClaimTypes.Role`. Path was not SoT. That is the dual-JWT mess NP-XX-008 refuses.

New Pay chrome must not emit `VIEWER` / `ADMIN` uppercase just to keep ops CSS. Speak One’s strings.

---

## 6. Mapping of One roles to Pay chrome (`whoami` / `tenants[].role`)

### 6.1 Directory vs authorization

| Question | Source | Authorize with it? |
|----------|--------|--------------------|
| Who is this human? | Zitadel access_token `sub` (never `id_token` as Bearer) | Identity only |
| Which workspaces? What role for chrome? | `GET /api/v1/me` → `tenants[].role`, `tenants[].permissions`, `active_tenant_id`, `active_role` | **No** — directory / chrome |
| May this human hit this Pay merchant route? | Path `{tenantId}` + `POST …/authz/check` | **Yes** |
| Zitadel `urn:zitadel:iam:org:project:roles` | May appear on JWT if `accessTokenRoleAssertion` | **Never parse** (NP-XX-024, NP-ONE-008) |

Dogfood §11.3: **Do not cache membership from `/me` alone for authorization.** `/me` is directory. `check` is authz when FGA is enabled. `/me` can also **write** (domain auto-join, SSO JIT). Do not hammer it from a hot loop (NP-ONE-006).

Pay `whoami` (when it exists) should be a thin projection of `GET /me`, not a second role store. Suggested field: `tenants[].role` copied as One sent it (`owner` | `admin` | `member`). Do not uppercase. Do not invent `viewer`.

### 6.2 `GET /me` shape (TypeSpec `MeResponse` / `TenantSummary`)

```json
{
  "user_id": "{zitadel sub}",
  "email": "ada@acme.test",
  "name": "Ada",
  "is_platform_admin": false,
  "tenants": [
    {
      "id": "{tenant uuid}",
      "slug": "acme",
      "name": "Acme",
      "role": "owner",
      "status": "active",
      "permissions": ["tenant:update", "tenant:delete", "domains:manage", "…"]
    }
  ],
  "active_tenant_id": "{uuid}",
  "active_role": "owner"
}
```

`role` / `active_role` comments in TypeSpec: **`owner | admin | member`**. API keys: `user_id` is the key GUID; `tenants` is the bound workspace 0–1; role is scope-derived (`admin` if key has `admin`/`*`, else `member`); keys are never `owner`; `is_platform_admin` is never true.

`active_tenant_id` / `active_role` are set only when `X-Lazuar-Tenant-Id` matches an active membership (JWT) or the key’s bound tenant. Bad hint → fields omitted, response still 200. **Never authorize from these fields alone.**

`MeEndpoints.GetMe` loads SQL memberships, projects `Role = x.Membership.Role`, then `ActiveTenantHint.TryGet`. It does not read FGA. A join-without-ticket incident can list a tenant on `/me` while `authz/check` returns `allowed: false` (issue 034). Pay chrome may show the workspace; Pay admin routes must still 403 on check deny.

### 6.3 Chrome mapping table

| One `tenants[].role` | FGA tenant relations that Check allows | Pay chrome label | Old Pay closest | Dummy `check(member)` | S1 keys / gateway paste | S1 refund / charge-from-ops |
|----------------------|----------------------------------------|------------------|-----------------|------------------------|-------------------------|-----------------------------|
| `owner` | owner, admin, member, can_view, can_manage_* | Owner | ADMIN (workspace owner) | allow | allow (`check(admin)`) | allow (`check(member)`) until VIEWER exists |
| `admin` | admin, member, can_view, can_manage_* | Admin | ADMIN | allow | allow | allow |
| `member` | member, can_view | Member | MEMBER | allow | **deny** (`check(admin)` false) | allow on One today — see §10 |
| `member` + custom role | still member | Member; `permissions[]` only for One-settings embed | none | allow | deny | allow |
| *(no such role)* | — | do **not** paint Viewer | VIEWER | n/a | n/a | n/a |
| API key `admin` | (key is not an FGA user; check needs `user_id`) | Admin (chrome only) | API_CLIENT + admin scopes | n/a on dummy human route | key mint is One, not Pay | worker uses `lzr_sk_` on Pay `/v1`, not this check |
| `is_platform_admin` without membership | may call façade (gate lets staff in) but Check local/FGA is false if no row | do not treat as merchant | SUPER_ADMIN | **deny** dummy if `allowed=false` | staff uses `lazuar-admin`, not Pay | — |

`lazuar-app` already maps labels this way (`roleLabel`: Owner / Admin / Member; unknown → title case; empty → `'Member'`). Pay chrome should copy that, not old ops chips.

`isWorkspaceAdmin` in `lazuar-app`: owner or admin. That is One-settings (invite, roles). Pay keys/gateways should use **`check(admin)`**, not `/me.role === 'admin'` cached.

### 6.4 NP-ONE-022 (invited MEMBER can see merchant ops)

Invite as `member` (copy-link). After accept:

- `/me.tenants[]` contains the tenant with `role: "member"`.
- Dummy admin `check(member)` → 200 `{allowed:true}` (once FGA matches SQL).
- Pay paints merchant ops. That is the dogfood second engineer.

Do not invite as `admin` just to make the dummy pass. `member` is the relation NP-ONE-015 names for merchant admin **read** of the dummy. Money writes are a stricter question (§10).

### 6.5 Do not mint Pay JWT roles

NP-XX-008: refuse dual JWT vs membership. Pay’s session is the One access_token. Pay may keep an opaque cookie that holds that token. Pay must not append `ClaimTypes.Role = MEMBER` from a header the way `TenantSecurityMiddleware` did. Chrome reads `/me`. Routes call `authz/check`.

---

## 7. Path `{tenantId}` is SoT; `X-Lazuar-Tenant-Id` is hint only

### 7.1 One’s rule (copy this)

`ActiveTenantHint.cs`:

```csharp
/// Optional inbound workspace hint. Never authorize from this header alone.
/// Path {tenantId} + TenantAccessService stay SoT.
public const string HeaderName = "X-Lazuar-Tenant-Id";
```

`TenantAccessService` comment: never trust `X-Tenant-Id` alone. (One does not read `X-Tenant-Id` at all. Old Pay did.)

011 `02`:

> Path `{tenantId}` + membership is Authorization SoT. `X-Lazuar-Tenant-Id` is a **hint only**. Never authorize by header alone.

Dogfood §6.3 same sentence. NP-ONE-007 same sentence.

### 7.2 What the header is for

- First-party SPA sends it so `GET /me` can fill `active_tenant_id` / `active_role` for the switcher.
- `lazuar-app` writes cookie `lazuar_active_tenant` and `apiFetch` copies it to the header.
- `@lazuar/one-client` `getTenantId()` sets the header. Comment: never authorizes by itself.
- One **outbound webhooks** also send `X-Lazuar-Tenant-Id` as metadata of the event, not as a way for Pay to authorize a user.

A bad or missing hint does not 401 `/me`. A missing path `{tenantId}` on a tenant-scoped route does not fall back to the header on One. Pay must not invent that fallback.

### 7.3 Old Pay anti-pattern (do not port)

`TenantSecurityMiddleware`: if `X-Tenant-Id` parses, that GUID becomes ambient `HttpContext.Items["TenantId"]` and membership roles are injected onto the principal. Routes without a tenant in the **path** still authorized by header. Missing header → 400 `"X-Tenant-Id is required for this route."`

New Pay merchant admin routes **require `{tenantId}` in the path**. If the header is absent, still authorize on the path. If the header is present and disagrees with the path, **path wins**; do not 403 merely because the switcher is stale; do not authorize the header’s tenant.

### 7.4 403 not 404

Cross-tenant path → 403 (`Not a member of this tenant.`). Pay’s own APIs should return 403 for a merchant UUID the caller cannot check, not 404. Dogfood §11.2: Pay merchant ids will be guessed, copied from emails, left in HAR files.

Platform staff (`is_platform_admin`) is a different gate on One. Pay dummy merchant-admin is **not** a staff route. If a platform admin is not a member, Check should fail closed for Pay’s merchant fixture (`allowed: false` → Pay 403). Do not special-case `is_platform_admin` on Pay money or dummy admin.

---

## 8. First dummy admin route algorithm

This is S0 step 5 (NP-ONE-014 / 015, 011 `03` step 5, dogfood §6.11 step 5). Focused Pay today has only `GET /v1/health`. The dummy is the first **merchant** route. It does not charge, mint keys, or read a ledger.

Suggested path (analysis, not TypeSpec yet):

```http
GET /v1/tenants/{tenantId}/ops/ping
Authorization: Bearer <user access_token>
```

Fixture on success (example):

```json
{
  "ok": true,
  "tenant_id": "{tenantId}"
}
```

No catalog, no secrets, no “you are admin” lie — the gate is **member**, so owners and members both pass.

### 8.1 Algorithm (Pay host)

```
DummyMerchantAdmin(request):
  1. If Authorization missing or not Bearer → 401.
     Reject id_token-shaped calls the same as any bad bearer (NP-ONE-003).
     Do not accept lzr_sk_ on this human dummy unless a later slice
     explicitly documents M2M ping (then user_id is required on One).

  2. tenantId = path parameter.
     If missing or not a UUID → 400.
     Do not read tenant id from query, body, or header for this step.

  3. hint = request header "X-Lazuar-Tenant-Id" (optional).
     Parse with the same rules as One ActiveTenantHint (trim, Guid.TryParse).
     If hint is present and hint != tenantId:
       log a mismatch at debug; continue.
     Never: if hint is absent → 400.
     Never: authorize hint instead of path.
     Never: require hint == path.

  4. POST {ONE}/api/v1/tenants/{tenantId}/authz/check
     Headers:
       Authorization: copy the caller's Bearer (access_token)
       Content-Type: application/json
       Accept: application/json
       (optional) X-Lazuar-Tenant-Id: tenantId   // hint for One /me, unused by check
     Body (exact):
       {
         "relation": "member",
         "object": { "type": "tenant", "id": "<path tenantId as string>" }
       }
     Do not set user_id (user JWT).
     Do not send type "app" | "payment" | "document".
     Do not send relation "viewer" | "admin" | "owner" on this dummy.
     Time out; on transport error treat as 503 (fail closed).

  5. Map One’s response to Pay’s response:

     | One                          | Pay                         |
     |------------------------------|-----------------------------|
     | 401                          | 401                         |
     | 403                          | 403 (no fixture body)       |
     | 400                          | 400 (Pay built a bad check) |
     | 429                          | 429 (One `RateLimitPolicies` policy `"authz"`, default 30/window — do not retry-storm) |
     | 503                          | 503 fail closed             |
     | 5xx other / timeout          | 503 fail closed             |
     | 200 + allowed === true       | 200 fixture                 |
     | 200 + allowed === false      | 403 (deny; no fixture)      |
     | 200 + missing/invalid JSON   | 503 fail closed             |

     Do not translate One 403 into 404.
     Do not return 200 fixture on allowed:false.
     Do not ignore 503 (or 429) and fall back to "/me lists this tenant".

  6. Return 200 fixture only after step 5 said allow.
     Fixture may echo tenant_id from the **path**, not from the header.
```

That is the whole dummy. Stop. Do not check `admin`. Do not batch-check. Do not subscribe to webhooks in this handler. Do not write Pay DB.

### 8.2 Why `member`, not `admin`

First-slice and dogfood both say **`check` `member` before merchant admin routes**. NP-ONE-022 needs the invited second engineer (a `member`) to see ops. If the dummy checked `admin`, NP-ONE-022 would fail and Ada would “fix” it by inviting everyone as admin.

`admin` / `owner` checks belong on **later** routes: rotate gateway keys, mint `lzr_sk_` (that mint is One’s API with One’s own admin gate), transfer billing owner. Same façade, different `relation`.

### 8.3 Why 200 `{allowed:false}` is Pay 403

One’s façade returns 200 for “I ran the check and the answer is no” when the **caller** is a member (or a key on that tenant) but the **subject** lacks the relation. For the dummy, caller == subject, relation is `member`, so a member-caller getting `allowed:false` is the join-without-ticket / FGA-lag case (`/me` lists, graph denies). Dogfood §11.3: treat **200 + later 403 on check** as an incident. Pay must not 200 a fixture on that graph deny. Mapping `allowed:false` → 403 keeps Pay’s client simple: 200 means in, 403 means out.

If Pay instead 200’d `{ ok: false }`, ops UI would have to inspect bodies. Don’t. Status is the gate.

### 8.4 Suspended tenant

One membership gate 403s `"Tenant is suspended."` before Check. Pay dummy 403s. NP-ONE-018 (stop staff access on `tenant.suspended`) is a webhook plus this gate: even if the webhook is late, the next dummy/admin call still 403s once One has suspended. Money in Pay stays true if the webhook is late (011 `02`); the dummy is staff access, not money.

### 8.5 What the dummy is not

- Not `GET /me`.
- Not `list-objects`.
- Not `check` on type `app` (Pay’s OIDC app id is irrelevant to “may I see merchant ops”).
- Not a Pay policy catalog.
- Not proof of VIEWER enforcement.

---

## 9. Why Pay does not get `authz/write`

### 9.1 The endpoint does not exist

TypeSpec `AuthzOperations`: `check`, `batchCheck`, `listObjects`. No write. `AuthzEndpoints` maps those three POSTs only.

Public docs (`integrations/authz.md`):

> Public `authz/write` | **Not available** — platform dual-write on membership only  
> There is **no** public `authz/write` in v1.

AUTHZ-06 in 013 / 017 FEATURE-CHECKLIST: **N**. 017-08 §6.7: “Do not give Pay `authz/write` (AUTHZ-06 never).” 017-08 §13.2 hold list: “Public `authz/write`.” NP-XX-016: refuse. D121: no FGA admin secrets in integrator apps. D120 / D124: no unrestricted tuple write; platform types under tenant parent only.

Historical 003 papers sketched `POST …/authz/write` as privileged. It was never shipped. Pay must not “add it for documents.”

### 9.2 Who writes tuples

| Event | Writer | Tuple |
|-------|--------|-------|
| Tenant create (owner) | One `MembershipService` / provision | `user:{sub} owner tenant:{id}` (and inherited admin/member via model) |
| Invite accept / role change / transfer / remove / leave | One `WriteFgaOrUnavailableAsync` | `ToFgaRelation(role)` on `tenant:{id}` |
| Domain auto-join / SSO JIT | One join services | `member` |
| OIDC app create / revoke | One `OidcAppService` | `tenant:{tid} tenant app:{appId}` (+ deletes on revoke) |

Pay’s job on membership change is **webhook consumer** (`member.*`, `ownership.transferred`), not tuple writer. If Pay wrote `user:X member tenant:Y` it would fight the healer, skip SQL, and hold an OpenFGA admin token (NP-XX-017 refuse).

### 9.3 What Pay would be tempted to write (and must not)

- `user:{viewer} viewer tenant:{id}` — relation not on type tenant; membership role does not exist.
- `user:{sub} can_charge payment:{id}` — type `payment` not allow-listed; 400 today; adding it is AUTHZ-05/20 with Pay as named consumer **and** a real Pay check call in the same change set. This slice has no such call.
- Direct OpenFGA HTTP from Pay to `:8090` — AUTHZ-07 exists so Pay never holds store admin. Playground `:3009` is One-engineer debug, not Pay runtime (dogfood §4.9).

Pay’s ACL for **Pay resources** (a specific invoice, a specific refund) stays **in Pay** until a named AUTHZ-05 expansion ships with dual-write. That is the 015-04 ceiling: “Fine-grained authorization for *your* documents, folders, or invoices is **not** provided — keep those ACLs in your app.”

### 9.4 AUTHZ-05 / NP-XX-015 / AUTHZ-20

Named consumer of type `app` is **in-repo OIDC apps**, dual-written on app create/revoke. 017 FEATURE-CHECKLIST AUTHZ-05 stays **P**. Notes: do not add `document` / `order` to chase the tracker. 017-10 AUTHZ-20: no Pay document/order types without a named consumer. 011 NP-XX-015: add FGA types `payment` / `document` with **no written check call** → refuse.

This slice’s written check call is `tenant` + `member`. That does **not** name Pay as consumer of `payment` / `document`. Do not expand `model.fga` in One “for Pay” in the same PR as the dummy ping.

D124 text (2026-08-10) still says runtime allow-list `{ tenant }` and `model.fga` unchanged; **runtime on this One SHA has already added `app`**. Trust `AuthzObjectRules` and live `model.fga`, not the lock’s frozen “scaffolding only” sentence, for what is shipped. Trust D120/D121/AUTHZ-20 for what must **not** ship next.

---

## 10. VIEWER enforcement — honesty

### 10.1 The 011 sentence vs One runtime

NP-ONE-021: “VIEWER cannot charge, change keys, or refund. Enforce in Pay using One role + `authz`.”

011 `02` two-planes table: “Ada, invited MEMBER/VIEWER.”

011 `03` step 12: “VIEWER cannot change keys or refund.”

011 `12` pass line: “MEMBER sees ops, VIEWER cannot charge.”

**One cannot invite VIEWER. One cannot store VIEWER. One `authz/check` cannot distinguish VIEWER from MEMBER, because VIEWER is not a membership role and `check(member)` is true for every `IsValid` role.**

That is not a docs nit. If Pay implements NP-ONE-021 as `check(member)` on charge/refund, VIEWER-as-old-Pay does not exist, and every `member` can charge. If Pay implements it as `check(admin)` on charge/refund, invited MEMBER cannot refund — which breaks the old OrgMember split and may be stricter than 011 intended, but is at least expressible in One.

### 10.2 What `check` can and cannot say today

| Pay wants | One check | Result today |
|-----------|-----------|--------------|
| Any staff of this merchant (dummy ops, NP-ONE-022) | `relation=member`, `type=tenant` | owner, admin, **member** (all of them) |
| Can manage members / keys / tenant settings | `relation=admin` or `can_manage_members` / `can_manage_tenant` | owner, admin |
| Is the billing owner | `relation=owner` | owner only |
| Read-only staff (old VIEWER) | *(no relation)* | **cannot express** |
| `relation=viewer` on type tenant | 400 unsupported relation | — |
| `relation=viewer` on type app | any tenant member (plus app-specific) | **wrong noun** — OIDC app, not merchant read-only |
| Custom role “Viewer” with empty One permissions | still `check(member)=true` | **wrong** — FGA stays member |

`can_view` currently equals `member` in the model (`define can_view: member`). Using `can_view` for “read-only” does nothing.

### 10.3 What Pay should do (honest options)

**Do not:**

1. Store a Pay-side `VIEWER` flag / table. That is a second membership plane (NP-XX-014 refuse).
2. Parse Zitadel project roles and map a Console role to VIEWER (NP-XX-024).
3. Add FGA type `payment` with relation `charger` to fake VIEWER (NP-XX-015).
4. Use One custom roles as Pay VIEWER. Overlay is One settings; FGA is still `member`.
5. Paint a Viewer chip from `/me.role` — the string will never be `viewer`.
6. Mark NP-ONE-021 `done` because the dummy `check(member)` passed.

**S0 dummy (this slice):** ignore VIEWER. Gate is `member`. Owners, admins, members all get the 200 fixture. That matches NP-ONE-015 / NP-ONE-022. It does **not** match NP-ONE-021. Leave 021 `todo`.

**S1 money routes, until One grows a fourth role:**

Pay **must still enforce on money routes even though One only has coarse `member`.** Concretely:

| Pay route class | Gate | Who passes today | Old Pay |
|-----------------|------|------------------|---------|
| Dummy / read ops (payments list, receipt GET) | `check(member)` | owner, admin, member | OrgRead (included VIEWER) |
| Gateway keys paste/rotate, destructive billing profile | `check(admin)` | owner, admin | OrgAdmin (VIEWER and MEMBER denied) |
| Refund, charge-from-ops, create product | **Pay policy on top of `check(member)`** | see below | OrgMember (VIEWER denied, MEMBER allowed) |

For refund / charge-from-ops, One cannot name VIEWER. Choose one and write it on the Pay route:

- **Option A (recommended for v1 dogfood):** there is **no VIEWER in v1**. Invite `member` = old MEMBER (can see ops **and** refund/create product). Invite `admin` = old ADMIN (keys too). Document on Team copy: “Roles are Owner, Admin, Member. There is no read-only Viewer until One ships it.” NP-ONE-021 stays `todo` / blocked-on-One, not `done`. First-slice “VIEWER cannot charge” is **vacuously untestable** until a fourth role exists — do not fake a test with a custom role.
- **Option B (stricter, still honest):** refund/charge also require `check(admin)`. Then MEMBER cannot charge. That is **not** old Pay and **not** 011’s MEMBER-sees-ops-and-presumably-operates split, but it is expressible. Only do this if product explicitly wants “only admin moves money.” 011 does not say that; it says VIEWER cannot, implying MEMBER can.
- **Option C (One work, then Pay):** One adds membership role `viewer` (invite, `MembershipRoles`, FGA `define viewer: [user]` **not** included in `member`, and `can_view: viewer or member` or `can_view: viewer` with member including viewer — exact DSL is One’s). Then:
  - `check(can_view)` = OrgRead
  - `check(member)` = OrgMember (viewers fail)
  - `check(admin)` = OrgAdmin
  - Pay NP-ONE-021 = `check(member)` on charge/refund, `check(admin)` on keys
  - Pay still enforces those checks **in Pay**; it does not trust chrome.

Option C is the only way NP-ONE-021 is literally true. It is **One product-pull**, not this Pay slice. Do not sneak the role into Pay.

**Even under Option A**, Pay enforces on money routes:

- Always `authz/check` on the path tenant (never header, never `/me.role` cache).
- Keys: `admin`, not `member`.
- Charge/refund: `member` (Option A) **in Pay**, so a future One `viewer` that is **not** in `member` starts failing without Pay rewriting policy — **if and only if** One adds `viewer` outside `member`. If One mistakenly adds `viewer` as an alias of `member`, Pay cannot save it. Call that out in the One PR.

Pay must not skip the check on money routes “because the dummy already proved membership for this session.” Sessions are not a capability cache. FGA can lag; webhooks can suspend; role can change (`member.role_changed`).

### 10.4 Chrome vs API

Old Pay painted Viewer-illegal buttons that 403’d (issue 326). New Pay should hide buttons from `/me.role` **and** 403 on the API. Chrome is `owner`/`admin`/`member` only. Hide key rotation unless `role` is owner or admin **or** (later) a `batch-check` says `admin`. Do not hide refund from `member` under Option A. Do not show a Viewer-only page that One cannot populate.

NP-ONE-016 `batch-check` later: one POST with `member` + `admin` (+ `owner` if needed) to paint the ops shell. Still not authorization SoT — each mutating route re-checks.

### 10.5 Sentence to keep in the tracker notes

Suggested NP-ONE-021 note (when someone edits 011, not this file’s job to flip):

> One has no VIEWER membership role (`owner`\|`admin`\|`member` only; FGA `viewer` is type `app`). S0 dummy uses `check(member)`. S1 keys use `check(admin)`. Charge/refund cannot deny a Viewer until One adds that role; do not fake it with custom roles or Pay-side membership.

---

## 11. `batch-check` later (NP-ONE-016)

Not this slice.

When chrome needs several booleans (show Refund, show Rotate key, show Transfer owner):

```json
{
  "checks": [
    { "relation": "member", "object": { "type": "tenant", "id": "{tenantId}" } },
    { "relation": "admin",  "object": { "type": "tenant", "id": "{tenantId}" } },
    { "relation": "owner",  "object": { "type": "tenant", "id": "{tenantId}" } }
  ]
}
```

Max 50. Same allow-list. Same 403-if-not-a-member-of-path. Results are positional booleans, no relation echo — Pay must zip by index.

Do not batch `type: payment`. Do not use batch as a substitute for per-route check on POST refund. Chrome may be stale; the money POST still checks.

No `authz/write` in that later slice either.

---

## 12. Tests: mocked One 200 allow vs 403 deny

Pay does not boot One or OpenFGA in the dummy-route unit/integration tests. Stub the HTTP call to `POST /api/v1/tenants/{tenantId}/authz/check`.

Tenant A = `11111111-1111-1111-1111-111111111111`  
Tenant B = `22222222-2222-2222-2222-222222222222`  
User Ada has a bearer `access_token` (opaque in the test; Pay must forward it).

### 12.1 Allow — mocked One 200 `{ "allowed": true }`

- Arrange: Pay route `GET /v1/tenants/{A}/ops/ping` (or whatever path lands). Mock One: if method POST, path `/api/v1/tenants/{A}/authz/check`, Authorization echoed, JSON body `relation==member` and `object.type==tenant` and `object.id==A` → **200** `{ "allowed": true }`.
- Act: Ada GET dummy with Bearer, path A. Header absent **or** `X-Lazuar-Tenant-Id: A`.
- Assert:
  - Pay **200**.
  - Fixture body (e.g. `ok: true`, `tenant_id` == A).
  - One was called **once**.
  - Request body did **not** contain `"payment"` or `"document"`.
  - Request did **not** set `user_id` (user JWT).

### 12.2 Deny — mocked One 403 (not a member)

- Arrange: mock One POST `/api/v1/tenants/{B}/authz/check` → **403** ProblemDetails `{ "title": "Forbidden", "status": 403, "detail": "Not a member of this tenant." }` (ISO-09 shape).
- Act: Ada GET dummy path **B** (Ada is not a member of B).
- Assert:
  - Pay **403**, not 404, not 200.
  - No fixture `ok: true`.
  - One was called with path B, not with header-derived id.

This is the test the prompt names. It is the stranger / wrong-UUID case.

### 12.3 Deny — mocked One 200 `{ "allowed": false }`

- Arrange: mock 200 `{ "allowed": false }` on path A (FGA lag / join-without-ticket).
- Act: Ada GET dummy path A, even if a stub `/me` would list A.
- Assert: Pay **403**. Do not 200 fixture. This locks §8.3.

### 12.4 Header is hint only

- **12.4a** Path A + header B + One allow on A: Pay **200**. Mock must receive `{A}` in the URL, not B. Header B must not become the check tenant.
- **12.4b** Path B + header A + One 403 on B: Pay **403**. Do not “helpfully” check A because the header says Ada’s real workspace.
- **12.4c** Path A, **no** header, One allow on A: Pay **200**. Missing header is not 400 (old Pay would 400 `X-Tenant-Id is required`).

### 12.5 Fail closed

- Mock One **503** `{ "title": "…", "detail": "Authorization service is unavailable. Fail-closed." }` → Pay **503**, no fixture.
- Mock timeout / connection refused → Pay **503**.
- Mock 200 with body `{}` or `"allowed"` missing → 503, not allow.
- Mock One **429** → Pay **429**, no fixture (One `authz` policy, default 30/60s). Do not retry in the handler.

### 12.6 Pay must not call the wrong type

If a helper builder is used, a test (unit on the client mapper) asserts the serialized body is exactly the tenant/member JSON. A mutation that sends `type: "payment"` is a fail even if the mock would 200.

### 12.7 What not to test in this slice

- Live OpenFGA.
- `batch-check`.
- `list-objects`.
- VIEWER invite (cannot).
- Zitadel project-role claims on the token (Pay must not parse them; a test that puts `urn:zitadel:iam:org:project:roles` on a fake JWT and still only calls `authz/check` is optional armor for NP-XX-024).

Hermetic One tests that already lock the façade (do not rewrite them in Pay): `AuthzFacadeTests`, `ISO_09_AuthzCheck_cross_tenant_403`, `ISO_Z_unknown_type_rejected`. Pay trusts those by SHA; Pay tests mock the HTTP.

---

## 13. Dogfood §6.7 (quoted, then Pay implications)

One `08-dogfood-then-serve.md` §6.7:

| HTTP | Path | Pay use |
|------|------|---------|
| `POST` | `/tenants/{id}/authz/check` | Can this user `member` / `admin` / `owner` this tenant? Can they `viewer` this `app`? |
| `POST` | `/tenants/{id}/authz/batch-check` | Permission chrome |
| `POST` | `/tenants/{id}/authz/list-objects` | `type=app` inventory; **not** workspace inventory (`type=tenant` is 0/1) |

> Allow-list is `{ tenant, app }`. If Pay needs `payment` or `merchant_document`, that is AUTHZ-05 **with Pay as named consumer**. Do not add the type in One because the matrix is empty. Do not give Pay `authz/write` (AUTHZ-06 never).
>
> Custom permissions (`sso:manage`, etc.) are SQL catalog + `TenantPermission.Allows`. FGA stays coarse. Pay should not expect a JWT permission array.

Pay implications, unsoftened:

- The `viewer this app` column is **OIDC app**, not merchant Viewer. First dummy does not ask it.
- `payment` / `merchant_document` are named so we **do not** add them in this slice.
- Custom permissions and JWT permission arrays are the wrong place to hang refund. There is no JWT permission array for Pay to expect.

§6.11 step 5: `check` `member` before Pay-side admin routes. Step 7: **Stop.** No custom FGA types.

§4.9: Pay should call this façade. Pay should not open the OpenFGA playground except as a One engineer debugging.

§11.3: 503 on invite accept = retry the verb. 200 join + later 403 on check = incident. Do not cache `/me` for authz.

---

## 14. Dual-write lag Pay must live with

Order on One membership mutations: **SQL `SaveChanges` then FGA**. FGA fail → **503 after commit** on accept / role / transfer / remove / leave. Product routes follow SQL; `/authz` may lag until healer / retry-provision.

Residuals (017-08 §11.4): join / JIT / auto-join can **200** if both FGA and ticket commit fail (issue 034). `/me` lists; `/authz` denies.

Pay dummy algorithm maps that deny to 403. Support symptom: “I accepted, I see the workspace in the switcher, ping 403s.” That is One A6 / healer work, not a Pay bypass. Do not “fix” by authorizing from `/me`.

Healer is staff `POST /api/v1/platform/tenants/{id}/reconcile-fga`. Pay does not call platform tenants (NP-XX-023). Pay does not hold the FGA admin token to self-heal.

---

## 15. Zitadel project roles (NP-XX-024)

Do not parse `urn:zitadel:iam:org:project:roles`. One JWT middleware does not authorize on those claims. ROLE-04 is `/me` (`tenants[].role` + `active_role`). An optional Zitadel Action snippet in One docs may add claims for **the integrator’s own APIs**; One ignores them. Pay must ignore them too, even if a future Pay Action mints roles for a **different** Pay resource server — merchant membership stays `/me` + `authz/check`.

NP-XX-008: refuse dual JWT vs membership roles.

---

## 16. What Pay must not do (this slice)

1. POST `object.type` other than `tenant` (dummy) — especially not `payment` / `document`.
2. Call `authz/write` or OpenFGA HTTP.
3. Hold Zitadel PAT / OpenFGA admin / login PAT.
4. Authorize from `X-Lazuar-Tenant-Id` or old `X-Tenant-Id`.
5. Authorize from `/me.tenants[].role` or `permissions[]` or `active_role`.
6. Parse Zitadel project-role claims.
7. Copy `TenantSecurityMiddleware` header ambient tenant + injected `VIEWER` claims.
8. Implement `batch-check` as a blocker for the dummy.
9. Use `check(viewer)` on an OIDC app as staff read-only.
10. Invite a custom role and call it Viewer for money policy.
11. Return 200 fixture on One 403 or on `{allowed:false}`.
12. Return 404 on cross-tenant dummy.
13. Fail open on One 503 or 429.
14. Mark NP-ONE-021 done.

---

## 17. Implementer notes (when a later slice builds this)

Not this paper’s job to land code. When it does:

- Focused Pay TypeSpec lives in `packages/pay-spec` (not old `packages/api-spec`). Grow it when the dummy exists; today it is only `GET /v1/health` on `:8081`.
- Forward **access_token** only (`Authorization: Bearer`). Same token One minted via PKCE for Pay’s `client_id`.
- One base URL in dev: `http://localhost:8080/api/v1` (011 `02`, dogfood §6.2).
- Prefer workspace `@lazuar/one-client` `check()`; raw fetch is allowed if the body matches §3.2.
- Path tenant id **is** Pay `org_id` (NP-ONE-009). No second org table.
- Rate limits: One **does** limit `POST …/authz/check|batch-check|list-objects` (`RateLimitPolicies.Match` → policy `"authz"`, `RateLimitOptions.AuthzPerWindow` default **30 per `WindowSeconds` (60s)** per user or IP). Dummy ping once per page load is fine; a hot loop on every React render will 429. Simplest S0: no allow-cache; do not check from a render loop. Do not cache allows across `member.role_changed`. If One 429s, Pay 429s — do not fail open.
- Suspended tenant 403 from One is enough for the dummy; charge-stop on webhook is NP-ONE-018, another paper.

---

## 18. Traceability matrix

| Must-contain item | Where in this paper |
|-------------------|---------------------|
| Title, date, SHAs | header |
| Exact check request/response as implemented | §3 |
| Mapping of One roles to Pay chrome (`whoami` `tenants[].role`) | §6 |
| First dummy admin route algorithm: path tenantId, check member, then 200 fixture | §8 |
| Header `X-Lazuar-Tenant-Id` is hint only | §7 |
| Why Pay does not get `authz/write` | §9 |
| VIEWER enforcement even if One only has coarse member; honesty if roles don’t match | §10 |
| Tests: mocked One 200 allow vs 403 deny | §12 |
| Allow-list `{tenant, app}`; no `payment`/`document` | §4.2, §9.4, §16 |
| Do not parse Zitadel project roles | §15, NP-XX-024 |
| `batch-check` later | §11, NP-ONE-016 |
| Evidence from One code | §1.3, §4, §5, citations throughout |

---

## 19. Bottom line

Pay’s first authorization call is a single POST to One:

```json
POST /api/v1/tenants/{pathTenantId}/authz/check
{ "relation": "member", "object": { "type": "tenant", "id": "{pathTenantId}" } }
```

200 `{allowed:true}` → dummy 200 fixture. One 403 or `{allowed:false}` or 503 → no fixture. The path is SoT; `X-Lazuar-Tenant-Id` is a switcher hint. Roles in chrome are `/me` `owner|admin|member`. There is no One VIEWER; do not add FGA `payment`/`document`; do not call `authz/write`; do not parse Zitadel project roles; batch-check waits. Money routes still have to call Check themselves, with `admin` for keys, and they **cannot** honestly deny a Viewer until One has that role — enforce in Pay anyway, and do not pretend `check(member)` is a Viewer gate.
