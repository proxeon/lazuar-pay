# CAT10 — `POST` product for `org_id`

**Track:** Catalog · **Depends:** D20, M24  
**Analysis:** [01](../01-production-ready-bar.md) §3.1.2, [04](../04-merchant-frontend.md) Screen C  
**Goal:** Named product on Pay `/v1`. MemberGate then owner/admin write (M24). Not Hub `/admin/commerce`.  
**011:** NP-CAT-001, NP-CAT-005, NP-API-004

---

## CAT10.1 Route

- [x] `POST /v1/orgs/{orgId}/products` **or** `POST /v1/products` with `org_id` — pick one; path `{orgId}` is SoT if nested
- [x] `{orgId}` / `org_id` is the One tenant id (same string as whoami `tenants[].id`)
- [x] **Not** Hub `POST /admin/commerce/products`
- [x] Persist on D20 `products` (not in-memory only if D20 exists)

## CAT10.2 Authz

- [x] Require `Authorization: Bearer`; missing/blank → **401** (do not call One)
- [x] `MemberGate` first (`authz/check member` on that org)
- [x] Then M24 write: only `owner` / `admin`
- [x] `member` → **403** (not 200, not a created row)
- [x] Other org / `{allowed:false}` → **403**

## CAT10.3 Body

- [x] `name` **required** (NP-CAT-001); empty → 400
- [x] Optional description allowed
- [x] JSON snake_case; 201 includes `id`, `org_id`, `name`

## CAT10.4 Must not

- [x] No LHDN B2B TIN, WhatsApp required, Hub fulfillment-target badges
- [x] No `check(member)` as the **write** gate (that is not NP-ONE-021 / M24)
- [x] No Pay `organizations` table

## CAT10.5 Exit

- [x] Happy path 201; 401 and 403 tests exist (or same tip as CAT15)
- [x] Do **not** flip NP-CAT-005 / NP-API-004 until CAT13 is a `:5178` client
- [x] Unblocked for CAT11, CAT12, CAT14
