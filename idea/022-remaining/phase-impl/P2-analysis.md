# P2 Analysis — Hub Connect-ready provision

**Plan:** 663  
**Phase:** 2 — Hub Connect-ready provision (webhook at provision **or** companion API; owner membership for deep-link)  
**Repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub`  
**Status:** Design only — **no implementation in this doc**  
**Primary code:**
- `apps/lazuar-api/Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs`
- `apps/lazuar-api/Modules/One/Application/Commands/SaveWebhookCommand.cs` (`CreateWebhookEndpointCommand*`)
- `apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs` (provision + workspace webhooks)
- `apps/lazuar-api/Modules/One/Domain/PlatformApiScopes.cs`
- `apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs`
- TypeSpec: `packages/api-spec/modules/one/models.tsp`, `routes.tsp`
- Tests: `apps/lazuar-api/tests/Lazuar.ModuleTests/One/ProvisionAuraWorkspaceTests.cs`, `OutboundWebhookTests.cs`

---

## 1. Goal (Connect-ready)

When Aura (or another integrator) provisions a Hub workspace, the result must be **immediately usable for Hub → Aura event delivery** and **human deep-link into ops**:

| Capability | Why |
|------------|-----|
| Workspace + `aura` external ref + `PAYMENTS` entitlement | Existing P1 provision |
| Bootstrap `sk_test_` / `sk_live_` with Aura scopes | M2M checkout path |
| **Outbound webhook endpoint** registered to Aura’s receiver URL | Hub can fan-out commerce lifecycle events without a separate console hop |
| **Signing secret returned once** (`whsec_…`) | Aura stores secret for HMAC verify (`X-Lazuar-Signature`) |
| **Owner membership** (ADMIN or SUPER_ADMIN) if `owner_email` matches an existing GlobalUser | Deep-link “Open Hub” lands on a workspace the user already belongs to (`/me/entitlements`) |

Two registration paths (either is enough; prefer provision-time for zero-friction Connect):

1. **Provision-inline (primary):** optional `webhook_url` on `POST …/integrations/workspaces/provision`.
2. **Companion API (secondary):** after provision, create endpoint via workspace webhook API with machine or human credentials.

---

## 2. Current state (facts from code)

### 2.1 `ProvisionAuraWorkspaceCommand` (today)

```24:31:apps/lazuar-api/Modules/One/Application/Commands/ProvisionAuraWorkspaceCommand.cs
public record ProvisionAuraWorkspaceCommand(
    string AuraOrgId,
    string DisplayName,
    string? Slug,
    string? OwnerEmail,
    bool IsTestMode,
    string? KeyName,
    Guid? ActorUserId) : ICommand<ProvisionAuraWorkspaceResult>
```

**Create path (atomic `SaveChanges`):**
1. Normalize `aura_org_id` → lowercased GUID `D`.
2. Create `Organization`, bind `ExternalProduct="aura"` / `ExternalOrgId`.
3. **Optional membership:** if `OwnerEmail` non-empty and `GetUserByEmailAsync` finds a user → `TenantMembership(…, "ADMIN")`. Does **not** create users. Silent no-op if user missing.
4. `TenantAppEntitlement(org, "PAYMENTS")` + publish `AppEntitlementGrantedIntegrationEvent`.
5. Mint API credential with  
   `PlatformApiScopes.DefaultAuraIntegratorScopes` =  
   `payments.checkouts:write payments.checkouts:read`  
   (**does not** include `webhooks.endpoints:manage`).
6. On unique violation (concurrent same `aura_org_id`) → re-read and return **idempotent existing** result.

**Idempotent / existing result:**
- `Created: false`, `PlainKey: null`.
- Surfaces bootstrap key **id/prefix/hint/scopes** only (no remint).
- **No webhook create/list.**

**Auth (endpoint, not command):**
- `IntegratorProvisionAuth`: `X-Lazuar-Provision-Key` **or** Bearer provision secret **or** SUPER_ADMIN JWT.
- Tenant-exempt path: `/api/v1/one/integrations/workspaces/provision`.
- Rate limits: global + per-`aura_org_id`.

**TypeSpec** (`ProvisionWorkspaceRequestDto`): `aura_org_id`, `display_name`, `slug?`, `owner_email?`, `is_test_mode?`, `key_name?`.  
**Response:** `workspace_id`, `slug`, `aura_org_id`, `created`, `api_key { id, prefix, hint, scopes, plain_key? }`.  
No webhook fields.

### 2.2 Webhook create (today)

| Layer | Behavior |
|-------|----------|
| Domain | `TenantWebhookEndpoint(orgId, url, secretKey, isActive, enabledEvents?)` — multi-endpoint per org; empty `enabledEvents` = all events |
| Command | `CreateWebhookEndpointCommand` → secret = `"whsec_" + GenerateSecureToken(24).PlainToken`; `AddWebhookEndpoint` + `SaveChanges`; returns full secret once |
| HTTP | `POST /api/v1/one/workspaces/{id}/webhooks` |
| AuthZ | Manual: membership role ∈ `{ADMIN, SUPER_ADMIN}` **or** `ctx.IsSystemAdmin`. **Not** `IntegrationWebhooksEndpointsManage`. |
| List | GET same path — secret never re-returned; `has_secret` + `secret_hint` only |
| Update | PUT — can change URL/active/events; **does not rotate secret** |

### 2.3 Scopes & policies (today)

| Symbol | Value | On Aura bootstrap key? | Policy |
|--------|-------|------------------------|--------|
| `PaymentsCheckoutsWrite` | `payments.checkouts:write` | ✅ default | `IntegrationPaymentsCheckoutsWrite` |
| `PaymentsCheckoutsRead` | `payments.checkouts:read` | ✅ default | `IntegrationPaymentsCheckoutsRead` |
| `WebhooksEndpointsManage` | `webhooks.endpoints:manage` | ❌ not default | `IntegrationWebhooksEndpointsManage` exists in `Program.cs` |
| LHDN / config scopes | … | ❌ | separate |

**Gap:** `IntegrationWebhooksEndpointsManage` is defined but **not attached** to any One webhook route. Companion M2M create is therefore **impossible** with current keys + current route auth (keys are `API_CLIENT`, never workspace `ADMIN` membership role).

### 2.4 Owner membership & deep-link (today)

- Membership role strings used in product: `"ADMIN"`, `"CLIENT"`, and checks also accept `"SUPER_ADMIN"` on some One routes (webhooks, etc.).
- `TenantMembership` stores role uppercased free string.
- Deep-link into ops depends on `/me/entitlements` → memberships for non–system-admin users.
- Provision only attaches **ADMIN** and only if GlobalUser already exists. No response signal for “owner attached / not found”. No invitation path.

### 2.5 Gaps vs Connect-ready

1. No `webhook_url` on provision → Aura must open console or call a human-only webhook API.
2. Secret reveal-once pattern exists for webhooks but is **not** wired into provision response.
3. Aura keys cannot manage webhooks (missing scope **and** policy wiring).
4. Owner deep-link is best-effort silent; role fixed to ADMIN; no SUPER_ADMIN option; no feedback.
5. Idempotent re-provision never ensures a webhook endpoint exists.

---

## 3. Design decisions (recommended)

### D-P2-1 — Primary path: extend provision (inline webhook)

**Accept optional `webhook_url` on provision.** When present on **first create**, create one `TenantWebhookEndpoint` in the **same** `SaveChanges` as org/key (single transaction). Return full signing secret **once** on that create (and on “first ensured create” — see D-P2-3).

Rationale: Connect onboarding is one call from Aura backend; no chicken-and-egg with ops UI or M2M webhook policy.

### D-P2-2 — Secondary path: companion API

Wire `POST/PUT/GET …/workspaces/{id}/webhooks` (mutate at least) to accept:

- Human: membership `ADMIN` | `SUPER_ADMIN` | system admin (**current**), **or**
- Machine: `API_CLIENT` + scope `webhooks.endpoints:manage` via **`RequireAuthorization("IntegrationWebhooksEndpointsManage")`** (and keep tenant binding from API key).

Optionally add `webhooks.endpoints:manage` to **`DefaultAuraIntegratorScopes`** so bootstrap keys can register/update endpoints after provision without a second mint.  

**Least privilege alternative:** keep defaults as-is; document that companion path needs an OrgAdmin-minted restricted key with that scope. **Connect preference:** include scope on Aura bootstrap so companion works out of the box if provision omitted `webhook_url`.

### D-P2-3 — Idempotency rules (critical)

Mirror API key secret handling.

| Scenario | Webhook behavior | Response secret |
|----------|------------------|-----------------|
| First create, `webhook_url` set | Create endpoint (active, default all events unless `webhook_enabled_events` provided) | Return `secret_key` once |
| First create, `webhook_url` omitted | No endpoint | `webhook: null` |
| Idempotent re-call, endpoint exists for org (match by URL preferred) | No remint; do not change secret | `secret_key: null`; return `id`, `url`, `secret_hint` / `has_secret` |
| Idempotent re-call, `webhook_url` set but **no** endpoint yet | **Ensure-create** endpoint (Connect heal) | Return `secret_key` once (first materialization) |
| Idempotent re-call, `webhook_url` differs from existing Aura-tagged endpoint | **Do not silent-overwrite** in v1 | Prefer: return existing Aura endpoint metadata without secret; optional later: `409` or explicit rotate endpoint |

**URL matching for ensure:** case-sensitive trim; require absolute `https` URL (see D-P2-5). If multiple endpoints exist, prefer exact URL match; else prefer a single “Aura Connect” named/tag convention if introduced (v1: **exact URL only**, else create new if URL not present — multi-endpoint model allows this; avoid duplicates by exact URL match).

### D-P2-4 — Owner membership role

**Request:** keep `owner_email?`; add optional `owner_role?` ∈ `{ "ADMIN", "SUPER_ADMIN" }`, default **`ADMIN`** (backward compatible).

| Rule | Behavior |
|------|----------|
| User not found | No membership; do **not** fail provision; report `owner_attached: false` / `owner_status: "user_not_found"` |
| User found, no membership | Add membership with requested role |
| User found, already member | Leave existing role (no downgrade/upgrade on idempotent re-call in v1); `owner_attached: true` |
| Role invalid | `400` before side effects |

**Deep-link:** ops uses `/me/entitlements` + workspace switcher. `ADMIN` is sufficient for webhook console routes and OrgAdmin policies. `SUPER_ADMIN` membership is only useful if product wants that string on workspace tools; **global** `IsSystemAdmin` is separate and must **not** be granted by provision.

**Do not create users** in P2 (unchanged).

### D-P2-5 — URL validation

Shared helper used by provision + `CreateWebhookEndpointCommand` (or endpoint layer):

- Required when creating.
- Trim; reject empty.
- `Uri.TryCreate` absolute URI.
- Scheme `https` only in Production; allow `http` for localhost in Development (or always reject non-https except loopback).
- Reject credentials in userinfo, reject obviously internal metadata hosts if cheap (optional).
- Max length e.g. 2048.

Today create accepts any non-whitespace string — **tighten as part of P2** for both paths.

### D-P2-6 — Default enabled events

- Provision inline: **empty list = all events** (domain default) unless optional `webhook_enabled_events: string[]` is passed.
- Companion API: same as existing `CreateWebhookEndpointRequestDto`.

Connect typically wants all commerce lifecycle events Aura cares about; empty-all is correct for MVP.

### D-P2-7 — Atomicity

Create path: org + membership? + entitlement + api credential + webhook endpoint → **one** `SaveChangesAsync`.  
Reuse same unique-violation → idempotent re-read path; after re-read, apply **ensure** rules for webhook/membership carefully (or re-enter a thin “ensure connect state” only when `Created: false` and request asked for webhook/owner).

**Concurrency note:** two concurrent first provisions already handled for org. Webhook ensure on the loser path must not double-insert for same URL — use pre-list + unique business rule, or catch unique if index added. **Recommended schema (optional P2):** unique index on `(OrganizationId, Url)` if not present — verify migration; if absent, application-level “list then create if missing” is OK for v1 with rare duplicate risk.

### D-P2-8 — Logging / secrets

- Never log `plain_key` or webhook `secret_key` (provision already omits plain key from logs).
- Log `webhook_endpoint_id`, URL host only (or full URL if already non-secret), `created`, `owner_attached`.

---

## 4. Concrete API contract (design)

### 4.1 Request extensions — `ProvisionWorkspaceRequestDto`

```ts
// Additive optional fields (TypeSpec + generated C# DTOs)
model ProvisionWorkspaceRequestDto {
  aura_org_id: string;
  display_name: string;
  slug?: string;
  owner_email?: string;
  /** Default ADMIN. Allowed: ADMIN | SUPER_ADMIN (workspace membership, not global). */
  owner_role?: string;
  is_test_mode?: boolean;
  key_name?: string;

  /** Absolute HTTPS URL of Aura (or integrator) webhook receiver. */
  webhook_url?: string;
  /** Optional event filter; omit/empty = all events. */
  webhook_enabled_events?: string[];
}
```

### 4.2 Response extensions — `ProvisionWorkspaceResponseDto`

```ts
model ProvisionWorkspaceWebhookDto {
  id?: string;
  url?: string;
  is_active?: boolean;
  enabled_events: string[];
  /** Full signing secret — only when newly created this call. */
  secret_key?: string;
  has_secret?: boolean;
  secret_hint?: string;
}

model ProvisionWorkspaceOwnerDto {
  /** true if membership exists after this call (created or pre-existing). */
  attached: boolean;
  status: "attached" | "user_not_found" | "not_requested";
  role?: string; // ADMIN | SUPER_ADMIN when attached
  email?: string;
}

model ProvisionWorkspaceResponseDto {
  workspace_id: string;
  slug: string;
  aura_org_id: string;
  created: boolean;
  api_key: ProvisionWorkspaceApiKeyDto;
  webhook?: ProvisionWorkspaceWebhookDto; // null/omitted if never registered
  owner?: ProvisionWorkspaceOwnerDto;
}
```

### 4.3 Command shape

```csharp
// Illustrative — implement later
public record ProvisionAuraWorkspaceCommand(
    string AuraOrgId,
    string DisplayName,
    string? Slug,
    string? OwnerEmail,
    string? OwnerRole,          // null → ADMIN
    bool IsTestMode,
    string? KeyName,
    string? WebhookUrl,         // NEW
    IReadOnlyList<string>? WebhookEnabledEvents, // NEW
    Guid? ActorUserId) : ICommand<ProvisionAuraWorkspaceResult>;

public record ProvisionAuraWorkspaceResult(
    Guid WorkspaceId,
    string Slug,
    string AuraOrgId,
    bool Created,
    Guid? ApiKeyId,
    string? Prefix,
    string? Hint,
    string? PlainKey,
    IReadOnlyList<string> Scopes,
    // Webhook
    Guid? WebhookEndpointId,
    string? WebhookUrl,
    bool? WebhookIsActive,
    IReadOnlyList<string> WebhookEnabledEvents,
    string? WebhookSecretKey,   // once
    string? WebhookSecretHint,
    // Owner
    bool OwnerAttached,
    string OwnerStatus,         // attached | user_not_found | not_requested
    string? OwnerRole);
```

### 4.4 Endpoint mapping (`Endpoints.cs`)

- Pass `req.Webhook_url`, `req.Webhook_enabled_events`, `req.Owner_role` into command.
- Map result → response DTOs; **never** log secrets.
- Validation errors (bad URL, bad role) → existing `InvalidOperationException` → 400 path.

### 4.5 Companion API changes

| Route | Auth today | P2 target |
|-------|------------|-----------|
| `GET …/webhooks` | membership or system admin | + `IntegrationWebhooksEndpointsManage` (API_CLIENT + scope) **or** membership |
| `POST …/webhooks` | ADMIN/SUPER_ADMIN membership | same + scope policy |
| `PUT …/webhooks/{id}` | ADMIN/SUPER_ADMIN membership | same + scope policy |
| `GET …/webhooks/logs` | membership | keep read at membership **or** add read scope later; out of P2 core |

Implementation pattern options:

1. Replace manual role checks with `RequireAuthorization("IntegrationWebhooksEndpointsManage")` **and** extend that policy to treat workspace membership ADMIN/SUPER_ADMIN the same way (policy already allows role SUPER_ADMIN/ADMIN **claims**, but workspace roles are injected only when tenant resolved — verify claim injection for JWT path still works).
2. Or keep manual checks and add branch: `(ctx.IsApiClient && HasScope(webhooks.endpoints:manage))`.

**Recommend (2)** for path-scoped workspace id vs key’s org: require `id == ctx.TenantId` for API_CLIENT (fail closed on cross-tenant path). JWT path continues membership check on path `id`.

### 4.6 Scope bundle decision

**Recommended for Connect:**

```text
DefaultAuraIntegratorScopes =
  payments.checkouts:write
  payments.checkouts:read
  webhooks.endpoints:manage
```

- Update `PlatformApiScopes.DefaultAuraIntegratorScopes`.
- Update provision tests that assert exact scope string.
- OrgAdmin-minted keys remain free to omit the scope.

**Idempotent existing keys** minted before this change will **not** magically gain the scope — companion path for old workspaces needs remint or provision-inline webhook only. Document as migration note.

---

## 5. Handler algorithm (provision)

```
Normalize aura_org_id, display_name
Validate owner_role if provided
Validate webhook_url if provided (D-P2-5)

existing = GetByExternalRef(aura, aura_org_id)
if existing != null:
    return EnsureAndBuildExisting(existing, request)   // no remint key; ensure webhook/owner rules

// --- create ---
org = new Organization(...)
BindExternalRef(aura, id)

ownerStatus = "not_requested"
if OwnerEmail:
    user = GetUserByEmail
    if user:
        AddTenantMembership(user, org, role)
        ownerStatus = "attached"
    else:
        ownerStatus = "user_not_found"

AddEntitlement PAYMENTS; publish event

Mint api key (DefaultAuraIntegratorScopes incl. webhooks manage if D-P2-6 scope decision)

webhookSecret = null
if WebhookUrl:
    secret = "whsec_" + GenerateSecureToken(24).PlainToken
    endpoint = new TenantWebhookEndpoint(org.Id, url, secret, true, events)
    AddWebhookEndpoint(endpoint)
    webhookSecret = secret

SaveChanges (unique → race → EnsureAndBuildExisting)

return full create result including secrets once
```

**`EnsureAndBuildExisting`:**
1. Load bootstrap key metadata (existing logic).
2. Owner: if email provided and user exists and no membership → add membership + SaveChanges (small write). If already member → attached. Report status.
3. Webhook: if `webhook_url` provided:
   - List endpoints; if exact URL exists → return metadata, secret null.
   - Else create endpoint + SaveChanges → return secret once.
4. If `webhook_url` omitted → surface first active endpoint metadata without secret (optional convenience) or omit webhook object.

---

## 6. Code touch list (implementation later)

| Area | Files / artifacts |
|------|-------------------|
| Command | `ProvisionAuraWorkspaceCommand.cs` — fields, create + ensure paths |
| Webhook command | Optionally extract secret mint helper shared with `CreateWebhookEndpointCommandHandler`; URL validation |
| Repository | Already has `AddWebhookEndpoint`, `ListWebhookEndpointsAsync` — use in provision handler |
| Endpoint | `Endpoints.cs` provision mapping; webhook routes auth |
| Scopes | `PlatformApiScopes.cs` DefaultAura |
| Host | Possibly no policy change if companion uses manual scope branch; else `Program.cs` already has policy |
| TypeSpec | `models.tsp` DTOs; `routes.tsp` doc comments |
| Contracts | Regenerate `api-types-dotnet` / ts via `task gen` when implementing |
| Tests | See §7 |

**Out of scope for P2:**
- Secret rotation endpoint
- User auto-create / invite email on provision
- Changing existing membership roles
- Product-URL fulfillment gating (already fixed in B.4)
- Ops UI changes (optional later: show Connect webhook)
- Deep-link URL construction in Hub (Aura builds URL from `workspace_id` / `slug` + known ops base)

---

## 7. Tests needed

### 7.1 Unit — `ProvisionAuraWorkspaceTests` (extend)

| Test | Assert |
|------|--------|
| `Provision_Create_With_WebhookUrl_Returns_Secret_Once` | Endpoint added; `WebhookSecretKey` starts with `whsec_`; `Created`; single `SaveChanges`; list has matching URL |
| `Provision_Idempotent_With_WebhookUrl_No_Secret_Remint` | Second call: same endpoint id; secret null; no second AddWebhook for same URL |
| `Provision_Idempotent_Heal_Missing_Webhook` | First create without URL; second with URL → creates endpoint, secret once, `Created: false` |
| `Provision_Without_WebhookUrl_Omits_Webhook` | No AddWebhookEndpoint |
| `Provision_Rejects_Invalid_WebhookUrl` | relative URL / http (prod rules) / empty after trim → `InvalidOperationException` |
| `Provision_Owner_Admin_When_User_Exists` | `AddTenantMembership` with `ADMIN`; `OwnerAttached` |
| `Provision_Owner_SuperAdmin_When_Requested` | role `SUPER_ADMIN` |
| `Provision_Owner_UserNotFound_Does_Not_Fail` | no membership; status `user_not_found`; still creates workspace |
| `Provision_Owner_Invalid_Role_Rejected` | 400-class exception |
| `Provision_Owner_Idempotent_Does_Not_Duplicate_Membership` | second call no second Add when already member |
| `Provision_Default_Scopes_Include_Webhooks_Manage` | if D-P2-6 adopted; else assert absence documented |
| Existing tests | Update constructors for new command params; keep plain_key / race / auth tests green |

### 7.2 Unit — webhook create / companion

| Test | Assert |
|------|--------|
| `CreateWebhookEndpoint_Validates_Https` | shared validation |
| Policy/scope: API_CLIENT **with** `webhooks.endpoints:manage` can authorize Integration policy | extend `ApiKeyAuthenticationTests` pattern used for payments scopes |
| Policy: API_CLIENT **without** scope fails | |
| Companion path IDOR: key org A cannot POST webhooks for workspace B | path id ≠ TenantId → 401/403 |

### 7.3 Unit — fan-out still works

Reuse / extend `OutboundWebhookTests`:
- Endpoint created with provision-equivalent fields receives `subscription.activated` fan-out (already covered generically if endpoint active).

### 7.4 Contract / mapping (light)

- Map DTO field names snake_case in endpoint test or pure mapping unit if introduced.
- TypeSpec compile after model change (implementation phase).

### 7.5 Explicitly not required for P2 analysis exit

- Full host E2E Aura → provision → pay → webhook delivery (manual residual).
- Load/rate-limit changes beyond existing provision limiter.

---

## 8. Security notes

1. **Provision secret** remains the only non-SUPER_ADMIN way to call provision; webhook registration inherits that trust boundary for inline path.
2. **Secret once** for both `plain_key` and `webhook.secret_key`; idempotent responses must not re-emit.
3. **Do not** put `webhooks.endpoints:manage` on unrestricted “all scopes” mints without product review; Aura default is intentional Connect surface.
4. **Owner SUPER_ADMIN** is workspace-scoped string only — never set `GlobalUser.IsSystemAdmin`.
5. **SSRF:** outbound dispatcher already posts to customer URLs; validating https reduces footguns but does not replace dispatcher allow/deny if added later.
6. Cross-tenant companion calls: bind path workspace id to API key’s `OrganizationId`.

---

## 9. Suggested implementation order (when coding starts)

1. Shared URL validation + optional scope default change + tests.  
2. Extend `ProvisionAuraWorkspaceCommand` create path (webhook + owner_role + owner status).  
3. Idempotent ensure path (heal webhook, attach owner).  
4. Endpoint + TypeSpec DTO mapping.  
5. Companion auth on webhook POST/PUT/GET + IDOR tests.  
6. Update `DefaultAuraIntegratorScopes` consumers/tests/docs-one blurb.  
7. Manual Connect checklist: provision with webhook_url → store secret in Aura → trigger test event → verify HMAC.

---

## 10. Acceptance criteria (P2 done)

- [ ] Aura can call provision with `webhook_url` and receive `webhook.secret_key` **only** on first materialization of that endpoint.
- [ ] Re-provision same `aura_org_id` never remints API key or webhook secret; may heal missing webhook once.
- [ ] `owner_email` of existing user yields workspace membership `ADMIN` (or requested `SUPER_ADMIN`) so `/me/entitlements` lists the workspace for deep-link.
- [ ] Missing owner user does not fail provision; response communicates not attached.
- [ ] Companion: key with `webhooks.endpoints:manage` can create endpoint for **its** workspace; without scope cannot.
- [ ] Module tests in §7 green; no secret in logs.
- [ ] TypeSpec models document new fields.

---

## 11. Open questions (resolve at implement PR if needed)

1. **Include `webhooks.endpoints:manage` in Aura default scopes?** — Recommend **yes** for companion parity (D-P2-6).  
2. **Heal membership on idempotent re-call when owner was missing then registered later?** — Recommend **yes** (symmetric to webhook heal).  
3. **Tag Connect endpoints** (e.g. fixed name field) vs URL-only match? — Domain has no `Name` on `TenantWebhookEndpoint` today; URL-only is enough for v1.  
4. **Return ops deep-link URL from Hub?** — Nice-to-have; Aura can compose from config + `slug`/`workspace_id`. Out of P2 unless product asks.

---

## 12. Summary

P2 makes integrator provision **Connect-ready** by (1) optionally creating a signed outbound webhook endpoint in the same transactional provision as workspace + bootstrap key, returning `whsec_` **once**, (2) clarifying optional owner membership as `ADMIN`/`SUPER_ADMIN` with explicit attach status for deep-link, and (3) enabling a **companion** webhook create API for machine clients via existing `webhooks.endpoints:manage` scope + policy wiring that is defined but unused today. Design reuses `CreateWebhookEndpointCommand` secret format and multi-endpoint domain model; idempotency mirrors API key reveal-once semantics with ensure-heal for missing Connect pieces.
