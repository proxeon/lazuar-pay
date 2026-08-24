# L16 — `.env.example` names both bases

**Track:** Checkout origin · **Depends:** L10, L14  
**Analysis:** host `.env.example` PublicBaseUrl; merchant only Pay API  
**IDs:** —  
**Goal:** A human can tunnel Billplz callback without sending buyers to that tunnel.

---

## L16.1

- [ ] Host: `Pay__PublicBaseUrl` (https tunnel) commented
- [ ] Host: `Pay__CheckoutBaseUrl` (`http://localhost:5179` or deployed checkout origin)
- [ ] Merchant: `VITE_CHECKOUT_ORIGIN`
- [ ] Checkout Vite: still `VITE_PAY_API_URL` only

## L16.2 Exit

- [ ] Examples exist
- [ ] Unblocked for L17
