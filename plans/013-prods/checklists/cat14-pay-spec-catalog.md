# CAT14 — TypeSpec products/prices (`packages/pay-spec` only)

**Track:** Catalog · **Depends:** CAT10  
**Analysis:** [01](../01-production-ready-bar.md) §3.5 host #11, [04](../04-merchant-frontend.md) §7  
**Goal:** Spec matches the host catalog door. Do not clone Hub commerce.

---

## CAT14.1 Add

- [x] Grow `packages/pay-spec` with product create + list (and price models matching CAT11)
- [x] Namespace `LazuarPay`; prefix `/v1`; server still `http://localhost:8081`
- [x] Field names snake_case, same as the host
- [x] Document 401 unauthenticated and 403 not-member / member-cannot-write as the host does

## CAT14.2 Must not add

- [x] No import of Hub `/public/commerce` or `/admin/commerce`
- [x] No `packages/api-spec` import
- [x] No LHDN, TIN, WhatsApp, Hub `AuthUser`
- [x] No One `POST /tenants` copied into pay-spec

## CAT14.3 Compile

- [x] `task pay:spec` succeeds
- [x] **Not** `task gen` / honesty-allowlist / NSwag into Hub `api-types-dotnet` / `@repo/api-types-ts`
- [x] Dist stays gitignored

## CAT14.4 Exit

- [x] Spec and host paths/fields match
- [x] Catalog spec may land same tip as CAT10/CAT11 if small
- [x] Unblocked for later `@repo/pay-types-ts` (do not invent it in this phase)
