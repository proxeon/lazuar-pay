# I15 — Public GET exposes started + redirect_url

**Track:** Idempotent start · **Depends:** I10  
**Analysis:** [`../03-checkout-frontend.md`](../03-checkout-frontend.md); K14  
**IDs:** —  
**Goal:** `:5179` can Continue without guessing. GET is public; the hosted URL is already a buyer secret-in-the-URL.

---

## I15.1 Live today

- [ ] GET `/v1/pay/{token}` returns token, amount, currency, status, payer_*, `email_required`
- [ ] No `started`, no `redirect_url`

## I15.2 Change

- [ ] Add `started: true` when `PspRedirectUrl` is non-whitespace (else false)
- [ ] Add `redirect_url` when started **and** status is `open` (omit or null when paid/expired)
- [ ] Never include API keys, webhook secrets, PEM, last4

## I15.3 Must not

- [ ] Do not add a provider picker field
- [ ] Do not return `ProviderSessionId` unless you need it for support later — default omit (plink/cs_ is enough leakage on the processor URL)

## I15.4 Exit

- [ ] `PublicPayTests` after one CHIP start: GET has `started: true` and the stub URL
- [ ] GET before start: `started: false`, no redirect
- [ ] Unblocked for K14
