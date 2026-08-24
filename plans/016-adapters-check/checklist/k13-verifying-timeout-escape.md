# K13 — Verifying poll escape after 15 ticks

**Track:** Checkout · **Depends:** A00  
**Analysis:** [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) P1-6; interval clears, UI stays Verifying  
**IDs:** K13 015  
**Goal:** Late webhook is not a hung cashier. Do not re-enable a second mint.

---

## K13.1 Live today

- [ ] `n >= 15` clears interval; heading stays “Verifying…”
- [ ] No refresh, no return to Pay

## K13.2 Change

- [ ] After cap: show “Not paid yet. The success URL is not paid.”
- [ ] Button: **Refresh status** → GET `/v1/pay/{token}` once (no start)
- [ ] Do **not** show Pay if `started` (K14)
- [ ] Optional: keep polling with backoff instead of a hard stop — still must not start

## K13.3 Must not

- [ ] Do not treat timeout as paid
- [ ] Do not `location.assign` the processor URL automatically on timeout

## K13.4 Exit

- [ ] Unblocked for K14, K16
