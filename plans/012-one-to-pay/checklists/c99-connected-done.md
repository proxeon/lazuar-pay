# C99 — Connected definition of done

**Track:** Program · **Depends:** Whoami C10–C19 and Authz C20–C24  
**Analysis:** [10](../10-dogfood-and-tests.md)  
**Goal:** Close *this* program honestly. Not S0, not S1.

---

## C99.1 Runtime

- [ ] `GET /v1/whoami` on 8081 forwards Bearer to One `GET /me` and returns the locked projection
- [ ] Missing/bad token → 401; One down → 503; never 200 empty user
- [ ] `GET /v1/orgs/{orgId}/ready` uses path + `authz/check member` on `type=tenant`
- [ ] `{allowed:false}` and One 403 → Pay 403
- [ ] `/health` and `/v1/health` never call One
- [ ] Listen still **8081**

## C99.2 Tests

- [ ] `task pay:test` covers whoami 200/401/503 and authz allow/deny/header-hint without live One
- [ ] IsolationTests still ban the cathedral

## C99.3 Contract

- [ ] `task pay:spec` includes whoami (and ready if you added it to spec — if ready is host-only, say so in README)
- [ ] Old `task gen` / honesty allowlist untouched

## C99.4 What is still not done (must remain explicit)

- [ ] SPA / OIDC / copy-link invite (P10)
- [ ] `lzr_sk_` in Pay env (P20)
- [ ] One webhooks (P30)
- [ ] Checkout / BYOK / RCPT (P50)
- [ ] Ops/portal on 8081 (P60 — refused for this program)
- [ ] NP-ONE-021 VIEWER (C24)
- [ ] 011/12 steps 1–7 not all `done`

## C99.5 Tracker

- [ ] Flip **only** the NP-ONE rows listed in C00.6, and only if the job is proven
- [ ] Do not mark 011/12 step 2 complete just because whoami exists (step 2 includes `:5175` login UX)

## C99.6 Exit

- [ ] PR description links this file and says **connected**, not “Pay identity shipped”
- [ ] Parked P-files remain `todo` / not started
