# D11 — Local Postgres on 5435

**Track:** Database · **Depends:** D10  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Pay money engine on host **5435**, database **`lazuar_pay`**. Not Hub. Not One.  
**No money tables yet.**

---

## D11.1 Publish

- [ ] Local Postgres published **`5435:5432`** (host 5435)
- [ ] Database name **`lazuar_pay`**
- [ ] User and password documented in `apps/lazuar-pay/.env.example`
- [ ] Image `postgres:16` / `postgres:16-alpine` is OK (repo already uses it)

## D11.2 Not these worlds

- [ ] **Not** host **5432** (One / Hub fight)
- [ ] **Not** database **`lazuar`** (One) or **`lazuar_mvp`** (Hub)
- [ ] Do **not** start Hub compose `db` / `task infra:up` for this
- [ ] Do **not** attach Pay to One’s cluster even if a laptop once remapped One to 5435
- [ ] Service / container must **not** be Hub `lazuar-db`

## D11.3 Init

- [ ] Do not copy Hub nine schemas (`commerce`, `billing`, `payments`, `lhdn`, `crm`, `one`, `ops`, `messaging`, `communications`)
- [ ] Do not `CREATE DATABASE lazuar` / `zitadel` / `openfga` in Pay’s init

## D11.4 Exit

- [ ] `psql` (or equivalent) to `localhost:5435` / `lazuar_pay` works with the `.env.example` user
- [ ] Unblocked for D12
