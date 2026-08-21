# M15 — Login host `:5175`

**Track:** Merchant · **Depends:** M14  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Sign-in CTA is PKCE. Password UI is One login, not this homepage.  
**011:** NP-ONE-005

---

## M15.1 CTA

- [ ] Sign-in button calls `signinRedirect()` (oidc-client / react-oidc-context)
- [ ] Copy: no password form on this app
- [ ] User types password on **`:5175`**, not on the `:5178` homepage

## M15.2 Destinations

- [ ] Homepage remains `http://localhost:5178` after callback
- [ ] Never link merchants to `:5173` (One admin) or `:3005` (Login V2 / Hub admin)
- [ ] Do not `window.location = 'http://localhost:5175/'` as the product URL

## M15.3 Chrome

- [ ] `index.html` title remains **Lazuar Pay — merchant**
- [ ] Not “Lazuar Console”; not ops branding

## M15.4 Exit

- [ ] Unauthenticated visit → Sign in → Zitadel → `:5175` → back to `:5178`
- [ ] Unblocked for M16
