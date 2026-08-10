# S41 — Sample order domain + store

**Track:** Sample app · **Analysis:** `../03`  
**Depends on:** S31, S40  
**Goal:** Toy domain object independent of Hub schemas.

---

## S41.1 Model

- [ ] Type `Order` fields at least:
  - [ ] `id` (local)
  - [ ] `amount`, `currency`, `description`, `customerEmail`
  - [ ] `status`: `draft` | `checkout_open` | `paid` | `failed` | `cancelled` (or equivalent)
  - [ ] `hubCheckoutId?`, `checkoutUrl?`
  - [ ] `paidAt?`, `lastDeliveryId?` / `lastEventId?`
  - [ ] `metadata` map for Hub round-trip

## S41.2 Store

- [ ] `lib/orders-store.ts` (or similar)
- [ ] In-memory Map acceptable; prefer **file-backed JSON** under `.data/` (gitignored) so restarts keep demos
- [ ] Methods: create, get, list, update status, find by checkout_id
- [ ] No Postgres / Hub DB / Aura imports

## S41.3 UI read path

- [ ] List orders page or section
- [ ] Order detail shows status badges (pending vs paid)

## S41.4 Exit

- [ ] Can create and list draft orders without Hub
- [ ] Store path documented in README
