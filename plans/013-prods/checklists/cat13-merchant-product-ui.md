# CAT13 — `:5178` create + list products

**Track:** Catalog · **Depends:** CAT12, M16  
**Analysis:** [01](../01-production-ready-bar.md) §3.5 merchant, [04](../04-merchant-frontend.md) Screen C  
**Goal:** Staff shell is a client of Pay `/v1` (NP-CAT-005, NP-API-004). Not ops modules.  
**011:** NP-CAT-005, NP-API-004

---

## CAT13.1 Page

- [ ] Origin `apps/lazuar-pay-merchant` **5178** `strictPort`
- [ ] Page can **create** (name) and **list** products for the active org
- [ ] After M16: `GET /v1/whoami` with Bearer `access_token` (picker), then catalog calls with the same Bearer
- [ ] `org_id` from whoami `tenants[].id` / path — header is not authz

## CAT13.2 Must not

- [ ] No `lazuar-ops` `src/modules/**` copy (no Commerce accordion, no `/admin/commerce`)
- [ ] No `@repo/api-types-ts` in merchant `package.json` (M23 still holds)
- [ ] No password form; no `credentials: "include"`
- [ ] No Hub TIN / WhatsApp / “Require Company Name & Tax ID” on the form

## CAT13.3 Honesty

- [ ] `member` UI may hide create; API still 403 if they POST (M24)
- [ ] Do not call Hub `:3003` or Pay as if it were ops

## CAT13.4 Exit

- [ ] Human on `:5178` after One login can create + see a product
- [ ] NP-CAT-005 / NP-API-004 may flip **only** when that job ran (not because the route exists)
- [ ] Catalog UI track unblocked; keys/receipts are other tracks
