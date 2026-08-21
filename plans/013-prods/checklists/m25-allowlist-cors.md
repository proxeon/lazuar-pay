# M25 — Allowlist + CORS

**Track:** Merchant · **Depends:** M14  
**Analysis:** [04](../04-merchant-frontend.md), [08](../08-one-identity-production.md)  
**Goal:** Login allowlist includes `:5178/callback`. Do not add ops `:3003`.  
**011:** NP-ONE-004

---

## M25.1 Login allowlist

- [ ] Login `REDIRECT_ALLOWLIST` includes `http://localhost:5178/callback`
- [ ] Include `http://127.0.0.1:5178/callback` twin **if** that hostname is used
- [ ] Prefer documented URL `http://localhost:5178`

## M25.2 One CORS (if SPA calls One)

- [ ] One `App:CorsOrigins` includes `:5178` **if** the SPA calls One directly (M19)
- [ ] Localhost **and** `127.0.0.1` twins if both are used
- [ ] Pay CORS already allows 5178 — **do not add `:3003`**

## M25.3 Production note

- [ ] Document: empty One CORS / empty `REDIRECT_ALLOWLIST` **fails boot**
- [ ] Staging/prod origins are exact HTTPS; no localhost in those lists

## M25.4 Exit

- [ ] Finalize after password can return to `:5178/callback`
- [ ] Unblocked for M26
