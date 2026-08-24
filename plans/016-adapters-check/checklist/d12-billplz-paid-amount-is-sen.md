# D12 — Billplz `paid_amount` is sen

**Track:** Units · **Depends:** A00  
**Analysis:** Hub parse `paid_amount` / 100m for major; Pay stores minor and compares to `ToMinor(checkout)`  
**IDs:** —  
**Goal:** Missing `paid_amount` today becomes 0 → mismatch 400. That is fail-closed. Keep sen.

---

## D12.1

- [ ] Comment: form `paid_amount` is sen (minor). RM10.00 → `1000`
- [ ] Missing/unparseable → treat as mismatch (do not default checkout amount)

## D12.2 Exit

- [ ] Comment exists
- [ ] fb22 uses `paid_amount=999` vs checkout 10.00
