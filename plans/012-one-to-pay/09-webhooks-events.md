# 09 — One → Pay webhooks and events (HMAC)

**Family:** 012-one-to-pay  
**Paper:** 09 — control-plane events Pay may consume from lazuar-one  
**Date:** 20 August 2026  
**Type:** Analysis only. **Do not implement** from this file. **Do not** add a Pay webhook route, **do not** register an endpoint on One, **do not** flip `NP-ONE-017` / `NP-ONE-018` / `NP-ONE-019`, **do not** tail Zitadel.

**Repos and SHAs (this write-up):**

| Tree | Path | HEAD |
|------|------|------|
| Pay (this tree) | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay` | `6ca8f19f4b28c056f852b7b579b5b30428e48ad6` (`feat(pay): add TypeSpec package for the focused Pay host`) |
| One (sibling) | `/Users/akmalfirdaus/Code/lazuar/lazuar-one` | `0f79fe4f6503847881286ead2e7e57b7c7dc1808` (`WIP: Thu Aug 20 21:24:22 +08 2026`) |

**Must-read sources actually opened:**

- Pay `plans/011-new-lazuar-pay/02-one-integration.md` — Events Pay should subscribe to; secrets; two planes.
- Pay `plans/011-new-lazuar-pay/03-first-slice.md`, `11-checklist.md` (`NP-ONE-017` / `018` / `019`), `12-first-slice-tracker.md` step 6.
- Pay `plans/011-new-lazuar-pay/01-product.md`, `07-separate-vs-one-binary.md`, `08-bezos-door.md`, `13-monolith-vs-services.md`.
- One `plans/017-evals/08-dogfood-then-serve.md` §6.8 (and the surrounding §6 first-party product checklist). That paper was written against One `main` @ `11f6082` (2026-08-18). Catalog and HMAC on live One `0f79fe4` still match; this paper re-reads TypeSpec + C# rather than quoting 08 as current truth.
- One TypeSpec: `packages/api-spec/modules/webhooks/{routes,models}.tsp`, `packages/api-spec/modules/tenants/{routes,models}.tsp` (`GET /tenants/{id}/events`).
- One implementation: `WebhookEventCatalog.cs`, `WebhookService.cs`, `WebhookDispatcher.cs`, `WebhookSigning.cs`, `WebhookFanoutOutboxHandler.cs`, `WebhookEventPublisher.cs`, `WebhookUrlValidator.cs`, `EventEndpoints.cs`, publishers in `TenantService` / `MembershipService` / `ApiKeyService` / `OidcAppService`.
- One docs: `apps/lazuar-docs/docs/integrations/webhooks.md`, recipe R5 `docs/recipes/webhook-verify.md`, sample `examples/node-webhook-verify/`.
- One tracker IDs: EVT-02 (closed catalog), EVT-07 (events pull). Issues 009 / 035 / 036 are **Done** on this SHA (SKIP LOCKED, lease ≥ batch × timeout, `tenant.deleted` keeps endpoints until fan-out).

**Assigned slice (binding):** Prefer HMAC **push**. Pull `GET /tenants/{id}/events` if Pay cannot take push. Do **not** tail Zitadel. The **first connection** slice must **not** require webhooks — `GET /me` + `authz/check` is enough before money. This paper documents how to add the receiver **next**, not now.

---

## 0. How to read this paper

This is the honesty paper that stops Pay from building a second event bus, and also stops Pay from blocking login on a webhook that is not needed yet.

Three different “webhook” stories live in this family. Mixing them is how the old tree got an in-process catalog talking to itself **and** a public HMAC door **and** Stripe/CHIP/Billplz callbacks all called “webhooks”:

| Plane | Direction | Job | When |
|-------|-----------|-----|------|
| **A. One → Pay** (this paper) | One POSTs signed JSON to a Pay URL | Membership, tenant lifecycle, key revoke | After connection works; **mandatory before live charges** for `tenant.suspended` |
| **B. PSP → Pay** (`NP-GW-004` / `NP-GW-006`) | Stripe / CHIP / Billplz POST to Pay | Money. Verify, idempotent `(tenant, provider, event_id)`, journal + `RCPT-` in one handler | S1 money slice. Unrelated HMAC. |
| **C. Pay → merchant / second app** (Bezos door, later) | Pay POSTs to a stranger | `payment.completed` and friends | Not v1 for first-party dogfood. Old Hub outbound is museum. |

Plane A is **lazuar-one’s** product: closed catalog, `whsec_…`, `X-Lazuar-Signature: v1=<hex>`. Plane B is **Pay’s** product: provider SDK or provider HMAC, different headers, different idempotency tuple. Do not share a table, a secret, or a route prefix. Do not implement Plane A in order to “practice” Plane B.

**What “first connection” is allowed to mean here.** Papers in this 012 family that wire Pay as Consumer-0 over HTTP: register the SPA, sign in via `:5175`, `GET /me`, path `{tenantId}` + membership, `authz/check` before merchant admin routes, mint a scoped `lzr_sk_`. That loop is **whoami + authz**. It does not move money. It does not need One to POST anything at Pay.

**What 011 already said, and how 012 narrows it.** `03-first-slice.md` step 6 and `12-first-slice-tracker.md` step 6 put “Subscribe to `member.*` and `tenant.suspended`” on the One side of the dogfood loop, IDs `NP-ONE-017` / `NP-ONE-018`. `08-dogfood-then-serve.md` §6.11 step 6 is the same sentence. §6.12 even has a printable checkbox “at least one webhook received and HMAC-verified.” That checkbox is **real**, and it is **not** a login gate. This paper splits S0:

1. **Connection (now):** `/me` + `authz/check`. Webhooks deferred.
2. **Money gate (next, still before the first live charge):** HMAC receiver + `tenant.suspended` → stop new charges. That is when `NP-ONE-017` / `NP-ONE-018` actually earn `Dogfood = Y`.

`NP-ONE-017` is marked Dogfood **Y** in `11-checklist.md`. The printed dogfood sentence in `12-first-slice-tracker.md` is: merchant via One → keys → buyer (no One account) pays → `RCPT-` + balanced journal → **webhook retry no-ops** → MEMBER sees ops, VIEWER cannot charge. The “webhook retry no-ops” in step 11 is the **PSP** webhook (`NP-GW-006`), not One’s `member.accepted`. A missing One receiver does not fail that sentence. A missing Stripe/CHIP/Billplz idempotency key does. Do not let the S0 Dogfood flag on `NP-ONE-017` block connection work.

---

## 1. Standing law (do not weaken)

1. **Prefer HMAC push.** One already delivers signed POSTs. Pay should receive them, not poll as the happy path.
2. **Pull `GET /tenants/{id}/events` if Pay cannot take push** (laptop SSRF, no public URL, dispatcher disabled in a test host). Pull is catch-up and a fallback, not a second SoT.
3. **Do not tail Zitadel.** No Zitadel event stream, no Console “message sink,” no `urn:zitadel:iam` parsing. One’s outbox is the catalog. Zitadel is One’s problem.
4. **Do not put buyer entitlement in One.** Cardholders never become Zitadel humans. Pay’s subscription / checkout session row is access. One membership is **staff**. If a One webhook is late, **money in Pay is still true**; staff chrome may lag. That lag is the cost of the split (`02-one-integration.md`, `07-separate-vs-one-binary.md` rule 2).
5. **First connection does not require this paper’s receiver.** Whoami + authz is enough before money.
6. **`tenant.suspended` becomes mandatory before live charges.** Not before `GET /me`. Not after the first Stripe `payment_intent.succeeded`. Before Pay will take a buyer’s money for that tenant.
7. **Pay holds the receiver HMAC secret** (`whsec_…`, shown once). One holds the AES-wrapped copy. Pay never holds `Webhooks:SigningSecretEncryptionKey`, Zitadel PAT, or OpenFGA admin (`NP-ONE-020`, `NP-XX-017`).
8. **Do not implement from this file.** The sketch in §15 is so the next slice has a place to start. It is not a ticket list for this week.

---

## 2. What is implemented on One (live SHA)

One’s webhook product is real. It is not a slide. `lazuar-app` has Settings → Integrations → Webhooks, a delivery log, rotate, test ping. Recipe R5 and `examples/node-webhook-verify` verify HMAC. That is Consumer-0’s door. Pay does not need to invent one.

### 2.1 HTTP Pay would call (later) to *register*

Base: One `/api/v1` (documented local `http://localhost:8080/api/v1`; this laptop often remaps One API to **8081** because Aura owns 8080 — see §9.3).

From TypeSpec `packages/api-spec/modules/webhooks/routes.tsp` and `WebhookEndpoints.cs`:

| Method | Path | Auth | Returns |
|--------|------|------|---------|
| `POST` | `/tenants/{tenantId}/webhooks` | JWT **admin\|owner**, or API key `webhooks:write` (or `admin` / `*`) | **201** `WebhookEndpointCreatedResponse` including **`secret` once** |
| `GET` | `/tenants/{tenantId}/webhooks` | JWT admin\|owner, or key `webhooks:read` | Paginated metadata. **No raw secret** — `secret_prefix` only |
| `GET` | `/tenants/{tenantId}/webhooks/{webhookId}` | same as list | Metadata only |
| `PATCH` | `/tenants/{tenantId}/webhooks/{webhookId}` | write | URL / events / description / `status` (`active` \| `disabled`) |
| `DELETE` | `/tenants/{tenantId}/webhooks/{webhookId}` | write | **204** |
| `POST` | `/tenants/{tenantId}/webhooks/{webhookId}/rotate-secret` | write | **200** new `secret` once. **Previous `whsec_…` stops verifying immediately** (no dual-verify window) |
| `POST` | `/tenants/{tenantId}/webhooks/{webhookId}/test` | write | **202** `{ delivery_id, event_id }` — enqueues `webhook.test` even if the filter omitted it |
| `GET` | `/tenants/{tenantId}/webhooks/{webhookId}/deliveries` | read | Delivery log (status, attempts, snippet, next_attempt) |
| `GET` | `/webhook-event-types` | any authenticated user | `{ events: string[], api_version: "v1" }` — closed catalog |

Create/update: omit `events` or pass `[]` → subscribe to **all** v1 catalog types. Unknown type strings → **400** with the allowed list. Attribute / JSONPath filters do not exist.

JWT: `RequireMembershipAsync(..., minRole: "admin")`. Members and viewers cannot register Pay’s receiver. API keys: `webhooks:write` is explicit; empty scopes are not admin (`ApiKeyScopeHelper`, P12 / D68). Default mint is `["tenant:read"]` — **not** enough to register a webhook. When Pay later registers, mint `webhooks:write` (and `events:read` if pull is in play) on purpose. Do not mint `*`.

Suspended tenant: **create** is 403 (`Tenant cannot create webhooks in its current status`). List/get/deliveries use `TenantAccessMode.AllowSuspended` so operators can still see the log after a suspend.

`@lazuar/one-client` `createClient()` on this SHA wraps `me`, `tenants`, `apiKeys`, `authz` only. Webhook CRUD lives in `generated.ts` OpenAPI types but is **not** on the hand-written `Client`. First-party Pay may raw-fetch or extend the workspace client later. Do not wait on npm (`NP-XX-021`).

### 2.2 HTTP Pay would call (later) to *pull*

TypeSpec `EventOperations.listEvents` — `GET /tenants/{tenantId}/events?cursor=&limit=`.

- Auth: membership + `events:read`. JWT: ROLE-03 catalog (`owner` has all nine permissions including `events:read`; `admin` has all except `tenant:delete`; `member` has custom-role list or `[]`). API key: scope `events:read` (or `admin` / `*`).
- Cursor page, `limit` clamped 1–200, default 50. Invalid cursor → **400**.
- Implementation (`EventEndpoints.cs`): reads `outbox_messages` for that tenant, **excludes** `EventType == "audit.stream"`, does **not** filter `Status == completed`. Pending outbox rows are visible as soon as the domain `SaveChanges` committed. That is useful for catch-up.
- Mapped body: `{ id, type, created_at, data }`. **No** `tenant_id`, **no** `api_version`, **no** lock columns. `id` is the outbox GUID — the same value One puts on `X-Lazuar-Event-Id` when it POSTs.
- **Does not use `AllowSuspended`.** After `tenant.suspended`, a member `GET /events` is **403** `"Tenant is suspended."` Platform admin can still read. This is why pull is **not** a complete substitute for the suspend push: the event that says “stop charges” is the one you can no longer list. Fallback for suspend-without-push is **`GET /tenants/{id}`** (AllowSuspended, D32) or **`GET /me`** (`TenantSummary.status` is present for non-deleted tenants, including `suspended`).

`GET /tenants/{id}/audit` is a different product (persistable audit rows, secrets redacted). Do not confuse it with EVT-07. SIEM audit **streams** (`lzr_strm_…`) reuse the same HMAC algorithm and are not Pay’s receiver.

### 2.3 What Pay must never call

- Zitadel Console event/message APIs.
- Zitadel Admin `ListEvents` / any PAT-backed tail.
- One `POST /platform/tenants` (staff directory — `NP-XX-023`).
- One `authz/write` (`NP-XX-016`).
- Parsing `urn:zitadel:iam:org:project:roles` (`NP-XX-024`).

---

## 3. Event catalog as implemented

Closed v1 list is `WebhookEventCatalog.All` (`apps/lazuar-api/src/Lazuar.One.Api/Features/Webhooks/WebhookEventCatalog.cs`), `api_version = "v1"`. `GET /webhook-event-types` returns this list. Fan-out **skips unknown types**, so internal `audit.stream` rows never POST to customer URLs.

`oidc_app.updated` is **not** a type (no PATCH apps). Docs and 08 §6 say this out loud.

### 3.1 The seventeen types (produced when)

| Type | Producer (this SHA) | `data` actually enqueued (no secrets) | Pay use if subscribed |
|------|---------------------|----------------------------------------|------------------------|
| `tenant.created` | `TenantService` provision end, when status flips to `active` | `{ slug, status }` | Provision Pay catalog/ledger rows **if** the workspace was created outside Pay. Not required if Pay just `POST /tenants` and uses the response. |
| `tenant.deleted` | `TenantService.DeleteAsync` (owner wipe) | `{ slug, status: "deleted", actor_user_id }` | Stop charges, tombstone Pay rows, honest leftovers. Issue **036 is Done**: endpoints are **kept** until this row fans out (previously `RemoveRange` made the outbox a no-op). |
| `tenant.suspended` | `TenantService.SuspendAsync` (idempotent if already suspended — **no second event**) | `{ status, previous_status, actor_user_id }` | **Stop new charges** and staff admin. Money already captured stays booked. |
| `tenant.reactivated` | `TenantService.ReactivateAsync` (idempotent if already active — **no second event**) | `{ status, previous_status, actor_user_id }` | Re-enable charges. Pair with suspend. |
| `member.invited` | `MembershipService` invite create | `{ invite_id, email, role, invited_by_user_id, expires_at }` — **never** raw token or hash | Optional chrome (“pending invite”). Pay does not need a cache of invites; One is SoT. |
| `member.accepted` | Invite accept path only | `{ membership_id, user_id, email, role, invite_id }` | Sync **staff** cache if any. **Not** produced for domain auto-join or SSO JIT — see §3.3. |
| `member.removed` | Admin remove; also SCIM disable (`reason: "scim"`) | `{ membership_id, user_id, role, reason }` (`"removed"` or `"scim"`) | Drop cached staff session / chrome. |
| `member.left` | Member leave | `{ membership_id, user_id, role, reason: "left" }` | Same as removed. |
| `member.role_changed` | Role PATCH | `{ membership_id, user_id, previous_role, role, actor_user_id }` | VIEWER/MEMBER chrome. Re-check `authz` anyway. |
| `ownership.transferred` | Transfer ownership | `{ tenant_id, previous_owner_user_id, new_owner_user_id, actor_user_id }` | “Billing owner” if Pay ever prints a legal owner. Not needed for first charges. |
| `api_key.created` | Key mint | `{ key_id, name, prefix, scopes, created_by }` — **never** `lzr_sk_` | Inventory. Pay should not cache other people’s keys. |
| `api_key.revoked` | Key revoke (idempotent if already revoked — **no second event**) | `{ key_id, name, prefix, scopes, revoked_at }` | Drop **cached** One secrets if Pay cached them. Env-held Pay server key is an ops rotate, not this event. |
| `oidc_app.created` | App register | `{ app_id, name, type, client_id, status, redirect_uris }` — **never** client_secret | Pay’s own SPA already knows its `client_id`. Ignore unless Pay inventories apps. |
| `oidc_app.revoked` | App revoke | `{ app_id, client_id, status }` | If Pay’s SPA is revoked, login breaks; webhook is not the detector — OIDC will fail. |
| `invite.revoked` | Revoke or replace | `{ invite_id, email, actor_user_id }` | Optional. |
| `invite.resent` | Resend | `{ invite_id, email, actor_user_id }` | Optional. Token still not in payload. |
| `webhook.test` | `POST …/webhooks/{id}/test` only | `{ endpoint_id, message, livemode: false }` | Prove HMAC. Inserted **directly** as a delivery; fan-out skips this type if it ever appears on the outbox. **Does not** appear on `GET /events`. |

### 3.2 Envelope on the wire (push)

Dispatcher POSTs `delivery.PayloadJson`, which is `OutboxMessageSerializer` snake_case JSON:

```json
{
  "id": "<outbox guid = event id>",
  "type": "tenant.suspended",
  "created_at": "<ISO-8601>",
  "tenant_id": "<tenant guid>",
  "api_version": "v1",
  "origin_request_id": null,
  "data": { }
}
```

`origin_request_id` is almost never set from HTTP (`IWebhookEventPublisher` has no request-id parameter). Do not build correlation on it. `DefaultIgnoreCondition = WhenWritingNull` so a null origin field may be omitted.

Headers on every delivery (`WebhookDispatcher.ProcessOneAsync`):

| Header | Value |
|--------|--------|
| `Content-Type` | `application/json; charset=utf-8` |
| `User-Agent` | `Lazuar-One-Webhooks/1.0` |
| `X-Lazuar-Event-Id` | Outbox / event GUID (idempotency key) |
| `X-Lazuar-Event-Type` | Catalog type |
| `X-Lazuar-Tenant-Id` | Tenant GUID |
| `X-Lazuar-Timestamp` | Unix **seconds** UTC |
| `X-Lazuar-Signature` | `v1=` + lowercase hex HMAC-SHA256 |
| `X-Lazuar-Delivery-Id` | Delivery row GUID (attempt identity, **not** the idempotency key) |

Body is the **exact** UTF-8 bytes HMAC’d. Re-serializing JSON before verify will fail. Empty body is possible only if someone stored empty `PayloadJson`; One’s publisher always writes an envelope. Pay should still 400 a truncated/empty body the same way PSP webhooks do (`NP-GW-005` is the PSP twin — do not share the handler).

### 3.3 Names that look like events but are not in the webhook catalog

`lazuar-app` `workspaceEvents.ts` labels many more strings for the **audit / events UI**: `tenant.updated`, `member.auto_joined`, `member.sso_joined`, `domain.*`, `role.*`, `authz.denied`, `sso.connection_*`, `scim.*`, `audit_stream.*`. Those are **audit log** names (`AuditLog.Events`), not `WebhookEventCatalog`.

Consequences for Pay:

- **Domain auto-join** (`DomainJoinService`) and **SSO JIT join** (`SsoJoinService`) write membership + FGA + audit `member.auto_joined` / `member.sso_joined`. They do **not** call `IWebhookEventPublisher`. There is **no** `member.accepted` (or any catalog type) for those joins.
- Therefore **Pay must not treat `member.*` webhooks as the membership directory.** `GET /me` is the directory (`02-one-integration.md`). A cache filled only from webhooks will miss JIT/domain joins until the next `/me`.
- This is another reason the first connection slice skips webhooks: the SoT call is already `/me`.

### 3.4 011 / 08 list vs full catalog

`02-one-integration.md` and 08 §6.8 ask Pay to care about a **subset**:

- `member.accepted` / `removed` / `left` / `role_changed`
- `ownership.transferred`
- `tenant.suspended` / `reactivated`
- `tenant.created`
- `api_key.revoked`

That subset is the product intent. The closed catalog is larger. When Pay **does** register, either:

- subscribe to the subset explicitly (unknown types 400; extra catalog types simply never arrive), or
- omit `events` (all seventeen, including `webhook.test` on the test route anyway, plus `member.invited`, `oidc_app.*`, `tenant.deleted`, …) and ignore types Pay does not handle.

Prefer an **explicit subset** so a future catalog addition does not start hitting an unready handler. Include `webhook.test` on the filter **or** rely on the test route bypass (test route does not require the filter). Including `tenant.reactivated` whenever `tenant.suspended` is subscribed is mandatory for a coherent stop/start.

---

## 4. HMAC as implemented

### 4.1 Secret

`WebhookService.GenerateSecret()`: `whsec_` + 32 random bytes, base64url (no padding, `+/` → `-_`). Prefix stored as first 16 characters of that string (`secret_prefix`). Full secret is AES-256-GCM at rest (`AesWebhookSecretProtector`), AAD bound to endpoint id, wire `v1.{nonce}.{ciphertext||tag}`. One’s `Webhooks:SigningSecretEncryptionKey` is a 32-byte base64 key; empty is a **deterministic dev fallback** (logged once). Staging/Production must set a real key. **Pay never sees that AES key.** Pay sees `whsec_…` once at create/rotate and must persist it.

List/get never return `secret`. Lose it → rotate. Rotate invalidates immediately. Recipe common error: “wrong secret (rotated).”

### 4.2 Signature

`WebhookSigning.ComputeSignature`:

```text
signed_payload = "{unix_seconds}." + raw_body_bytes
digest         = HMAC-SHA256(key = full whsec_ string as UTF-8, msg = signed_payload)
header         = "v1=" + lowercase_hex(digest)
```

Receiver algorithm (R5 + sample):

1. Parse `X-Lazuar-Timestamp` as unix seconds. Reject if not finite.
2. Reject if `abs(now - ts) > 300` seconds (`Webhooks:ReplaySkewSeconds`, sample `LAZUAR_WEBHOOK_SKEW_SECONDS`). This is the replay window, not a business SLA.
3. HMAC the **raw** body. Constant-time compare to `v1=<hex>` (sample compares the full `v1=` header bytes with `timingSafeEqual`; length mismatch fails closed).
4. Process **idempotently** on `X-Lazuar-Event-Id` (see §10).
5. Respond **2xx quickly**. Non-2xx → One retries.

Clock: use seconds, not ms. Drift > 300s looks like a replay.

### 4.3 Delivery machinery (so Pay can predict retries)

- Outbox row committed **in the same `SaveChanges` as the domain mutation** (`EfOutbox` does not call `SaveChanges`; caller does). If the mutation rolls back, there is no event. If it commits, the event exists even if Pay is down.
- Fan-out (`WebhookFanoutOutboxHandler`) inserts `webhook_deliveries` for each matching **active** endpoint. Unique index `(endpoint_id, event_id)`. Re-handle of the outbox is a no-op on conflict.
- Dispatcher worker: in-process `WebhookDispatcherWorker`, poll `PollIntervalMs` default 1000. Postgres claim: `FOR UPDATE SKIP LOCKED` + lease (issue **009 Done**). `EffectiveLeaseSeconds()` = max(configured lease, batch × HTTP timeout), cap 3600 (issue **035 Done**; `appsettings.json` has `LeaseSeconds: 160` with `DispatcherBatchSize: 20` × `HttpTimeoutSeconds: 10` = 200 → effective 200).
- HTTP: 10s timeout, **no redirects**, connect **pinned** to the SSRF-validated IP, SNI/Host stay the registered hostname.
- Attempts: `MaxAttempts` default **7**. Backoff seconds after failure: 30s, 2m, 10m, 1h, 6h, 24h, ±10% jitter.
- Auto-disable: **15** consecutive failures → endpoint `disabled`, reason `auto_disabled_consecutive_failures`. Fan-out stops. In-flight deliveries for a disabled endpoint go **dead** (`endpoint_disabled`). Success resets the streak. Manual `PATCH status=active` re-enables. **No owner email** on auto-disable yet.
- At-least-once: a crash after Pay’s 2xx and before One’s `succeeded` write retries the **same** `event_id` with a new `delivery_id` / attempt. Unique row ≠ unique HTTP. Receiver idempotency is mandatory.

### 4.4 SSRF (local Pay will feel this)

`WebhookUrlValidator`:

- HTTPS required outside Development/Testing. HTTP allowed when `AllowHttpInDevelopment` and env is Development or Testing.
- Strict environments (`EnvironmentRules.IsStrict`): port must be in `AllowedPorts` (default **443 only**). Dev/Testing: any positive port (so **8081 is legal in Development**).
- No userinfo in the URL. Max 2048 chars.
- Loopback, RFC1918, link-local, CGNAT, metadata, IPv6 ULA/link-local/`::1` are **blocked** unless the host is on `Webhooks:UrlHostAllowlist` (staff hatch; default **empty**). Allowlist skips the private-IP deny; it does not skip HTTPS-in-prod.
- `host.docker.internal` may or may not resolve to a blocked address depending on OS. 08/010 evals already called local receive “hostile by design.”

So: `http://127.0.0.1:8081/v1/one/webhooks` **fails** SSRF (loopback). `http://localhost:8081/...` **fails** unless `localhost` is allowlisted. Compose-to-host often needs `Webhooks:UrlHostAllowlist` containing `host.docker.internal` (or the host LAN name) **on the One API**, not on Pay. This is One ops for laptop dogfood, not a Pay feature. Staging/prod: public HTTPS URL (tunnel if the receiver is still a laptop).

Do not weaken One’s CIDR list from Pay. Do not ask One for `AllowPrivateOnAllowlist` as a product. Use the documented hatch.

---

## 5. Why the first connection slice can skip webhooks

Connection, on this family, is: a human who already exists in One can use Pay as a merchant surface.

That requires:

| Call | Why it is enough |
|------|------------------|
| OIDC access_token as Bearer | AuthN. Not this paper. |
| `GET /me` | `user_id`, email, `tenants[]` (`id`, `slug`, `name`, `role`, **`status`**, `permissions`), `active_tenant_id`. Directory + **lifecycle status** without a webhook. Can write (domain auto-join, SSO JIT) — do not hammer it from a hot loop (`NP-ONE-006`). |
| Path `{tenantId}` + membership | Authorization SoT. Header `X-Lazuar-Tenant-Id` is a hint (`NP-ONE-007`). |
| `POST /tenants/{id}/authz/check` | `member` / `admin` / `owner` before merchant admin routes (`NP-ONE-015`). Allow-list `{ tenant, app }` only. |

None of those need One to reach Pay’s port. One does not know Pay exists except as an OIDC `client_id` and maybe a `lzr_sk_`.

**No charges yet.** There is nothing to stop. `tenant.suspended` as a **charge kill-switch** is meaningless until Pay will accept a payment for that tenant. A suspended workspace still shows up on `GET /me` with `status: "suspended"`; Pay chrome can hide “start accepting payments” without a push.

**Staff lag is OK.** If Ada removes Bob while Bob has a Pay tab open, Bob might still see ops until the next `/me` or `authz/check`. `02-one-integration.md`: “If the webhook is late, **money in Pay is still true**; staff access may lag. That is the cost of this split.” Connection has no money. A stale MEMBER badge for one request is not a ledger bug. Do not build a membership replica “so chrome is instant.” Chrome should call `/me` and `authz` (batch-check later, `NP-ONE-016`).

**`member.*` cannot be the directory even later.** Auto-join / SSO JIT do not emit catalog events (§3.3). A webhook-only cache is wrong. Skipping the receiver on connection is not a shortcut around a complete design — it is the design: **One HTTP is SoT for staff**.

**`tenant.created` is not how Pay first sees the workspace.** “Create workspace” in Pay **is** `POST /tenants` (`NP-ONE-009`). `TenantService.CreateAsync` inserts `status=provisioning`, then **awaits** `CompleteProvisioningAsync` (Zitadel org → owner membership → OpenFGA owner tuple → `status=active` + `tenant.created` on the same `SaveChanges`) **before** the HTTP handler returns 201. CreateTenantResponse embeds `Tenant`. On the success path Pay already has an active id in the response and can upsert `org_id = tenant.id` in the same Pay request. If provision throws, One marks `failed` and the HTTP call errors; Pay uses `POST /tenants/{id}/retry-provision` (break-glass), not a webhook wait. Idempotency replay may return an existing row that is still `provisioning`/`failed` — check `status` on the response; do not assume 201 means `active`. Workspaces created in `lazuar-app` then opened in Pay can be **lazy-provisioned** on first Pay hit for that id. `NP-ONE-019` (Dogfood **—**, not Y) does not have to be a webhook handler.

**`api_key.revoked` is not how Pay drops a key it never cached.** First connection mints a `lzr_sk_` and puts it in Pay server env (shown once, like `whsec_`). Revoke is an operator action; the next One call 401s. A cache of secrets is a later problem.

**`ownership.transferred` is not billing yet.** There is no SST registration, no tax invoice, no “who is on the receipt as merchant legal name” beyond tenant profile. `/me` role `owner` is enough for chrome.

**Pay’s focused host on this SHA** (`apps/lazuar-pay`, `Program.cs`) exposes `/health` and `/v1/health` only. `packages/pay-spec/main.tsp` has no webhook route. There is no table to store `event_id`. Building the receiver now is inventory without a consumer.

**08 §6.12 checkbox** “at least one webhook received and HMAC-verified” is a **Consumer-0 maturity** bar, not a **sign-in** bar. Move it to the money gate. Do not fail connection review because R5 never ran against Pay.

---

## 6. When `tenant.suspended` becomes mandatory

**Rule:** before Pay will take **live** money for that tenant (hosted checkout, pay link, off-session charge, “mark paid” if that exists). Not before `GET /me`. Not after the first captured payment.

### 6.1 What suspend means on One

- Staff or platform admin: `POST /tenants/{id}/suspend`. Idempotent if already suspended (returns the tenant, **does not** enqueue a second `tenant.suspended`).
- Membership gate Default mode: members get **403** `"Tenant is suspended."` on mutating tenant routes.
- `GET /tenants/{id}` and `/me` still show the row with `status: "suspended"` (AllowSuspended / non-deleted filter).
- Webhook create is 403; webhook list still works.
- `GET /events` as a member **403s** (Default mode).
- Reactivate: `POST /tenants/{id}/reactivate`; only from `suspended`; already-active is idempotent with **no** second event.

Pay does **not** call suspend as merchant self-serve default (`02-one-integration.md`). Platform / Pay-admin policy might. The event still arrives if anyone suspends.

### 6.2 What Pay must do with it (money gate)

On `tenant.suspended` (push), or on `GET /tenants/{id}` / `GET /me` showing `suspended` (pull / request path):

| Action | Required? |
|--------|-----------|
| Refuse **new** checkout sessions / pay links / off-session charges for that `org_id` | **Yes** — this is `NP-ONE-018` |
| Refuse merchant admin that would mint a charge (VIEWER already cannot; owner/admin also cannot charge a suspended shop) | **Yes** |
| Leave **already captured** journal + `RCPT-` + subscription row as they are | **Yes** — “money in Pay is still true if the webhook is late” |
| Revoke buyer access to a file they already paid for | **Product choice, default no.** Suspension is a **merchant** kill-switch, not a chargeback. Do not invent a One-side buyer lock. |
| Wait for the webhook before believing `/me.status` | **No.** Request-path check is the belt. Webhook is the suspenders for jobs that do not have a user JWT (billing tick, hosted checkout start with only Pay’s `lzr_sk_`). |

### 6.3 Why push is mandatory at the money gate (not merely “nice”)

Hosted checkout is the **buyer** plane. The payer has no One token. Pay’s server must decide “is this shop allowed to take money?” without Ada in the loop.

Options:

1. **HMAC push** sets `tenants.charges_enabled = false` (or a Pay-side status column). Checkout reads Pay’s row. Fast, works if One is briefly down after the event was delivered.
2. **Live `GET /tenants/{id}` with Pay’s `lzr_sk_` (`tenant:read`)** on every checkout start. Correct, extra RTT, 403/suspended handling, fails closed if One is down (or fails open — **do not fail open**).
3. **Pull `GET /events`** on a timer. **Fails after suspend** (member 403). Useless as the suspend detector. Fine as catch-up **while active**.

The money gate should do **(1) + (2)**: webhook updates the local flag; checkout **also** fail-closes if One says suspended when One is reachable. If One is down and the local flag is still `active`, that is the lag 011 accepted for **staff**, not for **charges**. Prefer fail-closed on checkout if the One status check errors. Document that. Do not fail-open “so dogfood works when One is rebooting.”

`tenant.reactivated` is the inverse. Subscribe to both or the shop stays dead.

### 6.4 Late webhook vs late charge

Timeline that 011 already chose:

1. Ada pays (buyer) → PSP → Pay webhook (Plane B) → journal + receipt **commit**.
2. Staff suspends the tenant on One.
3. One’s `tenant.suspended` POST to Pay is delayed (Pay down, SSRF, retries).

Law: step 1 stays booked. Step 3, when it arrives, stops **step 1 from happening again**. It does not reverse step 1. Refunds are a Pay money operation (`NP-MON-005`), not an One event.

If step 2 happens **before** step 1, and Pay has no receiver and does not check status, Pay might still charge. That is the hole this event exists to close. Hence: **mandatory before live charges**, even if connection skipped it.

### 6.5 `NP-ONE-018` scope

Checklist text: “Stop charges (and staff access) on `tenant.suspended`.” Staff access without a webhook is already mostly true: One 403s membership mutations; Pay should `authz/check` and `/me` on admin routes. The **new** work at the money gate is **charges**. Do not invent a Pay-side session kill-list for staff as a substitute for `/me`.

---

## 7. INCLUDE vs DEFER

Two moments: **first connection** (this 012 slice) and **money gate** (next, still before S1 live charges). “V1 later” means after dogfood money is boring.

| Event / job | First connection | Money gate (before live charges) | After, still Pay | Notes |
|-------------|------------------|----------------------------------|------------------|-------|
| Receiver URL + HMAC verify + store `whsec_…` | **DEFER** | **INCLUDE** | — | No Point A without a URL. |
| `webhook.test` round-trip | **DEFER** | **INCLUDE** (prove HMAC) | — | R5. 08 §6.12 checkbox moves here. |
| `tenant.suspended` | **DEFER** | **INCLUDE** (mandatory) | — | Kill new charges. Pair with request-path `GET /tenants/{id}` or `/me.status`. |
| `tenant.reactivated` | **DEFER** | **INCLUDE** (with suspend) | — | Otherwise the flag never clears. |
| Pull `GET /events` catch-up | **DEFER** | **INCLUDE if push is blocked** (laptop SSRF); else optional backfill | Keep as repair tool | Not a substitute for suspend after the 403. |
| Tail Zitadel | **REFUSE** | **REFUSE** | **REFUSE** | `NP-XX-007` adjacent. |
| `member.accepted` / `removed` / `left` / `role_changed` | **DEFER** | **DEFER** (optional cache) | Optional | `/me` + `authz` are SoT. JIT/domain joins never emit `member.accepted`. |
| `member.invited` / `invite.*` | **DEFER** | **DEFER** | Optional chrome | One is invite SoT. Copy-link stays One (`NP-ONE-011`). |
| `ownership.transferred` | **DEFER** | **DEFER** | When legal/billing owner is printed | `/me` role is enough for chrome. |
| `tenant.created` | **DEFER** | **DEFER** if Pay creates the workspace or lazy-upserts on first use | INCLUDE if other apps create tenants Pay must bill | `NP-ONE-019` is S0 but Dogfood **—**. Prefer sync/lazy provision over webhook-required. |
| `tenant.deleted` | **DEFER** | **DEFER** until wipe is a Pay story | INCLUDE before relying on owner wipe | Honest leftovers; do not expect Zitadel org gone (`NP-XX` / One issue 045). |
| `api_key.revoked` | **DEFER** | **DEFER** until Pay caches keys | INCLUDE with a key cache | Env-held server key: 401 is the detector. |
| `api_key.created` | **DEFER** | **DEFER** | Optional | |
| `oidc_app.*` | **DEFER** | **DEFER** | Optional | Login failure is the detector for Pay’s own SPA. |
| Grant buyer entitlement in One on any of the above | **REFUSE** | **REFUSE** | **REFUSE** | §11. |
| Plane B PSP webhooks | **DEFER** (no money) | Not this paper | S1 `NP-GW-*` | Different HMAC. |

**First connection INCLUDE (not this paper’s job, but the contrast):** SPA register, `:5175`, access_token Bearer, `GET /me`, `POST /tenants` or pick membership, copy-link invite, scoped `lzr_sk_`, `authz/check`. That is papers 01–08 of this family and `NP-ONE-001`…`016`, `020`–`022`.

---

## 8. Two planes of people (do not grant buyers in One)

`02-one-integration.md` table:

| Plane | System | Who |
|-------|--------|-----|
| Merchant staff | One humans + membership | Ada, invited MEMBER/VIEWER |
| Buyer / payer | Pay checkout profile | Person who pays on the hosted page |

Cardholders never become Zitadel users because they bought an ebook (`NP-XX-013`, `NP-CHK-007`, `NP-FUL-002`).

One catalog events are **staff and tenant lifecycle**. There is no `buyer.created`, no `entitlement.granted`, no FGA type `payment` / `document` (`NP-XX-015`). Pay must not:

- `POST /tenants/{id}/members/invite` for a cardholder.
- Treat `member.accepted` as “this human paid.”
- Call One `authz/write` to grant a tuple on a receipt.
- Wait for One to “hear” `payment.succeeded` before writing the ledger. Fulfillment is the **PSP** webhook handler in Pay, same process, same DB transaction (`NP-FUL-001`, Linux-shaped room). One down must not mean “paid but no receipt.”

If a second Lazuar **app** later needs entitlement, that is HTTP from Pay (`NP-LAT-003`), not an in-process catalog and not an One membership.

Staff VIEWER cannot charge: that is Pay enforcing One role + `authz` (`NP-ONE-021`), not a webhook.

---

## 9. Receiver design (for the next slice — not now)

When the money gate starts, Pay is the receiver. One is the sender.

### 9.1 URL

Focused Pay listens on **8081** so the **old** modular monolith can keep **8080** (`apps/lazuar-pay/README.md`, `Taskfile.yml` `pay:dev`, `launchSettings.json`, `packages/pay-spec` `@server("http://localhost:8081")`).

Suggested route (sketch only; TypeSpec grows when the slice starts):

```text
POST http://localhost:8081/v1/one/webhooks
```

Keep it under `/v1` (Bezos door, one public prefix). Do **not** put it under `/api/v1/webhooks/payments/{gateway}/…` (old Hub Plane B). Do **not** put it under `/one/*` inside Pay (Pay does not implement One). Name the One control-plane receiver so it cannot be confused with Stripe.

Staging/prod: `https://<pay-host>/v1/one/webhooks`. HTTPS required by One outside Development.

### 9.2 Secret shown once, stored in Pay

1. Owner/admin (or key with `webhooks:write`) `POST /api/v1/tenants/{tenantId}/webhooks` with `url` = Pay’s receiver and `events` = at least `["tenant.suspended","tenant.reactivated","webhook.test"]` (plus others when needed).
2. Response **201** includes `secret: "whsec_…"`. **Store immediately** in Pay’s secret store / env / per-tenant encrypted column. List/get will not show it again.
3. Pay’s process holds that value to verify `X-Lazuar-Signature`. One holds only ciphertext.
4. Rotate: `POST …/rotate-secret` → new secret once → Pay updates storage **before** or **atomically with** acknowledging rotate. There is **no** dual-key window. A racing in-flight delivery signed with the old key will 400 until retry with the new one (or dead). Rotate during a quiet period; accept one retry.

Do not log the secret. Do not put it in TypeSpec examples. Do not return it from Pay’s own admin GET.

Per-tenant vs one platform receiver: One’s webhooks are **per tenant**. Each merchant workspace Ada creates on One is one registration, one `whsec_`. Pay will have N secrets if N tenants dogfood. That is correct isolation. A single “platform webhook” would require One to grow a product it does not have. Do not invent one.

Alternatively, for **first-party only**, Pay-ops can register each dogfood tenant by hand from `lazuar-app` UI and paste `whsec_…` into Pay env (`PAY_ONE_WEBHOOK_SECRET_<tenantId>`). Ugly, honest for one shop. A Pay admin “paste endpoint + secret” screen is still later than connection.

### 9.3 Port collision on this laptop (honesty)

Documented One API port is **8080**. This laptop’s One app `.env` files remap One API to **8081** because **Aura owns 8080**. Focused Pay **also** claims **8081**. They cannot both bind 8081.

When the receiver slice runs locally:

- Move Pay (e.g. 8082) **or** run One API on 8080 when Aura is down **or** put Pay behind Caddy with a hostname.
- Point One’s webhook URL at whichever host:port Pay actually bound.
- Put that hostname on `Webhooks:UrlHostAllowlist` if it resolves private.

Do not “fix” this by opening One’s SSRF list. Do not assume `http://localhost:8081` works as the registered URL without the hatch.

### 9.4 Verify pipeline (sketch)

1. Read **raw** body bytes. Disable JSON model binding until after HMAC.
2. Missing timestamp/signature → **400**.
3. Skew > 300s → **400**.
4. HMAC fail → **400** (do not 401; there is no Bearer on this route). Do not 500 (One would retry a bad secret forever until auto-disable).
5. Parse JSON envelope. Require `id` / `type` / `tenant_id`.
6. Idempotency insert on `event_id` (§10). Duplicate → **200** with `{ received: true, duplicate: true }` (still 2xx so One stops).
7. Dispatch by `type`. Unknown type: **200** ignore (forward compatible) **or** 400 if you only subscribed to a closed subset and want noise. Prefer **200 ignore** so a catalog addition does not auto-disable the endpoint.
8. Handler work: local DB, **same request is OK** if it is a status flag. Do not call the PSP from this handler. Do not grant One membership from this handler.
9. Return 2xx in < HTTP timeout (One: 10s). If work can exceed that, persist-then-async **inside Pay** (one binary, function call / table), still 2xx after persist.

Unauthenticated public POST: rate-limit later. First money gate can rely on HMAC + skew. Do not skip HMAC “in Development.”

### 9.5 Registration ownership

Pay may register via:

- User JWT of the owner during “connect Pay” (browser session).
- Pay server `lzr_sk_` with `webhooks:write` (needs that scope on mint — first connection keys should stay `tenant:read` + `authz:check` until this slice).
- Human in `lazuar-app` Settings → Integrations → Webhooks (allowed; not `lazuar-admin`).

Do not send merchants to `:5173`.

---

## 10. Idempotency of `event_id`

### 10.1 What the id is

- Outbox primary key, UUID v7 (`Guid.CreateVersion7()` unless a publisher passed `eventId`).
- Copied to `webhook_deliveries.event_id`.
- Sent as `X-Lazuar-Event-Id` and envelope `id`.
- `GET /events` item `id` is the same string.

`X-Lazuar-Delivery-Id` is **per attempt**. Never use it as the idempotency key. A retry after a lost 2xx is a new delivery id, same event id.

`webhook.test` mints a fresh event id per test click. Tests are not replays of each other.

### 10.2 What One already guarantees

- Unique `(endpoint_id, event_id)` — one **row** per endpoint per event, not one HTTP.
- SKIP LOCKED — two API replicas do not claim the same in-progress row on Postgres. Docs still say: treat as at-least-once; crash-after-2xx retries.
- Suspend/reactivate/revoke idempotent **on One’s side** (no second outbox row if already in that state). Pay may still see **one** event and must still be idempotent on that id if One retries HTTP.

### 10.3 What Pay must guarantee (when the receiver exists)

A Pay table (name sketch: `one_webhook_events` or reuse a generic `processed_inbound_events` **with a `source = one` discriminant** — do not collide with Stripe event ids):

| Column | Role |
|--------|------|
| `event_id` | PK (text/uuid as received) |
| `tenant_id` | From header/body; **must match** Pay `org_id` |
| `type` | Catalog type |
| `received_at` | |
| `payload` | Optional jsonb for debug; redact if you ever log |

Insert-first (unique violation → duplicate, no-op handler). Then apply side effects. If side effects fail after insert, you need a status (`received` / `applied`) or the event is lost. Prefer **one Pay DB transaction**: insert processed row + update `charges_enabled` + audit row (`NP-AUD-001` spirit — same transaction as the write).

Pull + push together: both use the same `event_id`. A pull that already got the push is a no-op. A push that already got the pull is a no-op.

Do **not** key Plane B (Stripe) by this column without `source`. Stripe’s `evt_…` and One’s GUIDs are different spaces; a naive `event_id` PK shared across providers is how Hub’s “global webhook log” got forensics-wrong.

### 10.4 What not to idempot on

- Body hash (clock/format).
- `delivery_id`.
- `(type, tenant_id)` — two suspends cannot happen, but two `member.role_changed` can.
- Email / user_id — not unique in time.

---

## 11. Do not grant buyer entitlement in One

Restated because it will be tempting the first time someone sees `member.accepted` in a log next to a checkout.

- **Paid** is a Pay journal fact.
- **May use merchant ops** is an One membership fact.
- **May download the ebook** is a Pay subscription/session fact.

One webhooks move the second fact (staff) and tenant lifecycle. They do not move the first or the third.

If Pay “syncs customers into One” so “auth is unified,” you have recreated `Modules/One` and `NP-XX-013`. Fail the slice.

If Pay waits to write `RCPT-` until One ACKs an event, you have recreated the parked-event tax (`04-linux-shape.md`). Fail the slice.

If the webhook is late, the receipt already exists. Staff chrome catches up on next `/me`. That is the accepted split (`07-separate-vs-one-binary.md` advantage table, Pay ↔ One row).

---

## 12. Pull vs push vs Zitadel (operational matrix)

| Situation | What Pay does |
|-----------|----------------|
| Happy path, money gate | HMAC push to `:8081` `/v1/one/webhooks` |
| Laptop SSRF blocks loopback | Allowlist a hostname **or** skip push and **poll `GET /events` while tenant is active**; still `GET /tenants/{id}` on checkout for suspend |
| Tenant already suspended, push never arrived | `GET /me` or `GET /tenants/{id}` (AllowSuspended). **Do not** expect `GET /events` to work. |
| Pay was down for an hour | Dispatcher retries (up to 7 attempts / ~24h backoff). On boot, optional `GET /events` cursor catch-up for types Pay handles. Idempotent on `event_id`. |
| Endpoint auto-disabled after 15 failures | Pay ops: fix URL, `PATCH status=active`, `POST …/test`. Missed events: pull catch-up **if still active**; if suspended, status GET. |
| Secret lost | Rotate, store new `whsec_`. Old in-flight signatures fail until retries use the new secret. |
| Want “real time” from Zitadel Console | **No.** |

---

## 13. Tracker mapping (do not flip cells from this paper)

| ID | 011 text | 012 reading |
|----|----------|-------------|
| `NP-ONE-017` | HMAC webhooks: `member.*`, `tenant.created` / `suspended` / `reactivated`, `ownership.transferred`, `api_key.revoked`. Wave S0, Dogfood Y. | **Connection: leave `todo`.** Implement at money gate, and even then the **mandatory** types are `tenant.suspended` / `reactivated` (+ `webhook.test` to prove HMAC). Other names are optional caches. Pull if no push. Do not tail Zitadel. |
| `NP-ONE-018` | Stop charges (and staff access) on `tenant.suspended`. Dogfood Y. | **Connection: leave `todo`.** Mandatory **before live charges**. Staff access via `/me` + `authz` already. Money stays true if late. |
| `NP-ONE-019` | Provision Pay catalog/ledger rows on `tenant.created`. Dogfood — | Prefer **sync** on `POST /tenants` response and **lazy** upsert on first Pay use of an One tenant id. Webhook is an optimization for tenants born outside Pay. Not a connection blocker. |
| `NP-ONE-020` | Pay holds only OIDC `client_id`, `lzr_sk_`, One-webhook HMAC | HMAC line applies when the receiver exists. Connection holds `client_id` + `lzr_sk_` only. |
| `NP-GW-004` / `006` | PSP webhook verify + idempotent replay | **Different plane.** S1. Do not share code blindly; share the *idea* (raw body, 2xx, event id). |
| `NP-FUL-001` / `002` | Fulfill in the Pay handler; buyer access is Pay | Do not wait on One events. |
| `NP-XX-007` / `013` / `014` / `015` | No Zitadel in Pay; no buyer humans; no second org table; no empty FGA types | Unchanged. |

`12-first-slice-tracker.md` **step 6** (“Subscribe to `member.*` and `tenant.suspended`”) should be read, after this paper, as **the first step of the money side**, not the last step of connection. This analysis does not edit that file. When someone updates 011 trackers, move step 6 to sit with S1 keys/charges, or mark it blocked on “first charge” rather than on “first `/me`.”

---

## 14. Residuals and traps (so the next slice does not relearn them)

1. **`GET /events` 403s after suspend.** Pull is not the suspend detector. Status GET is.
2. **Auto-join / SSO JIT emit no catalog webhook.** Membership cache from `member.*` is incomplete. `/me` is SoT.
3. **Idempotent One verbs emit no second event.** If Pay misses the only `tenant.suspended`, catch up via status GET, not by waiting for another push.
4. **`webhook.test` is not on the outbox / EVT-07.** Delivery-only. Use it to prove HMAC, not to prove fan-out of domain events.
5. **Rotate has no dual-verify window.** Plan the cut.
6. **SSRF vs `:8081`.** Loopback blocked. Allowlist is One config. Strict prod ports = 443 only.
7. **Laptop 8081 collision** with One-API-on-8081-because-Aura. Pick a bind before registering the URL.
8. **`one-client` has no webhooks helper.** Raw fetch is fine. Workspace import is enough (`NP-XX-021`).
9. **`origin_request_id` is unused.** Do not promise tracing across One→Pay on that field.
10. **Secrets never in `data`.** Invite token, `lzr_sk_`, OIDC client_secret, `whsec_`. If a future payload grows them, refuse the handler.
11. **Issue 036 Done** — `tenant.deleted` can fan out because endpoints are kept. Still do not expect Zitadel org delete (issue 045).
12. **Issues 009 / 035 Done** — SKIP LOCKED + lease math. Still at-least-once HTTP. Still idempotent on `event_id`.
13. **Max 10 endpoints per tenant.** Pay should use **one** receiver URL per tenant, not one per event type.
14. **Auto-disable at 15.** A crashing Pay handler that returns 500 will silence One. Return 2xx after persist; fail internally.
15. **Do not copy old Hub** `POST /api/v1/one/workspaces/{id}/webhooks` or `whsec` from Pay’s own One module. That cathedral is the thing 011 left. New Pay is a **client** of sibling One.
16. **Do not add FGA types** to “check payment on webhook.” No written Pay `authz/check` for `payment` → `NP-XX-015`.
17. **GET `/me` can write.** A webhook handler that calls `/me` in a loop is a stampede. Do not.

---

## 15. Implementation sketch for later (not now)

This is a map for the money-gate slice. It is not an order to open a PR.

### 15.1 Pay HTTP

- TypeSpec: `POST /v1/one/webhooks` (no auth scheme; HMAC is the auth). Optional `GET /v1/one/webhooks/health` is unnecessary if `/v1/health` exists.
- Implementation: raw body endpoint. Verify per §9.4. 400 / 200 only for this route (avoid 5xx).

### 15.2 Pay storage

- Per-tenant: `one_webhook_endpoint_id`, `whsec` encrypted at rest **with Pay’s own key** (not One’s AES key), `charges_enabled` (bool, default true), `one_status` (copy of last seen).
- `one_events_processed (event_id PK, tenant_id, type, received_at)`.
- Do not create a membership replica table. If chrome needs roster, call One.

### 15.3 Register

- After connection can mint keys: add `webhooks:write` (and `events:read` if pull) to the Pay server key **or** register once with the owner JWT from a setup command.
- `POST /tenants/{id}/webhooks` `{ "url": "https://…/v1/one/webhooks", "events": ["tenant.suspended","tenant.reactivated","webhook.test"], "description": "lazuar-pay charges" }`.
- Persist `secret`. Persist endpoint id (for test/rotate/delete).
- `POST …/test`. Expect 202 then a POST to Pay. Logs: verified `webhook.test`.

### 15.4 Handlers

- `webhook.test` → no-op after idempotency insert.
- `tenant.suspended` → `charges_enabled = false` (same txn as processed row).
- `tenant.reactivated` → `charges_enabled = true`.
- Default → ignore 200.

Checkout / charge paths: if `!charges_enabled` **or** One `GET /tenants/{id}` says `suspended` → 403/409 honest “workspace cannot take payments.” If One is unreachable: fail closed on live charges.

### 15.5 Pull fallback

- Worker (same Pay binary, function call / hosted service — **not** a Notify process): cursor through `GET /events` while `one_status == active`. Apply same handlers. Stop on 403 suspended; then status GET.
- Do not run this worker on the connection slice.

### 15.6 Tests (when implemented)

- Vector: known `whsec_`, timestamp, body → expected `v1=` (port R5 / `WebhookSigningTests` vectors; or the sample `openssl dgst` snippet).
- Bad signature → 400, no row.
- Replay same `event_id` → 200, one side effect.
- `tenant.suspended` then checkout → refused; existing journal untouched.
- Do not call Zitadel in the test.

### 15.7 Explicitly not in that slice either

- `member.*` cache
- `ownership.transferred` billing owner
- `api_key.revoked` cache invalidation
- `tenant.created` as the only provision path
- Outbound Pay→merchant webhooks
- Custom FGA types
- npm publish of `one-client`

---

## 16. How this sits next to 08-dogfood §6.8

08 §6.8 table is still the right **catalog of interest** for Consumer-0. This paper does not delete it.

08 §6.11 step 6 (“Subscribe to `member.*` and `tenant.suspended`. **Stop.**”) mixed **connection** and **charge safety** in one numbered list because One’s paper was written before Pay’s 012 split. Read it as:

- Steps 1–5, 7: connection + refuse list. Do those.
- Step 6: **money gate**, not connection.
- §6.12 “at least one webhook received”: money gate proof, together with “at least one `authz/check` on a Pay route” (connection proof).

Prefer HMAC push. Pull EVT-07 if no push. Do not tail Zitadel. That sentence is unchanged and is the whole of §6.8’s operational advice.

---

## 17. Verdict

**Defer** the One HMAC receiver for the first connection slice. Whoami (`GET /me`) and authz (`authz/check`) are sufficient before money. Staff lag is acceptable. `member.*` is the wrong directory anyway (JIT/domain joins never fire it). `tenant.created` is redundant with `POST /tenants` + lazy upsert. `api_key.revoked` / `ownership.transferred` are not connection jobs.

**Include** the receiver **before live charges**: Pay URL on the focused host (designed **8081**, watch the Aura/One collision), HMAC `v1=` over `{timestamp}.{raw_body}`, `whsec_…` shown once and stored in Pay, idempotent on `event_id`, handle `tenant.suspended` / `tenant.reactivated` (and `webhook.test`). Pull `GET /events` only as catch-up while the tenant is still active; after suspend, use `GET /tenants/{id}` or `/me.status`. Never tail Zitadel. Never grant buyer entitlement in One. Money already booked stays booked if the push is late.

Do not implement that receiver from this file.
