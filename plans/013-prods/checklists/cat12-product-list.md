# CAT12 — `GET` product list for the org

**Track:** Catalog · **Depends:** CAT10  
**Analysis:** [01](../01-production-ready-bar.md) §3.1.2, [04](../04-merchant-frontend.md) Screen C  
**Goal:** Merchant list. `member` may **read**. Other org 403.  
**011:** NP-CAT-005

---

## CAT12.1 Route

- [x] `GET` list for the same org shape as CAT10 (`/v1/orgs/{orgId}/products` or `GET /v1/products` scoped by path/query `org_id`)
- [x] Path `{orgId}` is SoT; `X-Lazuar-Tenant-Id` cannot authorize a different org’s list
- [x] **Not** Hub `GET /admin/commerce/products`

## CAT12.2 Authz (read)

- [x] Require Bearer; missing → **401**
- [x] `MemberGate` is enough to **list** (`member` may read)
- [x] Other org / not a member → **403**
- [x] Unknown org the caller is not in → 403 (do not leak empty vs forbidden as an oracle if you can avoid it)

## CAT12.3 Body

- [x] 200 JSON array (or `{ "products": [...] }`) snake_case
- [x] Only rows for that `org_id`
- [x] Empty list is **200 `[]`**, not 404

## CAT12.4 Must not

- [x] Do not require `owner`/`admin` to list (that would break NP-ONE-022)
- [x] Do not return other tenants’ products

## CAT12.5 Exit

- [x] List after CAT10 create sees the row
- [x] Unblocked for CAT13, CAT15
