# Q17 — CORS still denies ops and portal

**Track:** Q · **Depends:** A00  
**Analysis:** 013 Q15 / P60  
**IDs:** NP-XX (old UIs)  
**Goal:** Adding four rails is not an excuse to allow `:3003`.

---

## Q17.1

- [ ] `CorsTests` still deny `http://localhost:3003` and `:3004`
- [ ] Allow-list remains 5178 + 5179 (+ 127.0.0.1 twins)
- [ ] Do not add admin `:5173`

## Q17.2 Exit

- [ ] Tests green
- [ ] Unblocked for A99
