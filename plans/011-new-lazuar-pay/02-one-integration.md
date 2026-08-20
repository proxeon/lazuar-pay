# 02 — Integrate existing Lazuar One

**Date:** 20 August 2026  
**Sibling repo:** `/Users/akmalfirdaus/Code/lazuar/lazuar-one`  
**Pay is Consumer-0** (One `plans/017-evals/08-dogfood-then-serve.md` §6).  
**Honesty:** One staging proof is **NOT PASSED**. Packages `@lazuar/one-client` / `one-react` / `one-cli` are unpublished workspace packages. There is no public hosted SKU. First-party Pay may import the workspace client; do not wait on npm.

Pay is a **separate origin**. Users are One humans. Merchants are One tenants. Pay does **not** hold a Zitadel PAT or OpenFGA admin token. Merchants never use `lazuar-admin` (`:5173`).

---

## Identity of Pay (as a sibling app)

1. A **browser origin** registered as a tenant OIDC **SPA** (or `web`) via `POST /tenants/{id}/apps` — same kind of object as seeded `lazuar-app`, **not** a Console click.
2. A **backend** that calls One with the user’s **access_token** (`Authorization: Bearer`) or a `lzr_sk_` key.
3. **Not** a second Zitadel project the Pay team maintains in Console.

Local Pay env looks like `lazuar-app`: authority (`http://localhost:8085` in dev), `client_id`, One API `http://localhost:8080/api/v1`. Product login is **`:5175`**. Stock Login V2 **`:3005`** is break-glass only.

---

## What Pay must not implement (AuthN)

| Do | Do not |
|----|--------|
| OIDC code + PKCE against Zitadel authority with Pay’s `client_id` | Password form in Pay |
| Zitadel redirect to `:5175/login?authRequest=` | Treat `:5175` as Pay’s homepage |
| Send **access_token** as Bearer | Send `id_token` as Bearer |
| Register Pay redirects on the One app + login `REDIRECT_ALLOWLIST` | Add Pay origin only in Zitadel Console |
| | Ship merchants to `:3005` or `:5173` |

Do not parse Zitadel `urn:zitadel:iam:org:project:roles`. Role SoT for Pay chrome is `GET /me`, plus `authz/check`.

`GET /me` can **write** (domain auto-join, SSO JIT). Do not hammer it from a hot loop.

---

## Session and active workspace

| Call | Why |
|------|-----|
| `GET /me` | `user_id`, email, `tenants[]` (`id`, `slug`, `name`, `role`), `active_tenant_id`, `is_platform_admin` |
| Path `{tenantId}` + membership | Authorization SoT. `X-Lazuar-Tenant-Id` is a **hint only**. Never authorize by header alone. |

One tenant id **is** Pay’s `org_id` unless Pay writes a reason to map otherwise. Do not invent a second membership system “just for merchants” and also use One members.

---

## HTTP Pay should use

Routes are One `/api/v1` unless noted.

### Tenancy

| Method | Path | Pay use |
|--------|------|---------|
| `POST` | `/tenants` | Create workspace (caller becomes owner) |
| `GET` | `/tenants` | List memberships (or trust `/me`) |
| `GET` | `/tenants/{id}` | Profile |
| `PATCH` | `/tenants/{id}` | Name / metadata / logo |
| `POST` | `/tenants/{id}/suspend` | Staff or Pay-admin policy — not merchant self-serve default |
| `POST` | `/tenants/{id}/reactivate` | Same |
| `POST` | `/tenants/{id}/retry-provision` | Break-glass |
| `POST` | `/tenants/{id}/transfer-ownership` | Owner change |
| `POST` | `/tenants/{id}/leave` | User leaves |
| `POST` | `/tenants/{id}/delete` | Owner wipe — **honest leftovers** (Zitadel org may remain) |

Do **not** call `POST /platform/tenants` (staff directory).

### People

| Method | Path | Pay use |
|--------|------|---------|
| `GET` | `/tenants/{id}/members` | Roster |
| `POST` | `/tenants/{id}/members/invite` | Invite by email + role |
| `GET` | `/tenants/{id}/invites` | Pending |
| `DELETE` | `/tenants/{id}/invites/{inviteId}` | Revoke |
| `POST` | `/tenants/{id}/invites/{inviteId}/resend` | Resend (if One has it) |
| `POST` | `/tenants/{id}/members/accept-invite` | Token accept |
| `PATCH` | `/tenants/{id}/members/{userId}` | Role change |
| `DELETE` | `/tenants/{id}/members/{userId}` | Remove |
| `GET` | `/me/invites` | Inbox |

Pay UI may deep-link to `lazuar-app` `/invites/accept?tenant_id=&token=` **or** post the same API from Pay. Copy-link format must stay stable. Keep a **non-email** accept path.

Do not call Zitadel InviteUser. One membership is SoT.

### Machines and apps

| Method | Path | Pay use |
|--------|------|---------|
| `POST` | `/tenants/{id}/api-keys` | Worker / cron / Pay API → One |
| `GET` | `/tenants/{id}/api-keys` | List |
| `DELETE` | `/tenants/{id}/api-keys/{keyId}` | Revoke |
| `POST` | `/tenants/{id}/apps` | Register Pay SPA / web / client-credentials |
| `POST` | `/tenants/{id}/apps/{appId}/rotate-secret` | Rotate |
| `DELETE` | `/tenants/{id}/apps/{appId}` | Revoke |

Request **explicit** key scopes. Empty/`*` is a footgun. Prefer `tenant:read` plus the routes Pay actually hits.

### Authz

| Method | Path | Pay use |
|--------|------|---------|
| `POST` | `/tenants/{id}/authz/check` | Can this user `member` / `admin` / `owner` this tenant? |
| `POST` | `/tenants/{id}/authz/batch-check` | Permission chrome |
| `POST` | `/tenants/{id}/authz/list-objects` | `type=app` only; `type=tenant` is 0/1 |

Allow-list on One today is `{ tenant, app }`. Do **not** add FGA types `payment` / `document` until Pay actually checks them. Pay does not get `authz/write`.

---

## Events Pay should subscribe to

Prefer One **webhooks + HMAC**. Pull `GET /tenants/{id}/events` if Pay cannot take push. Do not tail Zitadel.

| Event | Pay use |
|-------|---------|
| `member.accepted` / `removed` / `left` / `role_changed` | Sync cache if any |
| `ownership.transferred` | Billing owner |
| `tenant.suspended` / `reactivated` | Stop charges / staff access |
| `tenant.created` | Provision Pay-side catalog/ledger rows |
| `api_key.revoked` | Drop cached secrets |

If the webhook is late, **money in Pay is still true**; staff access may lag. That is the cost of this split. Do not put buyer entitlement in One.

---

## Enterprise (only when a named merchant asks)

SSO connections, SCIM, audit stream, HRD — One’s APIs. Pay’s IT admin uses **lazuar-app** settings/IT or embeds those APIs. Pay does not build a second portal on `lazuar-admin`. SCIM Groups and IdP-initiated stay held until a deal maps them.

---

## Secrets

| Secret | Who holds it |
|--------|----------------|
| Zitadel masterkey / first-instance | One ops |
| Login-client PAT | `lazuar-login` only |
| `ZITADEL_PAT` Management | One seed / provisioner |
| OpenFGA store admin | One ops |
| Webhook AES / pepper | One API config |
| Platform admin email list | One `Platform:AdminEmails` |
| Pay OIDC `client_id` | Pay (public) |
| Pay `lzr_sk_` | Pay (once, secret) |
| Pay’s receiver HMAC for One webhooks | Pay (shown once) |

---

## Two planes (do not mix)

| Plane | System | Who |
|-------|--------|-----|
| Merchant staff | One humans + membership | Ada, invited MEMBER/VIEWER |
| Buyer / payer | Pay checkout profile | Person who pays on the hosted page |

Cardholders never become Zitadel users because they bought an ebook.
