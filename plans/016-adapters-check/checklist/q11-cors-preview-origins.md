# Q11 — CORS preview 4178 / 4179

**Track:** Hygiene · **Depends:** A00  
**Analysis:** [`../03-checkout-frontend.md`](../03-checkout-frontend.md) preview 4179 not allow-listed  
**IDs:** Q17 015  
**Goal:** Decide: allow preview origins **or** document `pnpm preview` will hang.

---

## Q11.1 Pick one

- [ ] **A:** Host CORS allow-list adds `http://localhost:4178` and `http://localhost:4179` (and 127.0.0.1 twins)
- [ ] **B:** README: preview ports are not CORS-allowed; use `pnpm dev` 5178/5179

## Q11.2 Must not

- [ ] Do not allow 3003/3004 (Q12)
- [ ] Do not allow `*` 

## Q11.3 Exit

- [ ] A with a CorsTests pair **or** B with README sentence
