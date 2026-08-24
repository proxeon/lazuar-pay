# Q10 — TypeSpec start accepts `{name,email}`

**Track:** Hygiene · **Depends:** I15  
**Analysis:** `packages/pay-spec` start has **no body**; host and both SPAs send JSON  
**IDs:** —  
**Goal:** Spec matches the door. Do not generate clients until this exists.

---

## Q10.1

- [ ] `PublicPayApi.start` takes a body with optional `name`, `email`
- [ ] Public GET includes `email_required`, `started`, `redirect_url?`
- [ ] Regen openapi if that is the repo ritual; if not, edit `main.tsp` and note dist stale

## Q10.2 Must not

- [ ] Do not add payments/receipts/unversioned `/ready` unless cheap — out of money path
- [ ] Do not generate `@repo/api-types-ts` for the Vite apps

## Q10.3 Exit

- [ ] tsp describes the start body
