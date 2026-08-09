# F10 — BuildingBlocks port hygiene (FW-3 start)

**Goal:** Interfaces live in Application abstractions; Infrastructure implements.  
**Depends on:** F00 BB track selected  
**Do not:** Move LLM/email yet (F11/F12)

---

## F10.1 Inventory

- [ ] List ports defined only under BuildingBlocks.Infrastructure that modules consume
- [ ] List inverted dependencies (modules referencing concrete BB services unnecessarily)

## F10.2 Moves

- [ ] Move thin ports (e.g. storage, token, vault interfaces) to Application if still misplaced
- [ ] Update usings / DI registrations
- [ ] Architecture tests green

## F10.3 Docs

- [ ] Update `apps/lazuar-api/docs/009-building-blocks-ownership.md` if ownership changes

## F10.4 Exit

- [ ] No new product logic added to BB
- [ ] Build green
