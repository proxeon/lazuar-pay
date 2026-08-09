# R00 — Wave align

**Goal:** Choose which tracks run this execution wave.  
**Analysis:** `../10-program-sequencing-and-risks.md`  
**Output:** Fill answers below or `plans/005-remaining/wave-decisions.md`

---

## R00.1 Track selection (yes / no / later)

- [ ] Keys R01–R06: ________
- [ ] SQL R10–R17: ________
- [ ] TypeSpec R20–R25: ________
- [ ] BuildingBlocks R30–R35: ________
- [ ] Webhooks R40–R43: ________ (needs product for R40)
- [ ] Polish R50–R53: ________
- [ ] Extract R60: default **no** unless product trigger: ________

## R00.2 Delivery

- [ ] Branch strategy: long-lived `chore/remaining-005` **or** stacked PRs to main: ________
- [ ] One phase ≈ one PR (confirm)
- [ ] Confirm dual-read keys not removed before R04 migrate complete
- [ ] Confirm no second Lhdn webhook stack (decision B rejected)

## R00.3 Calendar / freezes still in force

- [ ] Keys dual-read until 2026-11-30 unless row count 0 (early OK)
- [ ] Revenue recognition stays parked
- [ ] WhatsApp / multi-channel stays frozen
- [ ] No new modules without R60 gate

## R00.4 Exit

- [ ] Ordered start list written (e.g. R01 + R10 + R20)
- [ ] Team unblocked to start first phase
