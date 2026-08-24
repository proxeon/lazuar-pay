# L10 — `Pay:CheckoutBaseUrl` config

**Track:** Checkout origin · **Depends:** A00  
**Analysis:** [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) P1-5  
**IDs:** —  
**Goal:** One host config for hosted success/cancel defaults. Not Billplz callback.

---

## L10.1 Live today

- [ ] All five `*Hosted` default to `"http://localhost:5179/c/" + PublicToken + "?status=verifying"`
- [ ] `Pay:PublicBaseUrl` is Billplz **callback** only

## L10.2 Change

- [ ] Read `Pay:CheckoutBaseUrl` (trim, trim end `/`)
- [ ] If unset in Testing, factory may set `http://localhost:5179` (L18)
- [ ] If unset outside Testing, prefer throwing on first hosted create **or** fall back to localhost only in Testing — **do not** silently use laptop origin in Production
- [ ] Production: missing CheckoutBaseUrl → `InvalidOperationException` with that name (like wrap key)

## L10.3 Must not

- [ ] Do not reuse `Pay:PublicBaseUrl` for buyer redirects (tunnel ≠ checkout SPA)
- [ ] Do not put webhook paths on this origin

## L10.4 Exit

- [ ] Unblocked for L11
