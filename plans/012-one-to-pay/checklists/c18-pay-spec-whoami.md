# C18 — TypeSpec whoami (`packages/pay-spec` only)

**Track:** Whoami · **Depends:** C13 (runtime exists)  
**Analysis:** [04](../04-pay-spec-contract.md)  
**Goal:** Spec matches the host. Do not clone One’s spec.

---

## C18.1 Add

- [x] `GET /v1/whoami` on namespace `LazuarPay`
- [x] Models for the Pay projection in [`decisions.md`](./decisions.md)
- [x] Document that unauthenticated → 401 (as the host does)
- [x] Server remains `http://localhost:8081`

## C18.2 Must not add

- [x] No `POST /tenants`, invites, `/one/auth/login`, `/one/auth/me`
- [x] No LHDN, `/public/commerce`, Hub `AuthUser`
- [x] No `packages/api-spec` import
- [x] No `task gen` / honesty-allowlist / NSwag into `api-types-dotnet`

## C18.3 Compile

- [x] `task pay:spec` succeeds
- [x] OpenAPI shows `/v1/whoami` and still `/v1/health`
- [x] Dist stays gitignored

## C18.4 Exit

- [x] Spec and host field names match (snake_case)
- [x] Unblocked for C19
