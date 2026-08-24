# A99 — Definition of done

**Track:** Program · **Depends:** Q16, M22, K18, G14, W24, J16, E13  
**Analysis:** [`../00-evaluation.md`](../00-evaluation.md) §10–§11  
**IDs:** NP-GW-006, NP-GW-003 (lived still open)  
**Goal:** Honest close of 016 harden. Not Hub dark. Not five lived loops. Not tax.

---

## A99.1 Lived sentence (not only unit tests)

- [ ] Merchant signs in on `:5178` via One
- [ ] Merchant sets **one** active provider; Billplz uses a **public https** `Pay:PublicBaseUrl` if that rail
- [ ] Buyer opens the minted origin `/c/{token}` with **no** One account
- [ ] Buyer Pay → processor; back/refresh does **not** mint a second processor session
- [ ] Success URL lands verifying; late webhook still becomes Paid without a second charge
- [ ] One `RCPT-`, balanced two-line journal; webhook retry no-ops
- [ ] `member` cannot paste keys; cannot `POST /v1/checkouts`

Hermetic `task pay:test` covers the money path with fake PSP HTTP. A99.1 stays open until a **human** loop on **one** rail. Five names in a `<select>` are not five lived loops.

## A99.2 Beat 1 money in code

- [ ] Start is idempotent for `open` + stored URL (I10–I12)
- [ ] One HMAC verifies `t=,v1=` over `{unix}.{body}` (W11–W16)
- [ ] Paused org does not mint `RCPT-` and does not consume paid event id (W21–W24)
- [ ] Razorpay captured without notes does not silent-pay (J12, J16)
- [ ] Billplz webhook does not hardcode MYR (D15)
- [ ] Stripe missing currency does not skip (D16)
- [ ] Process `whsec_` is Testing-only (E10)
- [ ] Git wrap key is Testing-only (E13)
- [ ] Hosted defaults use `Pay:CheckoutBaseUrl` (L10–L13)

## A99.3 Tests that 015 over-claimed

- [ ] G14 start twice, one FakePsp send
- [ ] W23 HMAC vector; W24 pause
- [ ] G12/G13 fulfill throw **or** G10 skip comment if no seam — not a silent skip
- [ ] S10–S18 strengthen done **before** F clones
- [ ] Billplz / Xendit / Razorpay: empty body, bad sig, not-paid ignore, paid replay (fb/fx/fr)
- [ ] Placeholder email 400 on four rails
- [ ] `Billplz_localhost_callback_start_is_400_without_psp_http` exists (not the lying name alone)

## A99.4 SPA honesty

- [ ] CHIP PEM textarea (M10)
- [ ] Billplz environment hydrates (M11)
- [ ] Pay link uses `VITE_CHECKOUT_ORIGIN` (M18)
- [ ] Buyer 400/503 show `detail` (K11–K12)
- [ ] Placeholder blocked (K10)
- [ ] Verifying timeout has an escape (K13)
- [ ] Started checkout Continue, not a second mint (K14)

## A99.5 Cathedral still banned

- [ ] IsolationTests still fail MediatR / Modules. / factory types / `Razorpay.Api`
- [ ] Extra tokens from S17: `ChipWebhookRegistrar`, `PublicDnsFallback`, Connect, LHDN
- [ ] Parked files still parked

## A99.6 Still not done (must remain explicit)

- [ ] Do not claim Hub replaced
- [ ] Do not claim Bar B / Pay v1 complete
- [ ] Do not claim `NP-GW-003` from hermetic CHIP
- [ ] Do not claim InMemory proved one TX unless G12 ran on a real transaction

## A99.7 Exit

- [ ] PR / note says **016 harden five wraps**, not “adapters ported”
