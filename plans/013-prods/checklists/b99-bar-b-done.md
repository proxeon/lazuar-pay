# B99 — Bar B definition of done

**Track:** Program · **Depends:** M27, D29 (tables in use), CAT15, K22, G26, F23, O17, Q15  
**Analysis:** [01](../01-production-ready-bar.md) §6  
**Goal:** Close *this* program honestly. Not Hub dark. Not Bar C.

---

## B99.1 The sentence (must have been lived, not only unit-tested)

- [ ] Merchant signs in through One `:5175` on origin `:5178`
- [ ] Merchant pastes **the B00 rail** keys (encrypted)
- [ ] Merchant creates a MYR product + shareable pay link
- [ ] Buyer opens `:5179/c/{token}` **without** a One account
- [ ] Buyer pays on the PSP hosted page
- [ ] Pay shows one `RCPT-` and a **balanced** journal
- [ ] Webhook retry no-ops
- [ ] Invited One `member` can see the payment; `member` cannot paste keys
- [ ] Fail locks still true (password, second org table, Zitadel buyer, setup-as-paid, Tax Invoice/UUID, double-journal, merchant sent to admin)

## B99.2 Process

- [ ] Listen still **8081**; Hub still not bound as Pay
- [ ] Checkouts survive process restart (D17)
- [ ] `/health` never calls One; ready (if present) is Postgres only
- [ ] IsolationTests still ban cathedral + Hub types on Vite apps
- [ ] `task pay:test` green without Zitadel/CHIP network

## B99.3 What is still not done (must remain explicit)

- [ ] Hub cutover phases B–D ([parked-hub-cutover.md](./parked-hub-cutover.md))
- [ ] Bar C: renew, refund-once, magic-link portal, SST × seats, second rail ([parked-bar-c.md](./parked-bar-c.md))
- [ ] Ops/portal on 8081 still refused ([parked-p60-old-frontends.md](./parked-p60-old-frontends.md))
- [ ] One staging PASSED / Okta / SCIM / npm
- [ ] Homemade LHDN
- [ ] Go kernel rewrite

## B99.4 Tracker

- [ ] Flip **only** Bar B must IDs in 011/11 that were actually proven (see 01 §3.1)
- [ ] 011/12 steps 1–12 may move to `done` **only** if the sentence ran
- [ ] Do not mark “Pay v1 complete”

## B99.5 Exit

- [ ] PR / note says **Bar B**, not “Pay identity shipped”, not “Hub replaced”
- [ ] Parked files remain `todo` / not started
