# Q13 — pay-spec is not Hub `gen`

**Track:** CI / isolation · **Depends:** — (always)  
**Analysis:** [10](../10-ci-observability-decommission.md) §2.2  
**Goal:** Keep `packages/pay-spec` off Hub honesty.

---

## Q13.1 Grep

- [ ] `Taskfile.yml` `gen` / `gen:*` sources stay `packages/api-spec/**/*.tsp` — **no** `pay-spec`
- [ ] `contracts:honesty` still Hub OpenAPI ↔ Minimal (`scripts/check-openapi-minimal-honesty.mjs`)
- [ ] `ci.yml` `contracts` dirty-check paths do **not** include `packages/pay-spec`

## Q13.2 Pay contract stays separate

- [ ] `task pay:spec` remains the Pay compile
- [ ] Optional later PR job for `pay:spec` must **not** merge with `contracts`

## Q13.3 Must not

- [ ] Add pay-spec to `honesty-allowlist.yaml`
- [ ] NSwag / Kiota Pay DTOs from Hub yaml
- [ ] `task gen` compiling pay-spec “so one pipeline”

## Q13.4 Exit

- [ ] Grep still clean
- [ ] Unblocked for Q14
