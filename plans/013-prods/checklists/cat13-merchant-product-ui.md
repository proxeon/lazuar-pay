# CAT13 — `:5178` create + list products

**Track:** Catalog · **Depends:** CAT12, M16  
**Analysis:** [01](../01-production-ready-bar.md) §3.5 merchant, [04](../04-merchant-frontend.md) Screen C  
**Goal:** Staff shell is a client of Pay `/v1` (NP-CAT-005, NP-API-004). Not ops modules.  
**011:** NP-CAT-005, NP-API-004

---

## CAT13.1 Page

- [x] Origin `apps/lazuar-pay-merchant` **5178** `strictPort`
- [x] Page can **create** (name) and **list** products for the active org
- [x] After M16: `GET /v1/whoami` with Bearer `access_token` (picker), then catalog calls with the same Bearer
- [x] `org_id` from whoami `tenants[].id` / path — header is not authz

## CAT13.2 Must not

- [x] No `lazuar-ops` `src/modules/**` copy (no Commerce accordion, no `/admin/commerce`)
- [x] No `@repo/api-types-ts` in merchant `package.json` (M23 still holds)
- [x] No password form; no `credentials: "include"`
- [x] No Hub TIN / WhatsApp / “Require Company Name & Tax ID” on the form

## CAT13.3 Honesty

- [x] `member` UI may hide create; API still 403 if they POST (M24)
- [x] Do not call Hub `:3003` or Pay as if it were ops

## CAT13.4 Exit

- [x] Human on `:5178` after One login can create + see a product
- [x] NP-CAT-005 / NP-API-004 may flip **only** when that job ran (not because the route exists)
- [x] Catalog UI track unblocked; keys/receipts are other tracks
