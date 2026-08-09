# R00 — Wave align

**Goal:** Choose which tracks run this execution wave.  
**Analysis:** `../10-program-sequencing-and-risks.md`  
**Output:** Fill answers below or `plans/005-remaining/wave-decisions.md`

---

## R00.1 Track selection (yes / no / later)

- [x] Keys R01–R06: YES
- [x] SQL R10–R17: YES
- [x] TypeSpec R20–R25: YES
- [x] BuildingBlocks R30–R35: YES
- [x] Webhooks R40–R43: YES (R40 document product defaults from decisions.md end-state A)
- [x] Polish R50–R53: YES
- [x] Extract R60: default **no** unless product trigger: NO (skip)

## R00.2 Delivery

- [x] Branch strategy: long-lived `chore/remaining-005` (one phase ≈ one commit; push after each)
- [x] One phase ≈ one PR/commit (confirm)
- [x] Confirm dual-read keys not removed before R04 migrate complete
- [x] Confirm no second Lhdn webhook stack (decision B rejected)

## R00.3 Calendar / freezes still in force

- [x] Keys dual-read until 2026-11-30 unless row count 0 (early OK)
- [x] Revenue recognition stays parked
- [x] WhatsApp / multi-channel stays frozen
- [x] No new modules without R60 gate

## R00.4 Exit

- [x] Ordered start list written (see `../wave-decisions.md`)
- [x] Team unblocked to start first phase
