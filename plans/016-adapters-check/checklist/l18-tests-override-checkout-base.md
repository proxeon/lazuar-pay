# L18 — PayApiFactory sets CheckoutBaseUrl

**Track:** Checkout origin · **Depends:** L10  
**Analysis:** factory already sets `Pay:PublicBaseUrl=https://pay.test.example`  
**IDs:** —  
**Goal:** Hermetic starts have a known success origin.

---

## L18.1

- [ ] Factory `Pay:CheckoutBaseUrl=http://pay-checkout.test.example` **or** keep `http://localhost:5179` explicitly
- [ ] CHIP/Billplz start tests may assert the success URL host if they read LastBody (S13 / L13)

## L18.2 Must not

- [ ] Do not set CheckoutBaseUrl to PublicBaseUrl in tests (would hide L13 mix-ups)

## L18.3 Exit

- [ ] Factory setting exists
