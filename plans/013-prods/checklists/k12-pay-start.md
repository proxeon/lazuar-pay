# K12 — `POST /v1/pay/{token}/start`

**Track:** Buyer page · **Depends:** K10 (K12.2: G16 / G17)  
**Analysis:** [05](../05-checkout-frontend.md) §3.5 D, §6.5  
**Goal:** Validate token/state now. `{ redirect_url }` when a rail exists. Honest 409/503 until then.

Bar B order: K public resource **before** G. Do not block this route on G16. Do not return a fake Stripe URL.

---

## K12.1 Route and state (now)

- [ ] `POST /v1/pay/{token}/start` — **no** Bearer
- [ ] Optional body `name` / `email` (K18 persists; empty allowed until K18)
- [ ] Unknown token → same 404 as K13 (not 401/403)
- [ ] Status `paid` or `expired` → refuse (4xx). Do not mint hop-2
- [ ] Do **not** accept a new `amount` (ignore or 400)
- [ ] Do not ungated merchant `POST /v1/checkouts`

## K12.2 Without a rail (honest stub)

- [ ] If G16/G17 not done: **409** or **503** with boring detail e.g. `rail not configured`
- [ ] Never a made-up `checkout.stripe.com` / CHIP URL
- [ ] Open token still passes K12.1 validation when the stub fires

## K12.3 Redirect (complete with G17)

- [ ] After G16 hosted session + G17 store: 200 `{ "redirect_url": "…" }` snake_case
- [ ] URL is the PSP hosted page, not Pay whoami, not Hub `/public/commerce`
- [ ] Unblock note: this box stays open until G17; K15 may land on K12.1 + stub

## K12.4 Exit

- [ ] K12.1 true in this PR
- [ ] K12.2 **or** K12.3 true (stub or real — not a lie)
- [ ] Unblocked for K18; K20 may start
