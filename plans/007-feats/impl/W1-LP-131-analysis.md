# W1-LP-131 — Scoped API keys (close UX / enforcement gaps)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-131` (“Scoped API keys”). Tracker: *Scoped keys (least privilege)* — Lazuar **P**.  
**Not this ID:** new Commerce M2M routes (`LP-137`), last-used/rotate/IP allowlist (docs backlog, not required to close **P**), key cutover (already One-only).

**Invariant:** A machine key can only do what its closed-catalog scopes allow. Humans mint **explicit** scopes. Dead catalog entries are either wired or removed.

---

## 0. Scope lock

In scope:

- `PlatformApiScopes` allowlist + mint validation
- Ops create-key UX vs API omit-scopes default
- Authorization policies in `AuthAndCorsExtensions`
- The unused `payments.config:read` policy
- Proof that a payments-only key cannot hit LHDN write

Out of scope:

- `commerce.subscriptions:*` (add those on **LP-137**, then add to this catalog)
- Dual-key rotation window / `LastUsedAt`
- Changing prefix (`sk_` collision with Stripe is accepted, Prefix decision B)
- Letting `API_CLIENT` mint keys (`OrgAdmin` only — keep)

---

## 1. Verdict

Scopes **exist and are enforced** on the routes that attach policies. The cell is **P** because of three leftovers, not because keys are unscoped.

| Area | Status |
|------|--------|
| Closed catalog `PlatformApiScopes.AllKnownScopes` | **Y** — 6 strings |
| Mint reject unknown / empty-when-provided | **Y** |
| Ops UI catalog + presets + “select at least one” | **Y** |
| `API_CLIENT` not in `OrgAdmin` | **Y** — cannot mint/revoke |
| Payments checkout write/read policies on M2M routes | **Y** |
| LHDN write/read policies on document routes | **Y** |
| Webhook manage: custom `CanAccessWorkspaceWebhooksAsync` | **Y** — scope + tenant IDOR |
| Omit `scopes` on `POST /one/api-keys` | **Trap** — defaults to **LHDN** `documents:write read` |
| `payments.config:read` | **Dead** — policy + UI checkbox; **no route** uses `IntegrationPaymentsConfigRead` |
| Human `ADMIN` / `SUPER_ADMIN` bypass on Integration* policies | By design for console; `/me` is machine-only |

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/One/Domain/PlatformApiScopes.cs` | Constants, `NormalizeAndValidate`, `HasScope` |
| `Modules/One/Infrastructure/Endpoints/ApiCredentialEndpoints.cs` | OrgAdmin GET/POST/DELETE; `req.Scopes is null` → LHDN default |
| `src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` | Policy catalog |
| `src/Lazuar.Api/Middleware/ApiKeyAuthenticationMiddleware.cs` | Hash lookup; `scope` claims; `IsTestMode` from prefix |
| `Modules/Payments/Infrastructure/IntegrationEndpoints.cs` | Write / read / `/me` |
| `Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` | LHDN policies |
| `Modules/One/Infrastructure/Endpoints/WebhookEndpoints.cs` | Manual scope check (not the named policy — equivalent) |
| `apps/lazuar-ops/src/modules/workspace/pages/ApiKeysPage.tsx` | Closed catalog copy of the six scopes + LHDN / Payments presets |
| `apps/lazuar-docs/docs/integrations/api-keys.md` | Documents the omit→LHDN trap |
| `tests/Lazuar.ModuleTests/One/ApiKeyAuthenticationTests.cs` | Claims + policy matrix |
| `tests/Lazuar.ModuleTests/One/GenerateAndListApiCredentialsTests.cs` | Unknown scope 400 |

**Catalog (do not invent more here):**

- `lhdn.documents:write` / `lhdn.documents:read`
- `payments.checkouts:write` / `payments.checkouts:read`
- `payments.config:read`
- `webhooks.endpoints:manage`

Defaults: omit → `DefaultDocumentScopes` (LHDN). Aura provision uses `DefaultAuraIntegratorScopes` (payments + webhooks, **no** LHDN).

---

## 3. Gaps

### G1 — Omit-scopes LHDN default (UX + curl footgun)

Ops UI cannot omit. curl / SDK / Scalar “Try it” without `scopes` mints a **tax-document** key. A cashier integrator who follows a stale snippet gets LHDN power and no checkout write.

### G2 — `payments.config:read` is theater

UI offers it. Policy exists. **Zero** `RequireAuthorization("IntegrationPaymentsConfigRead")`. `/me` uses `IntegrationPaymentsMe` (any payments.* including this scope — so a config-only key **can** call `/me`, which already returns `has_active_gateway` / `gateway_names`). The checkbox is redundant unless we attach the policy to something exclusive.

### G3 — Docs / Scalar vs Ops catalog drift

VitePress tells humans to pass explicit scopes. API still “helps” with LHDN. Two truths.

**Not gaps for this ticket**

| Item | Owner |
|------|--------|
| No commerce scopes | LP-137 adds them to `AllKnownScopes` |
| Last-used / rotate | Backlog; do not block Y |
| ADMIN cookie can POST M2M checkout | Intentional console bypass |
| Stripe `sk_` prefix collision | Documented; probe `/me` |

---

## 4. Minimal changes

### 4.1 Must — kill the LHDN default for new mints

| File | Change |
|------|--------|
| `PlatformApiScopes.NormalizeAndValidate` | **`null` / omitted → reject** with the same message as empty: require at least one known scope. Remove `DefaultDocumentScopes` as implicit mint. Keep the constant for **legacy rows** and tests that seed old keys. |
| `ApiCredentialEndpoints` | Comment update; still pass `req.Scopes` through. |
| LHDN façade mint (if any still omits) | Pass `DefaultDocumentScopes` **explicitly** so LHDN UI “create key” stays one-click. |
| `ApiKeysPage.tsx` | Already requires a selection. Add a one-line hint: “API clients must send `scopes`; there is no default.” |
| `integrations/api-keys.md` | Delete “Omit scopes → LHDN”. |

Provision / Aura bootstrap already passes explicit scopes — no change.

### 4.2 Must — make `payments.config:read` honest

Pick **one** (do not do both):

**Option A (recommended, smaller):** Remove `payments.config:read` from `AllKnownScopes`, Ops catalog, and `IntegrationPaymentsConfigRead`. `/me` already exposes connection status to any payments.* key. Update tests.

**Option B:** Attach `IntegrationPaymentsConfigRead` to `GET /integrations/payments/me` **in addition to** the current any-payments check, **or** add `GET /integrations/payments/config` that returns only `{ has_active_gateway, gateway_names }` and keep `/me` as today.

Prefer **A** unless a partner already minted config-read-only keys (inventory: unlikely; Ops never defaulted it).

### 4.3 Should

- Ops key list: if `scopes` empty on a **legacy** row, badge “legacy unscoped / LHDN default” so revoke is obvious.
- Developers `/auth` page: same “scopes required” sentence.

### 4.4 Do not

- Add `commerce.*` here.
- Put `API_CLIENT` on `OrgAdmin`.
- Change webhook IDOR rules.

---

## 5. Tests

Extend `GenerateAndListApiCredentialsTests` + `ApiKeyAuthenticationTests`:

| Case | Expect |
|------|--------|
| POST `/one/api-keys` with `scopes: null` / omitted | **400** (after G1) |
| `scopes: []` | 400 (already) |
| `scopes: ["not.a.real:scope"]` | 400 (already) |
| Payments-only key → LHDN POST documents | 403 |
| LHDN-only key → POST `/integrations/payments/checkouts` | 403 |
| Payments write key → GET checkout | 200 (write implies read) — already |
| After Option A: mint with `payments.config:read` | 400 unknown scope |
| After Option B: config-read key → `/me` or `/config` | 200; cannot create checkout |

Do not weaken `API_CLIENT` vs `OrgAdmin` tests.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| External script omitted scopes and expected LHDN | LHDN UI sends explicit list; document the break (additive catalog rule in `docs/api-versioning.md` allows adding required-ness? **This is breaking** for omit-default). Announce in Developers + require explicit scopes — acceptable for Wave 1 before many external keys exist. |
| Partner has config-read-only key | Option B, or treat as unused |

---

## 7. Acceptance

1. Mint without `scopes` returns 400.  
2. Ops and provision still mint in one click (explicit arrays).  
3. `payments.config:read` is either gone or attached to a real route.  
4. Payments-only key cannot submit LHDN; LHDN-only key cannot create a cashier checkout.  
5. Webhook manage + tenant IDOR unchanged.  
6. Tests §5 pass.  
7. Tracker **P → Y**.

---

## 8. Implement order

1. NormalizeAndValidate reject omit + LHDN façade explicit default  
2. Option A or B for config-read  
3. Docs + tests  
