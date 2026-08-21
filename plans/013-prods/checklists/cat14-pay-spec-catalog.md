# CAT14 — TypeSpec products/prices (`packages/pay-spec` only)

**Track:** Catalog · **Depends:** CAT10  
**Analysis:** [01](../01-production-ready-bar.md) §3.5 host #11, [04](../04-merchant-frontend.md) §7  
**Goal:** Spec matches the host catalog door. Do not clone Hub commerce.

---

## CAT14.1 Add

- [ ] Grow `packages/pay-spec` with product create + list (and price models matching CAT11)
- [ ] Namespace `LazuarPay`; prefix `/v1`; server still `http://localhost:8081`
- [ ] Field names snake_case, same as the host
- [ ] Document 401 unauthenticated and 403 not-member / member-cannot-write as the host does

## CAT14.2 Must not add

- [ ] No import of Hub `/public/commerce` or `/admin/commerce`
- [ ] No `packages/api-spec` import
- [ ] No LHDN, TIN, WhatsApp, Hub `AuthUser`
- [ ] No One `POST /tenants` copied into pay-spec

## CAT14.3 Compile

- [ ] `task pay:spec` succeeds
- [ ] **Not** `task gen` / honesty-allowlist / NSwag into Hub `api-types-dotnet` / `@repo/api-types-ts`
- [ ] Dist stays gitignored

## CAT14.4 Exit

- [ ] Spec and host paths/fields match
- [ ] Catalog spec may land same tip as CAT10/CAT11 if small
- [ ] Unblocked for later `@repo/pay-types-ts` (do not invent it in this phase)
