# M10 — CHIP PEM is a textarea

**Track:** Merchant · **Depends:** A00  
**Analysis:** [`../02-merchant-frontend.md`](../02-merchant-frontend.md) §6.3; U12 asked textarea  
**IDs:** U12  
**Goal:** A PEM cannot survive a single-line `<input>`.

---

## M10.1 Live today

- [ ] CHIP webhook secret uses the same `<input>` as `whsec_`

## M10.2 Change

- [ ] When `provider === 'chip'`, webhook secret control is `<textarea>` (rows enough for a PEM)
- [ ] Other rails stay single-line input
- [ ] Placeholder still “PEM from CHIP dashboard”

## M10.3 Must not

- [ ] Do not validate PEM in the SPA (host still 400s bad signatures)
- [ ] Do not put a sample PEM in the page

## M10.4 Exit

- [ ] Unblocked for M20
