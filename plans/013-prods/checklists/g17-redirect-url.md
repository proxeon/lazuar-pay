# G17 — Persist PSP URL; `start` returns it

**Track:** Rails · **Depends:** G16, K12  
**Analysis:** [06](../06-money-rails.md) §6.2 / §6.3  
**Goal:** Checkout row holds the hosted URL. Buyer `start` redirects there.

---

## G17.1 Persist

- [x] Store `checkout_url` (and `provider`, `provider_session_id`) on the D17 checkout row
- [x] Do **not** add Hub `IntegrationCheckoutSessions`
- [x] Stamp PSP metadata `org_id` + Pay `checkout_id` (CHIP/Stripe JSON; not a second SoT)

## G17.2 Public start

- [x] `POST /v1/pay/{token}/start` returns `{ redirect_url }` equal to the stored PSP URL
- [x] No One login on this route (buyer plane)
- [x] `open` only. **Refuse** `paid` / `expired` (409/400 — not 200 with a dead URL)
- [x] Unknown token → 404 (K13), not 401

## G17.3 Honesty

- [x] `success_url` landing is **not** paid (K19). Webhook is paid
- [x] Do not treat Hub `/success` as a template

## G17.4 Exit

- [x] Start returns the G16 URL for an open session
- [x] Unblocked for G18 (money comes back on Plane B)
