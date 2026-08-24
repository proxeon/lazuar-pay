# K11 — Start 400 shows host detail

**Track:** Checkout · **Depends:** A00  
**Analysis:** every 400 → `callback base not public or email required`  
**IDs:** K16 family  
**Goal:** Placeholder email must not look like a Billplz tunnel bug.

---

## K11.1

- [ ] Parse JSON `detail` on 400
- [ ] `setError(detail ?? 'start 400')`
- [ ] Do not set `pay.status` to paid

## K11.2 Must not

- [ ] Do not keep the conflated sentence as the only 400 path
- [ ] Mapping a **short allow-list** of known details is OK (`email is required`, `callback base not public`)

## K11.3 Exit

- [ ] Unblocked for K18
