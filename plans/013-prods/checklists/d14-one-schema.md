# D14 — One schema

**Track:** Database · **Depends:** D10  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** `public` **or** a single `pay` schema. Not Hub module schemas.

---

## D14.1 Pick

- [ ] **`public`** (fewer moving parts) **or** one schema named **`pay`**
- [ ] One migration timeline on that schema
- [ ] Tables are snake_case in this database (`lazuar_pay`)

## D14.2 Grep (must be empty)

- [ ] In `apps/lazuar-pay/src`, grep `HasDefaultSchema` for  
      `commerce|billing|payments|lhdn|crm|one|ops|messaging|communications`  
      → **no matches**
- [ ] No `CREATE SCHEMA commerce` (or the other eight) in Pay SQL / migrations

## D14.3 Refuse

- [ ] No second schema “for billing” / “for payments”
- [ ] No `HasDefaultSchema("commerce")` because a table is named checkout
- [ ] Pay tables do not live in One’s `lazuar` or Hub’s `lazuar_mvp`

## D14.4 Exit

- [ ] D14.2 grep is empty
- [ ] Unblocked for D15 (D16 after D12)
