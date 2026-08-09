# R30 — BuildingBlocks port hygiene

**Track:** BB · **Analysis:** `../03-bb-llm-move-to-ops.md`, `../04-bb-email-messaging-move.md`, `009` ownership  
**Goal:** Interfaces in Application; Infrastructure implements

---

## R30.1 Inventory

- [ ] Ports defined only under BB.Infrastructure that modules use
- [ ] Concrete BB services injected where interface would suffice

## R30.2 Moves (thin only)

- [ ] Move storage/token/vault interfaces to Application if misplaced
- [ ] Update DI
- [ ] Architecture tests green

## R30.3 Docs

- [ ] Touch `009-building-blocks-ownership.md` if needed

## R30.4 Exit

- [ ] No product logic moved in this PR (LLM/email later)
