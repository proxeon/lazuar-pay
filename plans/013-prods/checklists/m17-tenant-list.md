# M17 — Tenant list

**Track:** Merchant · **Depends:** M16  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Render whoami `tenants[]`. Empty is valid. No Pay org insert.  
**011:** NP-ONE-006 (already done on the host)

---

## M17.1 Render

- [ ] Render `tenants[]` from Pay whoami
- [ ] Show `id`, `name`, `slug`, `role`, `status`
- [ ] Do not re-implement host whoami / One `/me` (NP-ONE-006 is C13)

## M17.2 Empty

- [ ] Empty list: explain create workspace (M19)
- [ ] Do **not** `INSERT` Pay `organizations` to make the picker non-empty
- [ ] Empty membership is a first-run screen, not a crash

## M17.3 Must not

- [ ] No Hub `GET /one/auth/me` fallback
- [ ] No synthetic org row, mapping table, or Pay-side surrogate id

## M17.4 Exit

- [ ] Ada with tenants sees them; Ada with none sees create/pick copy
- [ ] Unblocked for M18
