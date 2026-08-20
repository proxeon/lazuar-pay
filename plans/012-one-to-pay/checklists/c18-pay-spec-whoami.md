# C18 — TypeSpec whoami (`packages/pay-spec` only)

**Track:** Whoami · **Depends:** C13 (runtime exists)  
**Analysis:** [04](../04-pay-spec-contract.md)  
**Goal:** Spec matches the host. Do not clone One’s spec.

---

## C18.1 Add

- [ ] `GET /v1/whoami` on namespace `LazuarPay`
- [ ] Models for the Pay projection in [`decisions.md`](./decisions.md)
- [ ] Document that unauthenticated → 401 (as the host does)
- [ ] Server remains `http://localhost:8081`

## C18.2 Must not add

- [ ] No `POST /tenants`, invites, `/one/auth/login`, `/one/auth/me`
- [ ] No LHDN, `/public/commerce`, Hub `AuthUser`
- [ ] No `packages/api-spec` import
- [ ] No `task gen` / honesty-allowlist / NSwag into `api-types-dotnet`

## C18.3 Compile

- [ ] `task pay:spec` succeeds
- [ ] OpenAPI shows `/v1/whoami` and still `/v1/health`
- [ ] Dist stays gitignored

## C18.4 Exit

- [ ] Spec and host field names match (snake_case)
- [ ] Unblocked for C19
