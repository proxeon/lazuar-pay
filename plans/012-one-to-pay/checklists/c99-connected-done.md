# C99 — Connected definition of done

**Track:** Program · **Depends:** Whoami C10–C19 and Authz C20–C24  
**Analysis:** [10](../10-dogfood-and-tests.md)  
**Goal:** Close *this* program honestly. Not S0, not S1.

---

## C99.1 Runtime

- [x] `GET /v1/whoami` on 8081 forwards Bearer to One `GET /me` and returns the locked projection
- [x] Missing/bad token → 401; One down → 503; never 200 empty user
- [x] `GET /v1/orgs/{orgId}/ready` uses path + `authz/check member` on `type=tenant`
- [x] `{allowed:false}` and One 403 → Pay 403
- [x] `/health` and `/v1/health` never call One
- [x] Listen still **8081**

## C99.2 Tests

- [x] `task pay:test` covers whoami 200/401/503 and authz allow/deny/header-hint without live One
- [x] IsolationTests still ban the cathedral

## C99.3 Contract

- [x] `task pay:spec` includes whoami (and ready if you added it to spec — if ready is host-only, say so in README)
- [x] Old `task gen` / honesty allowlist untouched

## C99.4 What is still not done (must remain explicit)

- [x] SPA / OIDC / copy-link invite (P10)
- [x] `lzr_sk_` in Pay env (P20)
- [x] One webhooks (P30)
- [x] Checkout / BYOK / RCPT (P50)
- [x] Ops/portal on 8081 (P60 — refused for this program)
- [x] NP-ONE-021 VIEWER (C24)
- [x] 011/12 steps 1–7 not all `done`

## C99.5 Tracker

- [x] Flip **only** the NP-ONE rows listed in C00.6, and only if the job is proven
- [x] Do not mark 011/12 step 2 complete just because whoami exists (step 2 includes `:5175` login UX)

## C99.6 Exit

- [x] PR description links this file and says **connected**, not “Pay identity shipped”
- [x] Parked P-files remain `todo` / not started
