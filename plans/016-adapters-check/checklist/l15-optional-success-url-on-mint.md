# L15 — Optional success_url on checkout create

**Track:** Checkout origin · **Depends:** L14  
**Analysis:** host Create already accepts optional success/cancel; SPA never sends them  
**IDs:** —  
**Goal:** Hosted rails prefer checkout.SuccessUrl. SPA may send it so defaults are unused.

---

## L15.1

- [ ] Merchant mint **may** send `success_url: ${origin}/c/${token}?status=verifying` — **but** token is unknown until create returns
- [ ] Therefore: either (a) host fills SuccessUrl on create from CheckoutBaseUrl + public_token, or (b) SPA PATCHes after mint, or (c) hosted defaults (L11) are enough
- [ ] **Prefer (a):** `CheckoutEndpoints.Create` sets `SuccessUrl` / `CancelUrl` from L11 once `PublicToken` exists if client omitted them
- [ ] SPA need not send them for laptop dogfood

## L15.2 Must not

- [ ] Do not require success_url from the SPA this program
- [ ] Do not leave SuccessUrl null **and** hosted default laptop if CheckoutBaseUrl is set — L11 covers defaults at start time even if create leaves null

## L15.3 Exit

- [ ] Either create stamps URLs or start helper is the single default (L11). Pick one in the PR description. Do not do both with different origins.
