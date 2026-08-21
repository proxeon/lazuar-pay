# M17 — Tenant list

**Track:** Merchant · **Depends:** M16  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Render whoami `tenants[]`. Empty is valid. No Pay org insert.  
**011:** NP-ONE-006 (already done on the host)

---

## M17.1 Render

- [x] Render `tenants[]` from Pay whoami
- [x] Show `id`, `name`, `slug`, `role`, `status`
- [x] Do not re-implement host whoami / One `/me` (NP-ONE-006 is C13)

## M17.2 Empty

- [x] Empty list: explain create workspace (M19)
- [x] Do **not** `INSERT` Pay `organizations` to make the picker non-empty
- [x] Empty membership is a first-run screen, not a crash

## M17.3 Must not

- [x] No Hub `GET /one/auth/me` fallback
- [x] No synthetic org row, mapping table, or Pay-side surrogate id

## M17.4 Exit

- [x] Ada with tenants sees them; Ada with none sees create/pick copy
- [x] Unblocked for M18
