# M25 — Allowlist + CORS

**Track:** Merchant · **Depends:** M14  
**Analysis:** [04](../04-merchant-frontend.md), [08](../08-one-identity-production.md)  
**Goal:** Login allowlist includes `:5178/callback`. Do not add ops `:3003`.  
**011:** NP-ONE-004

---

## M25.1 Login allowlist

- [x] Login `REDIRECT_ALLOWLIST` includes `http://localhost:5178/callback`
- [x] Include `http://127.0.0.1:5178/callback` twin **if** that hostname is used
- [x] Prefer documented URL `http://localhost:5178`

## M25.2 One CORS (if SPA calls One)

- [x] One `App:CorsOrigins` includes `:5178` **if** the SPA calls One directly (M19)
- [x] Localhost **and** `127.0.0.1` twins if both are used
- [x] Pay CORS already allows 5178 — **do not add `:3003`**

## M25.3 Production note

- [x] Document: empty One CORS / empty `REDIRECT_ALLOWLIST` **fails boot**
- [x] Staging/prod origins are exact HTTPS; no localhost in those lists

## M25.4 Exit

- [x] Finalize after password can return to `:5178/callback`
- [x] Unblocked for M26
