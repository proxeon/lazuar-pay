# D13 — Stripe `AmountTotal` is cents

**Track:** Units · **Depends:** A00  
**Analysis:** live already; test `amount_total:1000` vs amount 10  
**IDs:** H14  
**Goal:** Keep. G15 uses 999 vs 10.00.

---

## D13.1

- [ ] Comment if missing: `AmountTotal` is minor. Do not `ToMinor` it again
- [ ] Zero `AmountTotal` remains setup_or_zero ignore (H20), not mismatch

## D13.2 Exit

- [ ] G15 / fs11 use `amount_total:999`
