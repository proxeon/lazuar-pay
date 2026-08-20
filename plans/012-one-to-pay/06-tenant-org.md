# 06 — Merchant identity: One tenant id is Pay `org_id`

**Family:** 012-one-to-pay  
**Paper:** 06 — tenancy / merchant org  
**Date:** 20 August 2026  
**Type:** Analysis only. **Do not implement** from this file. **Do not** add a Pay `organizations` table, a Pay `users` table, or a Pay cookie session to “unblock” S0.  
**Sibling identity:** `/Users/akmalfirdaus/Code/lazuar/lazuar-one`  
**Old tree (negative example only):** `apps/lazuar-api/Modules/One/`

**SHAs considered (this write):**

| Repo | Full SHA | Short | Tip |
|------|----------|-------|-----|
| **lazuar-pay** | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` | `6ca8f19f` | `feat(pay): add TypeSpec package for the focused Pay host` (2026-08-20 21:00:06 +0800) |
| **lazuar-one** | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` | `0f79fe4` | `WIP: Thu Aug 20 21:24:22 +08 2026` (2026-08-20 21:24:22 +0800) |

011 papers cited below are dated 20 August 2026 on this Pay tree. One TypeSpec and handlers are on the One SHA above. Old Pay `Modules/One` is cited as a **refuse template**, not as a source of truth for new Pay.

Sibling in this family: [01-one-http-surface.md](./01-one-http-surface.md) lists which One routes Pay may call. This paper is the **id and storage** story those routes hang off — not a second route catalog.

---

## 0. How to read this paper

This paper answers one question and refuses a second:

1. **How does focused Pay know which merchant it is talking about?**
2. **Must Pay store a merchant identity of its own?**

The answer is already written in [011 02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) and locked in [011 11-checklist.md](../011-new-lazuar-pay/11-checklist.md) as **NP-ONE-009** and **NP-XX-014**. This paper unrolls that lock so an implementer cannot “helpfully” add `pay.organizations` “just for a name column,” a mapping table “just in case One’s UUID is not ours,” or a header-only tenant the way old Pay did.

It is **not** an implement order. It is **not** a schema dump. It does not flip tracker Status. It does not invent a Pay whoami against Pay’s own database.

Must-reads for this slice (already opened for this write):

| Source | What it contributes here |
|--------|--------------------------|
| [011 02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) § Session and active workspace; Tenancy HTTP; Events; Two planes | Path `{tenantId}` + membership is authz SoT. Header is a hint. One tenant id **is** Pay `org_id`. Create workspace = `POST /tenants`. Two planes. |
| [011 00-why-leave.md](../011-new-lazuar-pay/00-why-leave.md) | “Register said `ADMIN` in JSON and stamped `CLIENT` on the cookie.” Dual role vocabularies are a reason we left. |
| [011 01-product.md](../011-new-lazuar-pay/01-product.md) | Merchant staff live in One. Buyer / payer is Pay. Cardholders never become Zitadel humans. |
| [011 03-first-slice.md](../011-new-lazuar-pay/03-first-slice.md) / [12-first-slice-tracker.md](../011-new-lazuar-pay/12-first-slice-tracker.md) | S0 step 3: create workspace = `POST /tenants`. Fail if Pay password form or second org table. S1 is money rows. |
| [011 11-checklist.md](../011-new-lazuar-pay/11-checklist.md) **NP-ONE-009**, **NP-XX-014** | Create workspace = `POST /tenants`; One tenant id **is** Pay `org_id`. Refuse a second `organizations` table plus One members. |
| One TypeSpec `packages/api-spec/modules/tenants/{models,routes}.tsp` | `POST /tenants`, `GET /tenants/{tenantId}` with `status`, `id: string`. |
| One TypeSpec `packages/api-spec/modules/platform/{models,routes}.tsp` | `GET /me` returns `tenants[]`, `active_tenant_id`. |
| Old `apps/lazuar-api/Modules/One/` | Negative: `GlobalUser`, `Organization`, cookie `lazuar_auth` role `CLIENT` vs membership `ADMIN`, `X-Tenant-Id` ambient tenant. |

---

## 1. Binding answers (read this first)

These are the decisions this paper exists to keep from being “clarified” into a second identity system.

| # | Decision | Lock |
|---|----------|------|
| 1 | **One tenant UUID is Pay `org_id`.** Same bytes. Same string in JSON. No Pay-side surrogate. | NP-ONE-009 |
| 2 | **Pay does not have an `organizations` / `tenants` / `workspaces` table in the first slice.** Merchant existence, name, slug, logo, status, membership live in One. | NP-XX-014 |
| 3 | **Pay money rows (S1) store that id as a column** (`org_id` / `tenant_id` — pick one name and keep it). That column is a **copy of One’s id**, not a foreign key into a Pay org row. | this paper §6 |
| 4 | **Authorization SoT for merchant routes is the path `{tenantId}` plus One membership** (`GET /me`, `POST /tenants/{id}/authz/check`). `X-Lazuar-Tenant-Id` is a **hint only**. Never authorize by header alone. | NP-ONE-007 |
| 5 | **“Create workspace” in Pay UI is `POST /api/v1/tenants` on One**, Bearer = user’s **access_token**. Caller becomes **owner**. Or the user picks an existing membership from `GET /me`. | NP-ONE-009 |
| 6 | **Pay whoami is One `GET /me`.** It returns One tenants. Pay does not implement `GET /one/auth/me` against `GlobalUsers`. | NP-ONE-006 |
| 7 | **Pay-side rows appear at S1**, when the merchant creates a product or a charge exists — not at S0 tenant create, and not as empty “provisioned catalog” shells. | this paper §6 vs NP-ONE-019 |
| 8 | **Suspend:** first slice may `GET /tenants/{id}` (One exposes `status`). Stop charges on `tenant.suspended` is a **later webhook** (NP-ONE-017 / NP-ONE-018), not a Pay org row flip. | this paper §8 |
| 9 | **A mapping table is a last resort.** The written reason is §7. Until that reason is true in production, do not add `pay.org_map`. | NP-XX-014 |
| 10 | **Do not copy old Pay `Modules/One`:** no `GlobalUser`, no password hash, no `lazuar_auth` / `lazuar_admin_auth`, no JWT role `CLIENT` vs membership `ADMIN`, no `TenantAppEntitlement`, no `X-Tenant-Id` ambient `HttpContext.Items["TenantId"]`. | 00-why-leave, this paper §10 |

If an implementation PR adds `CREATE TABLE organizations` “for convenience,” that PR fails this paper and fails the first-slice lock in [03](../011-new-lazuar-pay/03-first-slice.md): *“Pay password form or second org table.”*

---

## 2. Two planes: merchant staff (One) vs buyer (Pay)

[01-product.md](../011-new-lazuar-pay/01-product.md) and [02-one-integration.md](../011-new-lazuar-pay/02-one-integration.md) already split the world. Restated here because mixing the planes is how old Pay grew `GlobalUser` + `CLIENT` JWT + CRM `GlobalUserId` on a buyer profile.

### 2.1 Merchant staff plane — One

| Fact | System |
|------|--------|
| Human identity | Zitadel human, surfaced by One. `GET /me.user_id` is Zitadel `sub`. |
| Workspace / org | One `tenants` row. `id` is a UUID (`Guid.CreateVersion7()` on create). JSON `id: string`. |
| Membership / role | One `memberships`: `owner` \| `admin` \| `member` (TypeSpec `MembershipRole`). Custom roles are a closed overlay on One’s own settings routes, not Pay ACLs. |
| Invite | One `POST /tenants/{id}/members/invite` + copy-link. Non-email accept path stays One. |
| Login | OIDC code + PKCE against Zitadel authority. Product login is **`:5175`**. Pay never ships a password form. |
| Machine | One `lzr_sk_` bound to **one** tenant. `GET /me` for a key: `user_id` is the key GUID, `tenants` is 0–1 bound workspace. |
| Staff access to Pay ops | One membership + Pay `authz/check` `member` / `admin` / `owner` before merchant admin routes (NP-ONE-015, NP-ONE-021, NP-ONE-022). VIEWER-class in old Pay ≈ One `member` with a custom role, or simply “not admin” — Pay enforces charge/key/refund using One role, not a Pay `roles` table. |

Ada (merchant owner) is a One human. The engineer she invites is a One human. They never get a Pay password.

### 2.2 Buyer / payer plane — Pay (later than S0; first money at S1)

| Fact | System |
|------|--------|
| Payer | Email / name on the checkout session. Small payer profile **inside Pay** (NP-BUY-001, NP-BUY-002). |
| Access after pay | Pay subscription / session row (NP-FUL-002). **Not** a One membership. **Not** a Zitadel human. |
| Magic link / receipts | To the **payer** mailbox (NP-BUY-003). Commerce-old `IMagicLinkTokenService` was already the buyer portal, not staff SSO — steal that *job*, not the token-subject bugs. |
| Hosted page | Cash register. Buyer pays **without** a One account (NP-CHK-007). Fail the slice if checkout requires Zitadel login. |

[02](../011-new-lazuar-pay/02-one-integration.md) sentence that must stay true:

> Cardholders never become Zitadel users because they bought an ebook.

NP-XX-013: create a Zitadel human per cardholder = refuse.

### 2.3 What “whoami” means on each plane

| Caller | Whoami | Returns |
|--------|--------|---------|
| Merchant SPA (Pay ops UI) | **One** `GET /api/v1/me` with `Authorization: Bearer <access_token>` | `user_id`, email, name, `tenants[]` (`id`, `slug`, `name`, `role`, `status`, `permissions`), `active_tenant_id` if `X-Lazuar-Tenant-Id` matches a membership, `is_platform_admin` |
| Merchant worker | One `GET /me` with `lzr_sk_` | Bound tenant only. Header ignored. `user_id` = key id. |
| Buyer on hosted page | **Not whoami.** Checkout session id + (later) magic link to payer email. | No One call. |
| Old Pay `GET /one/auth/me` | Cookie JWT → `GlobalUsers` row + `CLIENT`/`SUPER_ADMIN` role | **Do not rebuild.** |

Pay may expose a **BFF convenience** `GET /v1/whoami` that **proxies One `/me`** (same Bearer, same JSON shape or a thin wrapper). That is still One’s list of tenants. It is not a Pay org catalog. If the BFF caches, the cache is a snapshot with a short TTL; membership SoT remains One.

Do **not** hammer `GET /me` from a hot loop ([02](../011-new-lazuar-pay/02-one-integration.md): `/me` can **write** — domain auto-join, SSO JIT). One’s handler (`MeEndpoints.GetMe`) calls `IDomainJoinService.AutoJoinAsync` / `ISsoJoinService.AutoJoinAsync` when `email_verified == true`. Pay chrome should call it on session start and workspace switch, not on every keystroke.

### 2.4 Roles: One’s vocabulary vs old Pay’s (do not translate by string-equal)

| One (product) | Old Pay membership | Old Pay JWT cookie | Pay’s job |
|---------------|--------------------|--------------------|-----------|
| `owner` | (no equivalent; first member was `ADMIN`) | n/a | Billing owner; wipe; transfer |
| `admin` | `ADMIN` | n/a | Keys, refunds, invites (via One) |
| `member` | `MEMBER` / `VIEWER` mashed | n/a | See ops; cannot charge if Pay treats as VIEWER-class (NP-ONE-021) |
| (none) | (invite rejected `CLIENT`) | **`CLIENT`** on every merchant cookie | **Do not create this layer** |
| platform email list | `IsSystemAdmin` / genesis `SUPER_ADMIN` | `SUPER_ADMIN` on cookie | One `is_platform_admin`; Pay does not mint this |

One TypeSpec `MembershipRole` is lowercase `owner | admin | member`. Old Pay `WorkspaceStaffRoles` is uppercase `ADMIN | MEMBER | VIEWER` plus genesis `SUPER_ADMIN`. Mapping by `ToUpperInvariant()` will produce a staff role that One’s FGA does not know. Pay must consume **One’s strings** (and `authz/check`), not reimplement `WorkspaceStaffRoles`.

---

## 3. Mapping rule: One tenant UUID / string **is** `org_id`

### 3.1 What One actually stores and returns

One domain (`lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Domain/Tenants/Tenant.cs`):

- `Id` is `Guid`. Create path: `Guid.CreateVersion7()` in `TenantService.CreateAsync`.
- `Slug`, `Name`, `Status` (`provisioning` \| `active` \| `failed` \| `suspended` \| `deleted`), optional `ZitadelOrgId`, `Metadata`, `LogoUrl`.

TypeSpec (`packages/api-spec/modules/tenants/models.tsp`):

```tsp
model Tenant {
  id: string;
  slug: string;
  name: string;
  status: TenantStatus;
  zitadel_org_id?: string;
  ...
}
```

HTTP:

- Create: `POST /api/v1/tenants` → 201, `Location: /api/v1/tenants/{tenant.Id}`, body includes `id` as **guid string**.
- Get: `GET /api/v1/tenants/{tenantId}` — TypeSpec path param is `string`; **implementation** constrains `{tenantId:guid}`.
- List: `GET /api/v1/tenants` membership-scoped. `GET /me.tenants[].id` is the same string.

There is no second public identifier. Slug is unique and human, **not** the FK. `zitadel_org_id` is One’s mapping to **Zitadel**, which Pay must never hold or join on (NP-ONE-020, NP-XX-017). Pay’s `org_id` is One’s `tenants.id`, not Zitadel’s org.

### 3.2 The rule in one sentence

> For every Pay row that is merchant-scoped, `org_id` (or `tenant_id` — **one column name in Pay, used everywhere**) equals the One tenant UUID that `POST /tenants` returned and that `GET /me.tenants[].id` lists. There is no translation step.

UUID/string duality is serialization, not mapping:

| Layer | Type |
|-------|------|
| One Postgres `tenants.id` | `uuid` |
| One C# `Tenant.Id` | `Guid` |
| One JSON / TypeSpec | `string` (guid text) |
| Pay HTTP path `{tenantId}` | `string` that **must parse as UUID** |
| Pay Postgres money tables | `uuid` **equal to** One’s uuid |
| Pay logs / receipts metadata | the same guid string |

Do not store slug as the isolation key. Slugs change (`PATCH /tenants/{id}`). Do not store `zitadel_org_id`. Do not store “Pay org number 1, 2, 3” and map.

### 3.3 Why 011 says “unless Pay writes a reason to map otherwise”

[02](../011-new-lazuar-pay/02-one-integration.md):

> One tenant id **is** Pay’s `org_id` unless Pay writes a reason to map otherwise. Do not invent a second membership system “just for merchants” and also use One members.

This paper **is** that written surface. The reason to map otherwise is **not present**. §7 is the last-resort essay for a future day when it might be. Until then NP-XX-014 holds.

### 3.4 What “FK to that id” means without an org table

Old Commerce `Product` (`Modules/Commerce/Domain/Aggregates/Product.cs`):

```csharp
public class Product : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid OrganizationId { get; set; }
    ...
}
```

`IMustHaveTenant` is `Guid OrganizationId { get; set; }` with an EF global filter against ambient `HttpContext` tenant. That **shape** (a uuid column on the money row) is correct. The **ambient filter + org table behind it** is not.

New Pay:

```text
products.org_id      uuid  -- One tenants.id, no REFERENCES pay.organizations
charges.org_id       uuid  -- same
checkouts.org_id     uuid  -- same
gateway_keys.org_id  uuid  -- same
journal_lines.org_id uuid  -- same
receipts.org_id      uuid  -- same
```

There is no `REFERENCES organizations(id)` because that table does not exist. Isolation is: **every query takes `org_id` from the path** (after One membership check), not from a filter that silently becomes `Guid.Empty`.

---

## 4. Path `{tenantId}` vs header

### 4.1 What One already decided (copy this, do not “simplify”)

[02](../011-new-lazuar-pay/02-one-integration.md) session table:

| Call | Why |
|------|-----|
| `GET /me` | `user_id`, email, `tenants[]` (`id`, `slug`, `name`, `role`), `active_tenant_id`, `is_platform_admin` |
| Path `{tenantId}` + membership | Authorization SoT. `X-Lazuar-Tenant-Id` is a **hint only**. Never authorize by header alone. |

One code that implements the hint (`/Users/akmalfirdaus/Code/lazuar/lazuar-one/apps/lazuar-api/src/Lazuar.One.Api/Infrastructure/Tenancy/ActiveTenantHint.cs`):

```csharp
/// Optional inbound workspace hint. Never authorize from this header alone.
/// Path {tenantId} + TenantAccessService stay SoT.
public const string HeaderName = "X-Lazuar-Tenant-Id";
```

`GET /me` echoes `active_tenant_id` only when the hint parses as a GUID **and** matches an active membership. A garbage or cross-tenant header is **omitted**, not a 403 on `/me`. API-key `/me` sets `active_tenant_id` to the **bound** tenant and **ignores** the header.

`TenantAccessService` comment: “Never trust `X-Tenant-Id` alone.” (One’s product header is `X-Lazuar-Tenant-Id`; old Pay used `X-Tenant-Id`. Do not resurrect the short name as SoT.)

Membership gate is on **path** ` /tenants/{tenantId}/… `. Cross-tenant or missing tenant → 403, not 404 (D06 existence oracle). Pay should do the same on merchant `/v1/{tenantId}/…`: unknown id to a stranger is 403, not a leaky 404 of “this org exists.”

One does **not** mint `org_id` onto the access token (ORG-08 / M2M-15: different-by-design vs Auth0/Clerk). Pay must not start parsing `urn:zitadel:iam:org:project:roles` (NP-XX-024) or invent a Pay JWT with `org_id` as SoT.

### 4.2 What old Pay did (negative)

`TenantSecurityMiddleware` resolves tenant **in this order**:

1. `X-Tenant-Id`
2. `X-Tenant-Slug`
3. route value `tenantSlug`

Then it stuffs `HttpContext.Items["TenantId"]`. `ExecutionContextAccessor.TenantId` returns `Guid.Empty` when missing. EF global filter: `TenantId == Guid.Empty || OrganizationId == TenantId` — **empty context = full table**. Workers have no HTTP context → empty tenant → `IgnoreQueryFilters` harvest.

Admin routes required the header; `/one/*` workspace routes used path GUID and often **skipped** header membership. Login JWT role stayed `CLIENT` until the header injected membership `ADMIN`. That is NP-XX-008 (dual JWT vs membership) in production form.

### 4.3 What Pay’s own `/v1` should look like

Bezos door ([08-bezos-door.md](../011-new-lazuar-pay/08-bezos-door.md)): public versioned HTTP. Tenant is part of that door for **merchant** operations.

**Merchant (staff JWT or `lzr_sk_`):**

```text
POST   /v1/{tenantId}/products
GET    /v1/{tenantId}/products
POST   /v1/{tenantId}/checkouts      # merchant-created pay link
GET    /v1/{tenantId}/payments
POST   /v1/{tenantId}/gateway-keys
GET    /v1/{tenantId}/receipts/{id}
```

`{tenantId}` **is** the One tenant UUID = Pay `org_id`.

Authz sequence for those routes (S0 façade, used by S1 handlers):

1. Authenticate: Bearer access_token **or** `lzr_sk_`. Never cookie `lazuar_auth`.
2. Parse `{tenantId}` as UUID; else 400.
3. **One** `POST /tenants/{tenantId}/authz/check` (or batch-check for chrome) for `member` / `admin` / `owner` as required. API key: bound tenant **must equal** path `{tenantId}` (same rule as One `RequireApiKeyTenantAsync`).
4. Load Pay rows `WHERE org_id = :tenantId`. Do not also take a header as a second org.
5. Optional: send `X-Lazuar-Tenant-Id` to One `/me` for switcher chrome. **Ignore it for Pay SQL.**

**Buyer (no merchant token):**

```text
GET  /v1/pay/{payLinkId}          # hosted page bootstrap
POST /v1/checkouts                # public create if that is the sold door
POST /webhooks/{provider}/...     # gateway signed; tenant from verified payload / path that was minted server-side
GET  /v1/payments/{id}            # if public status; still scoped by knowledge of id + tenant on the row
```

Buyer routes **must not** require `X-Lazuar-Tenant-Id` or a One session. Tenant is on the checkout/product row. A buyer who guesses `{tenantId}` and lists products is a staff route, not a cash-register route.

**Do not:**

| Pattern | Why it fails |
|---------|----------------|
| Ambient `Items["TenantId"]` from header | Old empty-tenant workers; IDOR when filter disabled |
| Authorize only from `X-Lazuar-Tenant-Id` | Header is a hint; clients lie |
| Authorize from JWT `org_id` / Zitadel project roles | One does not mint this; NP-XX-024 |
| `{slug}` as isolation key | Slug is mutable; collisions after rename |
| Dual bind: header org A, path org B | Pick path. If both present and they disagree → 400 |
| Short `X-Tenant-Id` as the product header | One’s name is `X-Lazuar-Tenant-Id`. Do not create a third spelling |

If a SPA wants a “current workspace” cookie, that is **`lazuar_active_tenant` on the Pay origin** (same *idea* as lazuar-app), used only to **fill the path** and the One hint header. The cookie is not authorization.

### 4.4 Header still exists — for One, not for Pay SQL

Pay’s browser client, when calling **One**:

- `Authorization: Bearer <access_token>`
- `X-Lazuar-Tenant-Id: <uuid>` when the user has selected a workspace (so `/me.active_tenant_id` echoes)

Pay’s browser client, when calling **Pay `/v1/{tenantId}/...`**:

- `Authorization: Bearer <access_token>` (or `lzr_sk_` for scripts)
- Path already has the uuid
- Header to Pay is optional and **must not change which rows return**

---

## 5. Create workspace flow

### 5.1 S0 step 3 (first-slice tracker)

[12-first-slice-tracker.md](../011-new-lazuar-pay/12-first-slice-tracker.md) step 3:

> “Create workspace” in Pay = `POST /tenants` (or pick existing membership). One tenant id is Pay `org_id`. **No second org table.**

IDs: NP-ONE-007, NP-ONE-009.

### 5.2 Preconditions (S0 steps 1–2)

1. Pay SPA is a One OIDC app (`POST /tenants/{id}/apps` or seed like `lazuar-app`) — NP-ONE-001.
2. User signs in via `:5175`, Pay origin gets **access_token**, Pay UI calls `GET /me` — NP-ONE-003, NP-ONE-005, NP-ONE-006.
3. `GET /me.tenants[]` may already be non-empty (user created a tenant in lazuar-app, or was invited). **Picking one is a valid workspace.** Creating is not mandatory.

### 5.3 Create (happy path)

One TypeSpec:

```tsp
@route("/tenants")
interface TenantOperations {
  @post
  createTenant(
    @header("Idempotency-Key") idempotencyKey?: string,
    @body body: CreateTenantRequest,  // { name, slug }
  ): { @statusCode statusCode: 201; @body body: CreateTenantResponse } | Err;
}
```

`CreateTenantRequest`: `name` 1–200, `slug` 1–64.

Handler (`TenantEndpoints.CreateTenant`):

- Human JWT only (`RejectApiKey`).
- `Platform:AllowSelfServeTenantCreate` must be true (One kill-switch). Pay does not implement a second kill-switch by refusing to show the button while still writing `pay.organizations`.
- Caller `sub` becomes owner after provisioning saga: One DB row (`status=provisioning`) → Zitadel org → owner membership → OpenFGA owner tuple → `status=active` + outbox `tenant.created`.
- Idempotency: `Idempotency-Key` scoped to `CreatedByUserId`. Pay UI should send one.

Pay UI:

```text
Ada @ Pay :517x (or whatever Pay ops origin is — not :5173)
  → already has access_token from :5175
  → GET {ONE}/api/v1/me
  → if tenants.length >= 1: WorkspaceSwitcher (presentational). Selecting id writes
     Pay’s active-tenant cookie/sessionStorage AND navigates to /w/{id}/... or /{id}/...
  → if Ada clicks “Create workspace”:
       POST {ONE}/api/v1/tenants
       Authorization: Bearer {access_token}
       Idempotency-Key: {uuid}
       { "name": "Acme", "slug": "acme" }
  → 201 { id, slug, name, status, ... }
  → Pay does not INSERT. Pay puts `id` in the URL.
  → If status is still provisioning, Pay may poll GET /tenants/{id} or wait and retry.
     Do not create a Pay “pending org” row.
```

Pay **does not** call `POST /platform/tenants` (NP-XX-023). That is the staff directory.

Pay **does not** call Zitadel Management to create an org.

### 5.4 Pick existing membership

`GET /me.tenants[]` already has `id`, `slug`, `name`, `role`, `status`. Switcher is enough. `GET /tenants` is the paginated equivalent if the UI wants it; [02](../011-new-lazuar-pay/02-one-integration.md) says “or trust `/me`.”

If `status != active` (provisioning, failed, suspended), the UI should not pretend S1 money works. See §8.

### 5.5 After create: still no Pay org row

NP-ONE-010: `GET` / `PATCH` tenant profile (name, metadata, logo) — **on One**. Pay ops “workspace settings” for **name/logo** is a client of One, not a Pay `UPDATE organizations`.

Pay-owned settings that appear later (S1): BYOK keys, products, pay links. Those hang off `org_id = tenant.id` without a parent org table.

### 5.6 Invite is not create-org

Second engineer (slice step 4): One copy-link invite. They accept, then `GET /me` lists the tenant. Pay still has no membership table.

### 5.7 What old Pay did instead (do not copy)

`POST /one/public/register` (`RegisterPublicUserCommandHandler`):

1. Insert `GlobalUser` (BCrypt password).
2. Optionally insert `Organization` + `TenantMembership(..., "ADMIN")`.
3. Grant `TenantAppEntitlement` for `OPS`, `BILLING`, `PAYMENTS`, `CRM`, `LHDN`.
4. Issue cookie `lazuar_auth` with JWT role **`CLIENT`** (unless system admin).

`POST /one/workspaces` (`CreateWorkspaceCommandHandler`): any authenticated user, membership `ADMIN`, optional `provision_apps`.

That is identity **inside** Pay. New Pay’s create-workspace is **one HTTP call to a sibling process**.

---

## 6. First slice: no Pay org table; whoami = One tenants; S1 rows FK the id

### 6.1 S0 (One façade) — Pay database may be empty of merchant rows

S0 is: SPA registered, login `:5175`, `GET /me`, create or pick tenant, invite, `lzr_sk_`, `authz/check`, (later) webhook subscription.

**Pay-side tables that S0 does not require:**

| Table people will try to add | Verdict |
|------------------------------|---------|
| `organizations` / `tenants` / `workspaces` | **Refuse** (NP-XX-014) |
| `users` / `global_users` | **Refuse** (NP-XX-007) |
| `memberships` / `workspace_members` | **Refuse** — One members |
| `invites` | **Refuse** — One invites |
| `org_map` / `one_tenant_map` | **Refuse** until §7 |
| Empty `products` / `ledger_accounts` seeded on `tenant.created` | **Do not** in first slice — see §6.3 |

S0 “Pay” can be a UI that only talks to One, plus a Pay process that already serves `/v1/health` (`packages/pay-spec/main.tsp` today). That is enough to prove Consumer-0.

Whoami in S0: One `GET /me`. The switcher renders `tenants[]`. There is nothing to JOIN in Pay.

### 6.2 S1 (money) — first Pay rows, keyed by One’s id

S1 is BYOK keys, product + pay link, buyer pays, webhook, subscription + journal + `RCPT-` in one transaction.

**Pay-side rows that appear at S1** (not earlier):

| Row | When it appears | `org_id` |
|-----|-----------------|----------|
| Encrypted gateway keys | Merchant pastes Stripe **or** CHIP/Billplz (NP-GW-001) | One tenant id |
| Product (+ prices, MYR) | Merchant creates catalog (NP-CAT-001…005) | One tenant id |
| Pay link / checkout session | Merchant creates or buyer opens (NP-CHK-*) | One tenant id |
| Charge / payment attempt | Gateway + webhook (NP-GW, NP-FUL) | One tenant id |
| Subscription or one-off complete | Same handler as ledger (NP-FUL-001) | One tenant id |
| Journal lines (cash, revenue, tax, fee) | Same transaction (NP-MON-001) | One tenant id |
| Receipt `RCPT-…` | Same transaction (NP-DOC-001) | One tenant id |
| Audit row on charge / key change | Same transaction (NP-AUD-001, NP-AUD-003) | One tenant id |
| Optional small payer profile | Checkout email/name (NP-BUY-001/002) | One tenant id of the **merchant**, plus payer identity that is **not** a One user |

“Products, charges” in the assigned slice are the **exemplars**. They are not a license to skip the rest of the S1 money loop; they are the proof that Pay stores **money**, not **orgs**.

Buyer rows (payer email) are Pay-owned and merchant-scoped. They are not One humans.

### 6.3 Tension with NP-ONE-019 — resolve it without an org table

[11-checklist.md](../011-new-lazuar-pay/11-checklist.md):

| ID | Feature | Wave | Notes |
|----|---------|------|-------|
| NP-ONE-019 | Provision Pay catalog/ledger rows on `tenant.created` | S0 | (empty notes) |

[02](../011-new-lazuar-pay/02-one-integration.md) events table: `tenant.created` → “Provision Pay-side catalog/ledger rows.”

That line is the footgun that recreates `TenantProvisionedIntegrationEvent` → Messaging replica → “tenant-specific schemas.” Old One README:

> `TenantProvisionedIntegrationEvent`: Fired when a new workspace is created. Triggers downstream modules to initialize tenant-specific schemas/replicas.

**This paper’s reading, for the first slice:**

- **Do not** insert a Pay `organizations` row on `tenant.created`.
- **Do not** pre-create empty products, empty ledger accounts, or a “tenant replica schema.”
- S1 inserts happen when Ada **creates a product** or a **charge** lands. `org_id` is the path id she already has.
- `tenant.created` webhook is useful **later** as a cache of “this uuid exists / is active,” or to fail closed if Pay is asked to charge a tenant One never created. It is **not** a provisioner of catalog.
- NP-ONE-019’s honest first-slice meaning: *Pay is allowed to hear `tenant.created` and no-op, or to record an in-memory/optional status cache.* It is **not** permission to dual-write org. If the checklist later needs a stricter job, split it: “optional status cache” vs “seed ledger.” Seeding ledger before first charge is how you get empty books and a second SoT.

First slice can **skip the webhook entirely** for create (S0 step 6 in the tracker is subscribe to `member.*` and `tenant.suspended` — not a hard create-provision). Status for “may I charge?” can be `GET /tenants/{id}` (§8).

### 6.4 Isolation without an org table

Every S1 handler:

```text
tenantId = path UUID
authz_ok = One authz/check(member|admin) on tenantId
if !authz_ok → 403
rows = SELECT * FROM products WHERE org_id = tenantId
```

No global query filter that treats missing tenant as “all rows.” Jobs carry `org_id` on the payload (old worker `Guid.Empty` is a refuse).

Gateway webhooks: tenant id in the **verified** path or signed metadata that Pay minted when creating the checkout — not a client header.

---

## 7. Why a mapping table is a last resort (written reason)

NP-XX-014 is a refuse row. Refuse rows get deleted by well-meaning “flexibility.” This section is the written reason so that a mapping table cannot land without **this text becoming false**.

### 7.1 What a mapping table is

Any of:

```text
pay.organizations(id, one_tenant_id, name, slug, ...)
pay.org_map(pay_org_id, one_tenant_id)
pay.tenants(id)  -- uuid generated by Pay, not equal to One’s
Organization.ExternalOrgId / ExternalProduct  -- old Pay already did this
```

Including “just a cache of name/slug” if it has its **own** primary key.

Old Pay already shipped the last-resort pattern for an integrator:

```csharp
// Modules/One/Domain/Organization.cs
public string? ExternalProduct { get; private set; }
public string? ExternalOrgId { get; private set; }
public void BindExternalRef(string product, string externalOrgId) { ... }
```

That is a mapping because Pay (then Hub) believed it *was* the org SoT and Aura had another id. New Pay is the **consumer**. One is the SoT. Turning Pay back into an SoT with `one_tenant_id` as an attribute repeats 2024–2026.

### 7.2 Why it is the wrong default

**1. Dual source of truth for “does this merchant exist?”**

One `tenants.status` can be `deleted` / `suspended` while `pay.organizations` is still `active`. Or the reverse: Pay inserts an org because a webhook retried, One never activated. Every invite, leave, wipe, and suspend then needs a sync. We already paid that tax with `TenantAppEntitlement` vs actual module rows, and with `TenantUpdatedIntegrationEvent` that Messaging subscribed to and One never published.

**2. Dual membership.**

The moment Pay has its own org id, someone will add `pay.members` “because checkout needs a role.” That is the exact sentence NP-XX-014 exists to kill: *“Second `organizations` table just for Pay plus One members.”* [02](../011-new-lazuar-pay/02-one-integration.md): do not invent a second membership system just for merchants **and** also use One members.

**3. Whoami becomes a JOIN.**

`GET /me` already returns tenants. A mapping table forces Pay whoami to join `pay.organizations` and decide what to show when One has a tenant Pay has not seen yet (new invite) or Pay has a row One has deleted (wipe leftovers). The first-slice UX is: **whoami is One**. Mapping makes “create workspace” a distributed transaction (`POST /tenants` + `INSERT pay.organizations`) — the saga we are trying not to rebuild.

**4. IDs drift under load.**

Retries of `tenant.created`, lost webhooks, and “provision catalog on create” (NP-ONE-019 misread) produce:

- One uuid with no Pay row → Ada cannot create a product until a healer runs.
- Pay uuid with no One row → charges for a ghost merchant.
- Two Pay rows for one One uuid after a non-idempotent consumer.

Identity-as-equality (`org_id == one.tenants.id`) makes those states **unrepresentable**. Mapping makes them the default incident.

**5. Header vs path vs map is three knobs.**

Old Pay: header, slug, route, JWT role, membership role. Add a map and you get “path is Pay id, header is One id, JWT has neither.” Support will not know which uuid to paste into SQL.

**6. Isolation tests get a translation bug.**

Tenant isolation bugs in the old tree were already “wrong uuid in the ambient context.” A map doubles the chance of checking membership on One id A and reading products for Pay id B.

**7. You do not need it for the product.**

S1 needs: products, prices, keys, checkouts, charges, journal, receipts. Each needs **one uuid** to isolate. One already minted a uuid. Using a second uuid does not add a feature Ada can see. It adds a migration and a sync job.

**8. Industry default for a *consumer* is copy-the-id, not map.**

Stripe Connect has a mapping because Stripe is the SoT and **your** app had users first. WorkOS/Clerk org id is stored **as** your account’s org id. Pay is Consumer-0 of One ([02](../011-new-lazuar-pay/02-one-integration.md), One `plans/017-evals/08-dogfood-then-serve.md` §6). Pay did **not** have merchants first. There is no legacy Pay org id to preserve.

**9. Name/slug cache is not a reason.**

“We need the workspace name on the receipt” is `GET /tenants/{id}` at print time, or a denormalized `merchant_name` **on the receipt row** taken at issue time (a document snapshot, like any legal document). That is not an org table. Receipts should not JOIN live org name anyway (rename should not rewrite history).

**10. SST registration, billing address, TIN — still not an org table.**

When V1 needs SST fail-closed (NP-MON-004), that is a **Pay tax profile** keyed by the same `org_id`, created when Ada fills tax settings — analogous to BYOK keys. It is not merchant identity. It must not grow `members[]`.

### 7.3 When a mapping table would stop being last resort

Write a new paper and **edit NP-XX-014** only if one of these becomes true:

1. **One tenant ids are not stable** (One starts recycling uuids, or exposes only slugs, or tenant wipe **reuses** id). Today wipe is tombstone (`status=deleted`); id does not come back as a live tenant. Pay should treat deleted ids as dead, not remap.
2. **Pay must survive replacing One** with another IdP **while keeping the same charge history**, and the new IdP cannot keep the uuid. That is a **migration event**, not a day-one schema. You add a map **then**, backfill once, and you still do not create a membership plane.
3. **A regulator requires a Pay-issued merchant identifier** distinct from the identity provider. Then the receipt prints `MERCH-…` as a **document number** (like `RCPT-`), still with `org_id = one uuid` internally. A public merchant code ≠ a second org SoT.
4. **Multi-One** (Pay talking to two One instances). Not on the table. Do not design for it.

Until 1–4 are real, `pay.org_map` is museum construction.

### 7.4 Last-resort shape, if that paper is ever written

If forced, the map is:

```text
org_id           uuid PRIMARY KEY  -- STILL One’s tenant id, not a new id
one_base_url     text              -- only if multi-One
recorded_at      timestamptz
```

That is a **cache of seen ids**, not a surrogate. A surrogate `pay_org_id serial` is the full anti-pattern. Prefer still not to have the table: `SELECT 1 FROM products WHERE org_id = $1 LIMIT 1` tells you whether Pay has seen the merchant.

---

## 8. Suspend: later webhook; first slice can GET `/tenants/{id}`

### 8.1 What One exposes

TypeSpec `TenantStatus`: `provisioning`, `active`, `failed`, `suspended`, `deleted`.

`GET /tenants/{tenantId}`:

- Implementation uses `TenantAccessMode.AllowSuspended` so **members may still GET when suspended** (diagnostics: `provisioning_step`, `last_error`).
- Returns `status` in the body (`MapTenantDto`).
- API keys need `tenant:read`.
- Deleted / non-member → 403 “Not a member,” not a chatty 404.

`POST /tenants/{id}/suspend` and `/reactivate` exist. [02](../011-new-lazuar-pay/02-one-integration.md): staff or Pay-admin policy — **not** merchant self-serve default. Pay UI should not put “Suspend” next to “Rename workspace” for Ada in v1.

`GET /me.tenants[].status` also carries lifecycle when known.

Webhooks (One `WebhookEventCatalog` + docs):

| Event | When |
|-------|------|
| `tenant.created` | Provisioning completes **active** |
| `tenant.suspended` | Suspended |
| `tenant.reactivated` | Reactivated |
| `tenant.deleted` | Wipe / tombstone |

NP-ONE-017 / NP-ONE-018: HMAC webhooks; **stop charges (and staff access) on `tenant.suspended`**. Money in Pay stays true if the webhook is late.

### 8.2 First slice (honest minimum)

S0 tracker step 6 says subscribe to `member.*` and `tenant.suspended`. That can land with S0 HTTP, but **Pay can charge correctly in S1 without a durable webhook consumer** if every **mutating merchant money route** and the **charge path** checks One:

```text
GET {ONE}/api/v1/tenants/{tenantId}
Authorization: Bearer {user token or lzr_sk_ with tenant:read}
```

If `status != active` → do not create products / do not start checkout / do not off-session charge. `provisioning` / `failed`: show One’s `last_error`, offer retry-provision **on One**, not a Pay healer. `suspended`: 403 with “workspace suspended.” `deleted`: 403, treat as non-member.

This is allowed because One **does** expose status on GET (the assigned slice’s “if One exposes it” is **yes** on this SHA).

Do **not** store `pay.organizations.status` as a copy you then trust. If you cache, cache with short TTL and still fail closed on GET when charging.

`RequireActiveTenantAsync` in One membership mutations already 403s non-active (issue 085 was about inconsistent “active”; tests now lock `status == active` for those verbs). Pay should not reimplement a looser check.

### 8.3 Later: webhook

When Pay takes `tenant.suspended` push:

- Verify HMAC (`whsec_`), idempotent on `X-Lazuar-Event-Id`.
- Stop **new** charges and **staff** mutating ops for that `org_id`.
- Do **not** reverse the journal because One was late. [02](../011-new-lazuar-pay/02-one-integration.md): *If the webhook is late, money in Pay is still true; staff access may lag. That is the cost of this split. Do not put buyer entitlement in One.*
- `tenant.reactivated` resumes staff + new charges; it does not invent PAST_DUE.
- `tenant.deleted`: stop everything; leftover Pay rows are **money history**, not a live merchant. Do not CASCADE delete charges because One wiped. (One’s own wipe already documents leftovers: audit, outbox, slug, …)

Pull alternative: `GET /tenants/{id}/events` if Pay cannot take push ([02](../011-new-lazuar-pay/02-one-integration.md)).

### 8.4 What not to do on suspend

| Wrong | Why |
|-------|-----|
| Flip `pay.organizations.is_active` | Table must not exist |
| Archive via old `Organization.Archive()` | Negative example |
| Disable `TenantAppEntitlement COMMERCE` | Entitlements are the old dual SoT |
| Put buyers into One so you can “suspend users” | Buyer plane is Pay |
| Call `POST /platform/tenants/...` | Staff directory |

---

## 9. Anti-pattern: Pay `organizations` + One `tenants` dual SoT

This is NP-XX-014 in operational form.

### 9.1 The shape that will be proposed

```text
One.tenants  1 ──<  pay.organizations.one_tenant_id
                 └── pay.memberships (optional “cache”)
                 └── pay.products.org_id → pay.organizations.id
```

Then a weekly “sync names from One” job. Then an incident where Ada renamed in One and the receipt still says the old name *and* the pay link 404s because slug was Pay-owned.

### 9.2 Why it is worse than a mapping table alone

A mapping table at least *admits* One is SoT. A full Pay `organizations` row with `name`, `slug`, `logo`, `status`, `owner_user_id` is a **fork**. Old Pay `Organization` is that fork: name, slug, `IsActive`, branding, `ExternalOrgId`. Downstream modules referenced `OrganizationId` as a primitive guid **and** One still owned membership — two writers for one idea.

New Pay + real One makes the fork **cross-process**. You cannot put both writes in one EF `SaveChanges`. You will build an outbox. You will park `TenantProvisioned`. You will write honesty files.

### 9.3 Dual SoT failure modes (concrete)

1. **Create:** Pay UI inserts `pay.organizations` first, then `POST /tenants` 409s on slug → orphan Pay org. Or One 201s and Pay insert fails → Ada sees a workspace in `/me` that 404s in Pay.
2. **Invite:** Member appears in One `GET /me` but Pay members cache lags → VIEWER/MEMBER cannot open ops (dogfood step 12 fails in the opposite direction from “VIEWER can charge”).
3. **Suspend:** One suspends; Pay org still active; CHIP charge still fires. Or Pay “suspends” and One still allows invites.
4. **Wipe:** One `POST /tenants/{id}/delete` tombstones; Pay org remains; receipts still “live”; next create reuses slug in One but Pay still holds the old uuid.
5. **Whoami:** Pay `/whoami` returns Pay orgs; One `/me` returns different set. Switcher depends on which client was coded last.
6. **Support:** Two uuids in every ticket. “Is this the One id or the Pay id?”

### 9.4 The allowed denormalizations (not dual SoT)

These are **not** an org table:

| Denormalization | Why it is OK |
|-----------------|--------------|
| `products.org_id uuid` | Isolation key = One id |
| `receipts.merchant_name` at issue time | Document snapshot |
| Short TTL cache of `GET /tenants/{id}` status | Fail-closed helper; One remains SoT |
| Gateway metadata `{ tenant_id }` Pay minted | So the webhook can isolate |

These **are** dual SoT:

| Thing | Why it is not OK |
|-------|------------------|
| `organizations.name` updated by Pay PATCH | NP-ONE-010 is One PATCH |
| `organizations.status` flipped by Pay admin | Suspend is One |
| `members` in Pay | One membership |
| `entitlements` COMMERCE flag | Old `TenantAppEntitlement` |
| Surrogate `organizations.id` | Mapping last resort |

---

## 10. Old Pay `Modules/One` — negative example (do not copy)

Skim-only, as assigned. The module README calls itself “the global CIAM.” That sentence is the bug. CIAM is **lazuar-one**. This folder is a museum of the first cut that put identity *inside* the money tree ([00-why-leave.md](../011-new-lazuar-pay/00-why-leave.md), [09-old-pay.md](../011-new-lazuar-pay/09-old-pay.md)).

### 10.1 `GlobalUser`

`Modules/One/Domain/GlobalUser.cs`: email, name, **BCrypt `PasswordHash`**, `SecurityStamp`, `IsSystemAdmin`, email-verify and password-reset hashes.

New Pay: **no password store** (NP-XX-007). Humans are One/Zitadel. Buyers are Pay payer profiles **without** passwords (magic link later).

`ClientProfileEntity.GlobalUserId` (CRM) is the old mix of planes: a buyer row pointing at a staff identity table. New Pay payer profile must not have `one_user_id` because cardholders are not One users.

### 10.2 `Organization` + `TenantMembership`

Staff roles in domain comments: `ADMIN`, `MEMBER`, `VIEWER`. JWT is separately `CLIENT` or `SUPER_ADMIN`. Cookie is injected with membership only after `X-Tenant-Id` / slug resolves (`TenantMembership.cs` comment; `TenantSecurityMiddleware`).

Invite **rejects** `CLIENT` (`WorkspaceStaffRoles.NormalizeInvitedRole`). README historically claimed a paid subscription “may grant a `CLIENT` membership.” No production handler inserts `TenantMembership(..., "CLIENT")`. That teachability hole is issue **259**.

New Pay must not have a JWT role named `CLIENT`. Buyers are not a role. Staff roles are One’s `owner|admin|member`.

### 10.3 Cookie `lazuar_auth` vs `lazuar_admin_auth` — CLIENT vs ADMIN

`AuthCookie`:

- Merchant cookie name `lazuar_auth`
- Admin cookie name `lazuar_admin_auth`, path `/api/v1/platform`

`IssueCookie` always sets `ClaimTypes.Role` to `SUPER_ADMIN` if `IsSystemAdmin`, else **`CLIENT`**.

Register (`POST /one/public/register`) used to return JSON `Role: "ADMIN"` while the cookie was `CLIENT` — [00-why-leave.md](../011-new-lazuar-pay/00-why-leave.md) lists this as a reason the cathedral hurt. Current `AuthEndpoints.cs` on this SHA **did** align register JSON to `CLIENT` (issue 259 harvest), but the **architecture** remains: cookie role is not workspace role; `OrgAdmin` needs membership injection from a header.

`GET /one/auth/me` returns that JWT role, not `tenants[]`. It is a user row, not a workspace switcher.

New Pay:

- **No** `lazuar_auth` issued by Pay.
- **No** second cookie for “admin path.”
- Session is One/Zitadel + Pay origin’s own OIDC tokens (and maybe `lazuar_active_tenant` as a **hint cookie**, like lazuar-app, never as a role).
- Merchants never use `lazuar-admin` `:5173` (NP-ONE-005, NP-XX-018).

### 10.4 Ambient header tenancy

Already §4.2. Global filter + `Guid.Empty` = cross-tenant read. New Pay: path `{tenantId}` on merchant routes; explicit `WHERE org_id = $1`; jobs carry org id.

### 10.5 `TenantAppEntitlement`

Per-workspace module toggles (`COMMERCE`, `BILLING`, …). Public register granted a hardcoded core list. Create-workspace `provision_apps` did not. Grant published an event; revoke did not.

New Pay is **one product**. If Ada can sign in and has One membership, she can use Pay (subject to suspend). Do not rebuild “enable COMMERCE for this org.”

### 10.6 System tenant `00000000-…-0001`

Genesis job, platform routes hardcoded to system tenant. New Pay has no system org in the money DB. Platform staff live in One `Platform:AdminEmails`. Pay does not special-case `…0001`.

### 10.7 Endpoints Pay must not grow

| Old | New |
|-----|-----|
| `POST /one/public/register` | One login + `POST /tenants` |
| `POST /one/auth/login` | OIDC |
| `GET /one/auth/me` | One `GET /me` |
| `POST /one/workspaces` | One `POST /tenants` |
| `GET /one/workspaces/{id}/members` | One `GET /tenants/{id}/members` |
| Host JWT + `X-Tenant-Id` | Path + One `authz/check` |

---

## 11. HTTP Pay should call (tenancy only)

From [02](../011-new-lazuar-pay/02-one-integration.md), narrowed to this paper. Base: One `/api/v1`.

| Method | Path | First-slice Pay use |
|--------|------|---------------------|
| `GET` | `/me` | Whoami, switcher, roles. Can write (JIT). Not a hot loop. |
| `POST` | `/tenants` | Create workspace. Human JWT. Idempotency-Key. |
| `GET` | `/tenants` | Optional list; `/me` is enough. |
| `GET` | `/tenants/{id}` | Profile + **status** (suspend/provisioning). Members allowed when suspended. |
| `PATCH` | `/tenants/{id}` | Name / metadata / logo — One SoT. |
| `POST` | `/tenants/{id}/authz/check` | Before merchant admin routes. |
| `POST` | `/tenants/{id}/members/invite` | Slice invite (not this paper’s core, but not a Pay table). |

Do **not** call `POST /platform/tenants`.

Create/get TypeSpec evidence (One SHA `0f79fe4`):

- `packages/api-spec/modules/tenants/routes.tsp` — `createTenant`, `getTenant`, `suspendTenant`, `reactivateTenant`
- `packages/api-spec/modules/tenants/models.tsp` — `Tenant.id: string`, `Tenant.status: TenantStatus`
- Implementation `TenantEndpoints.cs` — `{tenantId:guid}`, create 201, get `AllowSuspended`

---

## 12. S0 vs S1 vs later — tenancy checklist mapped

| ID | Job | When | This paper |
|----|-----|------|------------|
| NP-ONE-006 | `GET /me` | S0 | Whoami = One tenants |
| NP-ONE-007 | Path `{tenantId}` + membership SoT | S0 | Pay `/v1/{tenantId}/…`; header hint only |
| NP-ONE-009 | Create workspace = `POST /tenants`; id is `org_id` | S0 | §5 |
| NP-ONE-010 | GET/PATCH tenant profile | S0 | One, not Pay org |
| NP-ONE-015 | `authz/check` before admin routes | S0 | Used by S1 handlers |
| NP-ONE-017 | HMAC webhooks including `tenant.suspended` | S0 listed | First slice may GET status instead; webhook later |
| NP-ONE-018 | Stop charges on suspend | S0 listed | Charge path fail-closed on GET status; webhook later |
| NP-ONE-019 | Provision catalog on `tenant.created` | S0 listed | **Do not** seed org/catalog; S1 inserts products/charges |
| NP-CAT-001… | Products | **S1** | First Pay rows; `org_id` = One id |
| NP-GW / NP-FUL / NP-MON | Keys, charges, journal | **S1** | Same |
| NP-XX-007 | Identity inside Pay | refuse | No GlobalUser |
| NP-XX-008 | Dual JWT vs membership | refuse | No CLIENT cookie role |
| NP-XX-013 | Zitadel human per cardholder | refuse | Buyer plane |
| NP-XX-014 | Second organizations table | refuse | This paper |
| NP-XX-018 | Ship merchants to `:5173` | refuse | |
| NP-XX-023 | `POST /platform/tenants` | refuse | |
| NP-XX-024 | Parse Zitadel project roles | refuse | |

Pass/fail from [03](../011-new-lazuar-pay/03-first-slice.md) that this paper owns:

- **Fail:** Pay password form or second org table.
- **Fail:** Buyer created as a Zitadel human.
- **Fail:** Merchant sent to `lazuar-admin`.

---

## 13. Suggested Pay column name (one choice)

011 says `org_id`. One says `tenant_id`. Both appear in Pay papers (`org_id`) and One headers (`X-Lazuar-Tenant-Id`).

**Recommendation:** use **`org_id` in Pay SQL and Pay TypeSpec** as the column, with the invariant `org_id == One tenant UUID`. Use `{tenantId}` in **paths** to match One’s URL grammar so Ada’s mental model is one uuid in both dashboards.

Do not have both `org_id` and `tenant_id` columns. Do not have `organization_id` as a third spelling (old C# `OrganizationId`). Pick `org_id` in tables, `tenantId` in paths, document the equality here.

---

## 14. Fail modes for implementers

| If you… | You have failed this paper |
|---------|----------------------------|
| Add `CREATE TABLE organizations` | NP-XX-014 |
| Generate a Pay uuid and store One’s id beside it | §7 last resort without a new paper |
| Authorize merchant SQL from `X-Lazuar-Tenant-Id` only | NP-ONE-007 |
| Issue `lazuar_auth` with role `CLIENT` | §10.3, 00-why-leave |
| Implement `POST /v1/register` with password | NP-XX-007 |
| Seed products on `tenant.created` before Ada asks | §6.3 |
| Join `ClientProfile.GlobalUserId` | mixed planes |
| Call One `POST /platform/tenants` from Pay | NP-XX-023 |
| Treat slug as FK | §3.2 |
| Copy `TenantSecurityMiddleware` | §4.2 |
| Hammer `GET /me` per keypress | JIT writes |
| Show Zitadel login on hosted checkout | NP-CHK-007 / NP-XX-013 |

---

## 15. What this paper does not decide

- Pay ops origin port (not `:5173`; `:5175` is login, not Pay homepage).
- Exact Pay path prefix (`/v1/{tenantId}/products` vs `/v1/tenants/{tenantId}/products`) — both are path-SoT; pick one in TypeSpec later.
- VIEWER vs One `member` custom role — NP-ONE-021 enforcement matrix belongs with authz, not org storage.
- Whether Pay BFF proxies `/me` or the browser calls One directly — both are allowed; both must return **One** tenants.
- Language / binary of new Pay ([05](../011-new-lazuar-pay/05-language.md), [13](../011-new-lazuar-pay/13-monolith-vs-services.md)).
- Tax profile columns (V1 SST fail-closed) — Pay settings keyed by `org_id`, still not an org SoT.

---

## 16. Evidence index

| Claim | Evidence |
|-------|----------|
| One tenant id is Guid v7, JSON string | `TenantService.CreateAsync` `Id = Guid.CreateVersion7()`; TypeSpec `id: string`; `MapCreateResponse` `Id = t.Id.ToString()` |
| `GET /tenants/{id}` returns `status`; members may GET when suspended | `TenantEndpoints.GetTenant` `TenantAccessMode.AllowSuspended`; `Tenant` model `status` |
| Header is hint | `ActiveTenantHint`; [02] session table; `MeEndpoints.GetMe` hint match |
| Path + membership SoT | `TenantAccessService` on `/tenants/{tenantId}`; NP-ONE-007 |
| Create workspace | TypeSpec `POST /tenants`; `TenantEndpoints.CreateTenant`; NP-ONE-009 |
| Whoami shape | TypeSpec `MeResponse.tenants`; recipe `user-oidc-spa.md` |
| Webhook catalog includes suspend | `WebhookEventCatalog.TenantSuspended`; One docs `webhooks.md` |
| Old GlobalUser / cookie CLIENT | `GlobalUser.cs`; `AuthCookie.cs`; `AuthEndpoints.IssueCookie`; issue 259 |
| Old ambient tenant | `TenantSecurityMiddleware`; `docs/001-gaps/14-tenant-isolation.md` |
| Old product already used OrganizationId as uuid | `Modules/Commerce/Domain/Aggregates/Product.cs` |
| Dual SoT refuse | NP-XX-014; [03] fail list; [02] “do not invent a second membership system” |
| Two planes | [01] buyer plane; [02] two planes table |
| Pay TypeSpec has no orgs yet | `packages/pay-spec/main.tsp` health only |

---

## 17. One-paragraph restatement

Pay does not store merchants. One does. The uuid One returns from `POST /tenants` and lists on `GET /me` is the only merchant id Pay ever writes on a product or a charge. Merchant HTTP in Pay puts that uuid in the **path** and asks One whether the bearer is a member; the header is a switcher hint. Create workspace is One’s POST. Whoami is One’s GET. Suspend is One’s `status` (GET now, webhook later). A Pay `organizations` table, a mapping table, a `GlobalUser`, and a cookie that says `CLIENT` while the JSON says `ADMIN` are how the last tree split identity from money and then lied about both.
)