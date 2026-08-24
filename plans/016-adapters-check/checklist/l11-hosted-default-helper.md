# L11 — One helper for success / cancel defaults

**Track:** Checkout origin · **Depends:** L10  
**Analysis:** five copy-pasted localhost strings  
**IDs:** —  
**Goal:** Five rails cannot drift.

---

## L11.1

- [ ] Helper e.g. `CheckoutUrls.Success(checkout)` / `Cancel(checkout)`
- [ ] If `checkout.SuccessUrl` set, use it
- [ ] Else `{CheckoutBaseUrl}/c/{PublicToken}?status=verifying`
- [ ] Cancel: `{CheckoutBaseUrl}/c/{PublicToken}` without query
- [ ] All `*Hosted` call the helper — no remaining `localhost:5179` in `Gateways/`

## L11.2 Must not

- [ ] Do not change Billplz `callback_url` to this helper (L13)

## L11.3 Exit

- [ ] Grep `localhost:5179` in `apps/lazuar-pay/src` is empty **or** only `.env.example` / comments
- [ ] Unblocked for L12, L13
