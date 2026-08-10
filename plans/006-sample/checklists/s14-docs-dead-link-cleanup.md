# S14 — Dead link cleanup (`script/second-app-proof`)

**Track:** Docs IA · **Analysis:** `../06`, `../08`, `../10`  
**Depends on:** S00  
**Goal:** Stop advertising deleted harness; point to real paths.

---

## S14.1 Inventory

- [ ] Grep monorepo for `script/second-app-proof` and `second-app-proof.md`
- [ ] List hits: lazuar-docs, docs/payments-integration-quickstart.md, lazuar-developers, how-to-maintain, etc.

## S14.2 Replace policy

| If | Then |
|----|------|
| Sample not shipped yet | Point to **second-app-checklist** + engineer quickstart |
| Sample shipped (S50+) | Point to **run-sample-app** + `examples/hub-cashier-next` |
| Historical mention only | Mark “removed in 004; replaced by …” |

## S14.3 Edit each hit

- [ ] `apps/lazuar-docs/docs/integrations/payments-cashier.md`
- [ ] `apps/lazuar-docs/docs/integrations/second-app-checklist.md`
- [ ] `apps/lazuar-docs/docs/guide/how-to-maintain.md` (if present)
- [ ] `docs/payments-integration-quickstart.md`
- [ ] `apps/lazuar-developers/**` payments-cashier page if referenced
- [ ] Any other grep hits

## S14.4 Exit

- [ ] Grep returns zero **actionable** dead paths (or only historical notes)
- [ ] Docs / developers build or typecheck not broken
