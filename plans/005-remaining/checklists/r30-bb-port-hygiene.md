# R30 — BuildingBlocks port hygiene

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md`, `../04-bb-email-messaging-move.md`, `009` ownership  
**Goal:** Interfaces in Application; Infrastructure implements  
**Notes:** [`../r30-notes.md`](../r30-notes.md)

---

## R30.1 Inventory

- [x] Ports defined only under BB.Infrastructure that modules use — **`IJwtService`, `IR2StorageService`** (see notes §1)
- [x] Concrete BB services injected where interface would suffice — **none material** for these ports; DI already interface-based

## R30.2 Moves (thin only)

- [x] Move storage/token/vault interfaces to Application if misplaced — **moved `IJwtService` + `IR2StorageService`** (token/vault already Application)
- [x] Update DI — host registrations already interface→impl; usings cleaned where Infrastructure was only for moved ports
- [x] Architecture tests green — added `Shared_Technical_Ports_Must_Live_In_BuildingBlocks_Application`

## R30.3 Docs

- [x] Touch `009-building-blocks-ownership.md` if needed — R30 done on JWT/R2 rows + deferrals

## R30.4 Exit

- [x] No product logic moved in this PR (LLM/email later)
