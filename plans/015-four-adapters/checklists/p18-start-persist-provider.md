# P18 — Start persists provider, URL, session id

**Track:** Provider door · **Depends:** P17, S14, S15  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Checkout row remembers the PSP session.

---

## P18.1

- [x] After CreateHostedUrl succeeds: set `checkout.Provider`, `PspRedirectUrl`, `ProviderSessionId`
- [x] SaveChanges before returning `{ redirect_url }`
- [x] Second start while open: may reuse `PspRedirectUrl` if still valid, or mint a new session — pick **mint new** unless URL is non-empty and status open (document). Prefer mint new for Stripe (sessions expire); persist the latest id

## P18.2 Must not

- [x] Do not persist URL without provider
- [x] Do not write provider on `POST /v1/checkouts`

## P18.3 Exit

- [x] Row updated on successful start
- [x] Unblocked for B16 (Billplz join)
