# W1-LP-137 — M2M subscription admin API (key-auth)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-137`. Tracker: *M2M subscription admin API* — Lazuar **N**.  
**Not this ID:** Payments cashier (`LP-136` already **Y**). Product CRUD via key (inventory: console-only — stay that way). Portal buyer cancel. Scoped-key catalog hygiene (`LP-131`) — **add** two commerce scopes here after LP-131’s allowlist rules.

**Invariant:** An integrator with a workspace `sk_` can **list / get / cancel** Hub Commerce subscriptions over HTTP the same way they create M2M checkouts — no ops cookie.

---

## 0. Scope lock

In scope (v1):

- Key-auth routes under `/integrations/commerce/subscriptions`
- Scopes `commerce.subscriptions:read` and `commerce.subscriptions:write`
- List (paginated), get by id, cancel (immediate — same as admin cancel today)
- Tenant isolation = key `TenantId`

Out of scope:

- Create product / price / coupon via key
- Enroll / offline record-payment / refund
- Pause/resume dunning
- Cancel-at-period-end (`LP-056`)
- Plan change (`LP-174`)
- Public magic-link portal as a substitute

---

## 1. Verdict

Tracker **N** is correct. Commerce admin is **OrgAdmin JWT only**.

| Surface | Auth | Subscription verbs |
|---------|------|--------------------|
| `GET/POST /admin/commerce/subscribers*` | `OrgAdmin` cookie | list, export, enroll, cancel, record-payment, dunning pause/resume |
| `GET/POST /public/commerce/{slug}/portal*` | magic token | see + cancel **own** |
| `/integrations/payments/checkouts` | `sk_` + payments scopes | **no** subscription object |
| `/integrations/commerce/*` | **absent** | — |

Developers hub already says: “Key-authenticated M2M product/subscription admin is not in v1.” This ticket is that v1.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Commerce/Infrastructure/Endpoints.cs` | `MapGroup("/admin/commerce").RequireAuthorization("OrgAdmin")` |
| `SubscriberEndpoints.cs` | Human verbs; `CancelAdminSubscriptionCommand` |
| `packages/api-spec/modules/commerce/admin-routes.tsp` | `@useAuth(BearerAuth)` but runtime is cookie OrgAdmin — machines 403 |
| `PublicPortalEndpoints.cs` | Buyer cancel |
| `AuthAndCorsExtensions.cs` | No commerce integration policies |
| `PlatformApiScopes.cs` | No `commerce.*` |
| `CommerceSubscriptionDto` | Already has the fields a GET should return (minus extra PII if we choose) |

Reuse `CancelAdminSubscriptionCommand` and `GetSubscribersAsync` / get-by-id if a query exists. Add get-by-id if list-only today.

---

## 3. Gaps

### G1 — No machine route group

A payments key cannot read Hub subscriptions. Aura SaaS that sells **Commerce** products (not only guest cashier) must scrape Ops or wait for webhooks.

### G2 — No scopes

Even after LP-131, there is nothing least-privilege to grant.

### G3 — List DTO is CRM-heavy

Admin list includes email/phone. That is acceptable for a **server** key (same as admin). Do not add a second DTO unless you want to drop phone.

**Not gaps**

- Outbound `subscription.*` webhooks (already the unlock bus).
- M2M **create checkout** (Payments, different product).

---

## 4. Minimal changes

### 4.1 Must

| File | Change |
|------|--------|
| `PlatformApiScopes.cs` | Add `commerce.subscriptions:read`, `commerce.subscriptions:write`. Write implies read in policy (same pattern as payments). Add to `AllKnownScopes`. |
| `AuthAndCorsExtensions.cs` | `IntegrationCommerceSubscriptionsRead` / `Write` — `API_CLIENT` + scope; **do not** let human ADMIN bypass if you want keys to be the only M2M path. **Do** allow ADMIN bypass so Ops curl still works (match payments). |
| New `Modules/Commerce/Infrastructure/IntegrationSubscriptionEndpoints.cs` | Group `/integrations/commerce` |
| TypeSpec | New file `modules/commerce/integration-routes.tsp` imported by `docs-commerce.tsp` (or `docs-payments`? **Commerce product docs**). |
| Ops `ApiKeysPage.tsx` + VitePress api-keys.md | New group “Commerce subscriptions” |

**Routes:**

```
GET  /api/v1/integrations/commerce/subscriptions?page=&limit=&status=
GET  /api/v1/integrations/commerce/subscriptions/{id}
POST /api/v1/integrations/commerce/subscriptions/{id}/cancel
```

Auth: Bearer `sk_`. `ctx.TenantId` from key. Empty tenant → 401 (copy Payments).

Cancel: same domain as `CancelAdminSubscriptionCommand` (immediate `CANCELED` + `SubscriptionCanceled` webhook). Return `{ status: "CANCELED" }` or 400 if already canceled.

List/get: map existing `CommerceSubscriptionDto` (or a slimmer `IntegrationSubscriptionDto` with id, status, product_id, customer_email, current_period_end, next_billing_date, is_reminder_only). Prefer **reuse** the DTO.

### 4.2 Should

- `GET …/me` stays Payments-only. Do not overload.  
- 404 on wrong-tenant id (do not leak existence across orgs — already org-filtered).

### 4.3 Do not

- `POST /subscribers` enroll.  
- Product write.  
- Return vault tokens.

---

## 5. Tests

New `IntegrationSubscriptionEndpointsTests` / handler tests:

| Case | Expect |
|------|--------|
| No key | 401 |
| Payments-only scopes | 403 |
| Read scope + other org’s id | 404 |
| Read scope + own sub | 200 body |
| Write scope cancel ACTIVE | `CANCELED` + event published (or outbox row) |
| Read scope cancel | 403 |
| Write implies GET | 200 |
| Already canceled | 400 stable message |

Reuse existing cancel domain tests; do not re-test dunning.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Integrators treat this as Stripe Billing | Docs: Hub Commerce rows, not Stripe Subscriptions |
| PII on list | Server key only; HTTPS |
| Cancel is immediate (no period-end) | Honest; LP-056 later |

Depends on **LP-131** allowlist update (adding scopes is non-breaking per versioning policy).

---

## 7. Acceptance

1. A key with only `commerce.subscriptions:read` can list/get its workspace subs and cannot cancel or create Payments checkouts.  
2. A key with write can cancel; webhook `subscription.canceled` still fires.  
3. Admin cookie routes unchanged.  
4. TypeSpec + Ops catalog include the two scopes.  
5. Tests §5 pass.  
6. Tracker **N → Y** (narrow admin: list/get/cancel only).

---

## 8. Implement order

1. Scopes + policies  
2. Endpoints wrapping existing commands/queries  
3. TypeSpec + Ops UI  
4. Tests + VitePress one paragraph under product-lines / events  
