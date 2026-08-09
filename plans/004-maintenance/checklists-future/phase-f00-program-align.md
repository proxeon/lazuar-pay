# F00 — Program align (which tracks run now)

**Goal:** Choose active tracks for this execution wave so work is phased, not one mega-diff.  
**Output:** Written answers below (or `plans/004-maintenance/future-wave-decisions.md`).

---

## F00.1 Track selection (yes/no this wave)

- [ ] **Keys (F01–F04):** yes / no / later — ________
- [ ] **Webhooks (F05–F06):** yes / no / later — ________
- [ ] **Cross-schema SQL (F07–F08):** yes / no / later — ________
- [ ] **TypeSpec Wave B (F09):** yes / no / later — ________
- [ ] **BuildingBlocks moves (F10–F13):** yes / no / later — ________
- [ ] **Polish (F14):** yes / no / later — ________
- [ ] **Module extract (F15):** yes **only if** product trigger — ________

## F00.2 Constraints

- [ ] Confirm dual-read key cutover still bound to calendar unless legacy row count is 0
- [ ] Confirm webhook A still needs product signing/payload decisions before F06
- [ ] Confirm F15 remains closed without reopen of `decisions.md` §00.4 / 00.5 / 16
- [ ] Confirm delivery style: many PRs on one branch **or** stacked PRs (pick one): ________

## F00.3 Exit

- [ ] Active tracks listed in order
- [ ] Team agrees F01 (or first selected phase) can start
