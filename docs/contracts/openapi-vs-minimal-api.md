# OpenAPI vs Minimal API — path honesty (Phase C.8)

**Purpose:** Keep TypeSpec/OpenAPI, generated clients, and ASP.NET Minimal API routes aligned. Document intentional gaps so frontends never invent phantom paths.

**Hosts:** All module endpoints hang under `/api/v1` (`Program.cs` `apiGroup`). openapi-fetch clients set `baseUrl` to `…/api/v1` and use **relative** paths (e.g. `/lhdn/documents/{id}/cancel`, never `/api/v1/lhdn/...`).

## Contract pipeline

```bash
task gen                 # TypeSpec → openapi.yaml → TS + C# clients (+ LHDN Kiota SDKs)
```

CI enforces cleanliness (`contracts` job in `.github/workflows/ci.yml`):

```text
task gen --force
git diff --exit-code -- \
  packages/api-types-ts/src \
  packages/api-types-dotnet/Generated \
  packages/api-types-dotnet/Lazuar.ApiContracts.cs \
  packages/lhdn-sdk-ts/src/generated \
  packages/lhdn-sdk-dotnet/src/Generated
```

`packages/api-spec/dist/` is gitignored (`dist/`); OpenAPI YAML is rebuilt every gen and is the intermediate for clients. If generation fails or drifts, fix TypeSpec or commit regenerated clients — do not hand-edit generated files except as a temporary hotfix with a follow-up `task gen`.

**Machine allowlist (R23 seed / R25 CI):** `packages/api-spec/honesty-allowlist.yaml` — `impl_only` routes may exist on the host without a product TypeSpec path. Prefer fixing TypeSpec over growing the allowlist for any route used by ops, portal, developers hub, or SDKs.

## Surface map (summary)

| Prefix | Module | OpenAPI | Notes |
|--------|--------|---------|--------|
| `/one/*` | One | Yes | Auth, workspaces, webhooks, platform API keys, storage presign |
| `/ops/*` | Ops | Yes | Chat list/stream/system-message/execute-action |
| `/admin/commerce/*` | Commerce | Yes | OrgAdmin console |
| `/public/commerce/*` | Commerce | Yes | Checkout, portal, coupons |
| `/admin/communications/*` | Communications | Yes | Email config, templates, broadcasts (send/preview/status) |
| `/public/communications/*` | Communications | Partial | Compliance public helpers |
| `/admin/billing/*` | Billing | Yes | Ledger, credits, profile, summary, admin document URL |
| `/public/billing/*` | Billing | Partial | Product: public profile + **draft** signed PDF. Final signed PDF is allowlisted (email HMAC; 302 → R2) |
| `/lhdn/*` | Lhdn | Yes | Documents, config, keys, webhooks |
| `/webhooks/payments/*` | Payments | Documented as integration | Gateway inbound webhooks |
| `/messaging/*` | Messaging | Minimal | Internal notify + delivery logs |
| `/api/v1/platform/*` | Host | Platform | Superadmin control plane |

Full route truth lives in module `Endpoints*.cs` files under `apps/lazuar-api/Modules/*/Infrastructure/`.

## Intentional internal / non-OpenAPI routes (allowlist)

These exist on the host but are **not** required in the public TypeSpec surface, or are intentionally machine/internal. Canonical machine form: `packages/api-spec/honesty-allowlist.yaml`.

| Route pattern | Why allowed out of public OpenAPI |
|---------------|-----------------------------------|
| `GET /public/billing/{tenantSlug}/documents/{ledgerEntryId}` | **R23:** HMAC email/human receipt link (`document_link`); **302** to R2 presign — not a typed product API. Draft `…/documents/draft/{sessionId}` and admin `GET /admin/billing/ledger/{id}/document` **are** in TypeSpec. |
| `POST /webhooks/payments/{gatewayType}/{tenantId}` | Gateway-signed inbound; not a product API |
| `POST /messaging/notify` | Authenticated internal fan-in (not third-party product surface) |
| `GET /messaging/delivery-logs` | Ops-adjacent messaging diagnostics |
| Host health / swagger static | Infrastructure |

If you add a **product** route used by lazuar-ops, lazuar-portal, lazuar-developers, or SDKs, it **must** land in TypeSpec before UI wiring.

## Intentional frontend “dark matter” (not deleted)

Per **ADR 023** (UI lobotomy) and **ADR 022** (Community/Vault removal):

| UI island | Status | Reactivation |
|-----------|--------|--------------|
| `lazuar-ops` invoicing module (quotes / tax invoices / credit notes) | Code present; **no routes** in `App.tsx` | Uncomment `[MVP-HIDE]` routes + sidebar (Phase D.3) |
| `lazuar-ops` `BillingProfilePage` | Unrouted | Same |
| `lazuar-ops` Ops chat (`OpsChatWorkspace`, stream client) | Unrouted; API + OpenAPI exist | Mount `/ops/chat` when productizing |
| `use-product-associations` | No-op stub | Community/Vault modules removed (ADR 022) |

Do **not** reintroduce `/admin/community/*` or `/admin/vault/*` without restoring those modules.

## Critical path rules (do not regress)

1. **No double prefix:** client path + `baseUrl` must yield a single `/api/v1/...` hop.
2. **No `as any` on path strings** for openapi-fetch calls — if TS rejects the path, the contract is wrong.
3. **CreateManualSubscriberDto** uses `product_id` (not legacy `plan_id` / `source`).
4. LHDN cancel: `POST /lhdn/documents/{internalId}/cancel` with `{ reason }`.
5. SSE (`POST /ops/chat/stream`) may use raw `fetch` for streaming, but the path and body must match OpenAPI.

## Residual gaps (honest)

- Superadmin `/api/v1/platform/*` coverage in TypeSpec is thin; expand when superadmin UI grows.
- Communications public routes may lag admin surface — verify before portal features depend on them.
- CSV export (`GET /admin/commerce/subscribers/export`) is binary; raw `fetch` is acceptable with a typed path comment.
- Hand-patched OpenAPI from earlier phases must be replaced by a clean `task gen` (C.8 CI gate).
- R25 will enforce OpenAPI ⊆ Minimal and Minimal ⊆ OpenAPI ∪ `honesty-allowlist.yaml`.
