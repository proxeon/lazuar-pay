# K10 — Buyer does not pick a PSP

**Track:** Checkout UI · **Depends:** P17  
**Analysis:** [00](../00-what-must-be-done.md) §6.2  
**IDs:** NP-CHK-005  
**Goal:** `:5179` starts the org’s active rail only.

---

## K10.1

- [x] `App.tsx` has no provider `<select>`
- [x] Start POST has no `provider` override (unless you later add it for multi-rail orgs — **not this program**)
- [x] Buyer copy may say “you will continue on the processor’s page” without naming five logos

## K10.2 Exit

- [x] No picker
