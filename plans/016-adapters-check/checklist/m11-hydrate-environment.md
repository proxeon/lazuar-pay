# M11 — Hydrate Billplz environment from GET

**Track:** Merchant · **Depends:** A00  
**Analysis:** [`../02-merchant-frontend.md`](../02-merchant-frontend.md) §6.2 re-save live→test  
**IDs:** S12 / U13  
**Goal:** Reload must not show `test` when the row is `live`.

---

## M11.1 Live today

- [ ] `useState('test')`; `refresh()` never `setEnvironment(body.environment)`
- [ ] Re-save with rotated secrets overwrites live → sandbox

## M11.2 Change

- [ ] On GET gateway 200, if `body.environment` is `test` or `live`, `setEnvironment`
- [ ] Select still writer-only

## M11.3 Must not

- [ ] Do not infer live from hostname
- [ ] Do not send `environment` for non-billplz unless you already do (omit is fine)

## M11.4 Exit

- [ ] Unblocked for M21
