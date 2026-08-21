# D14 — One schema

**Track:** Database · **Depends:** D10  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** `public` **or** a single `pay` schema. Not Hub module schemas.

---

## D14.1 Pick

- [x] **`public`** (fewer moving parts) **or** one schema named **`pay`**
- [x] One migration timeline on that schema
- [x] Tables are snake_case in this database (`lazuar_pay`)

## D14.2 Grep (must be empty)

- [x] In `apps/lazuar-pay/src`, grep `HasDefaultSchema` for  
      `commerce|billing|payments|lhdn|crm|one|ops|messaging|communications`  
      → **no matches**
- [x] No `CREATE SCHEMA commerce` (or the other eight) in Pay SQL / migrations

## D14.3 Refuse

- [x] No second schema “for billing” / “for payments”
- [x] No `HasDefaultSchema("commerce")` because a table is named checkout
- [x] Pay tables do not live in One’s `lazuar` or Hub’s `lazuar_mvp`

## D14.4 Exit

- [x] D14.2 grep is empty
- [x] Unblocked for D15 (D16 after D12)
