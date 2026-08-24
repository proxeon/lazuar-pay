# P18 — Start persists provider, URL, session id

**Track:** Provider door · **Depends:** P17, S14, S15  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** Checkout row remembers the PSP session.

---

## P18.1

- [ ] After CreateHostedUrl succeeds: set `checkout.Provider`, `PspRedirectUrl`, `ProviderSessionId`
- [ ] SaveChanges before returning `{ redirect_url }`
- [ ] Second start while open: may reuse `PspRedirectUrl` if still valid, or mint a new session — pick **mint new** unless URL is non-empty and status open (document). Prefer mint new for Stripe (sessions expire); persist the latest id

## P18.2 Must not

- [ ] Do not persist URL without provider
- [ ] Do not write provider on `POST /v1/checkouts`

## P18.3 Exit

- [ ] Row updated on successful start
- [ ] Unblocked for B16 (Billplz join)
