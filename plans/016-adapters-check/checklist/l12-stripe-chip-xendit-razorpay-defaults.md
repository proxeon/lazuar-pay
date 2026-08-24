# L12 — Stripe / CHIP / Xendit / Razorpay use the helper

**Track:** Checkout origin · **Depends:** L11  
**Analysis:** live defaults in four hosted classes  
**IDs:** —  
**Goal:** Phone dogfood does not redirect to the developer’s laptop **if** CheckoutBaseUrl is the deployed origin.

---

## L12.1

- [ ] `StripeHosted` SuccessUrl / CancelUrl
- [ ] `ChipHosted` success_redirect / failure_redirect / cancel_redirect
- [ ] `XenditHosted` success/failure URLs
- [ ] `RazorpayHosted` `callback_url`

## L12.2 Exit

- [ ] Each file calls L11 helper
