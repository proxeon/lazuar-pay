# K17 — No card number fields

**Track:** Checkout UI · **Depends:** K12  
**Analysis:** wrap-rails hosted_link  
**IDs:** —  
**Goal:** Pay does not collect PAN. Stripe/CHIP hosted pages do.

---

## K17.1

- [x] No `input` autocomplete cc-number / cvc
- [x] No Stripe.js card element on 5179 in this program
- [x] Name + email only (plus Pay button)

## K17.2 Exit

- [x] Grep `autocomplete="cc` none
