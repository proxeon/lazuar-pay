# D10 — CHIP `purchase.total` is cents (minor)

**Track:** Units · **Depends:** A00  
**Analysis:** Hub Collect; live `ChipWebhook` `(long)total`; test `total:1000` for amount 10  
**IDs:** C13  
**Goal:** A live major-unit payload would 400 forever (P0-D). Pin the constraint.

---

## D10.1

- [ ] Comment on `ChipWebhook` next to `total`: **CHIP sends sen/cents. RM10.00 → 1000. Do not divide by 100.**
- [ ] Create already sends `price = MoneyMath.ToMinor` (keep)
- [ ] Do not change the integer unless a lived JSON fixture (D20) proves otherwise — then **one** parser change, not the journal

## D10.2 Must not

- [ ] Do not `ToMinor` the total again (would be ×100 twice)

## D10.3 Exit

- [ ] Comment exists
- [ ] Existing `Chip_start_and_paid_webhook` still uses 1000
