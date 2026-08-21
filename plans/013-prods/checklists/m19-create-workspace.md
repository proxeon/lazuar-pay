# M19 — Create workspace

**Track:** Merchant · **Depends:** M18  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Create = One `POST /tenants` (or deep-link app). Id is `org_id`.  
**011:** NP-ONE-009

---

## M19.1 Button

- [ ] Create-workspace button calls One `POST /api/v1/tenants` with Ada Bearer
- [ ] **Or** deep-link `lazuar-app` `:5174` (honest smaller path)
- [ ] Caller becomes **owner**. One tenant id **is** Pay `org_id` (same bytes)

## M19.2 After create

- [ ] Refresh Pay whoami (do not cache stale `tenants[]`)
- [ ] Then pick the new tenant (M18 path)

## M19.3 Must not

- [ ] No `INSERT` into Pay `organizations` / `users` / mapping tables
- [ ] No `POST /platform/tenants` (staff directory)
- [ ] No Pay BFF re-export of One tenant routes; no Hub `provision_apps`

## M19.4 Exit

- [ ] Empty Ada can obtain a tenant id without a Pay org table
- [ ] Unblocked for M20
