# U20 — No secrets in VITE_*

**Track:** Merchant UI · **Depends:** U11–U15  
**Analysis:** [00](../00-what-must-be-done.md) standing law  
**IDs:** NP-GW-001  
**Goal:** Merchant env stays `VITE_PAY_API_URL` + public OIDC client_id.

---

## U20.1

- [x] Grep `apps/lazuar-pay-merchant` for `sk_live`, `whsec_`, `CHIP`, PEM, `VITE_STRIPE_SECRET` — none as defaults
- [x] IsolationTests already ban Hub types
- [x] Secrets only in PUT body from user input

## U20.2 Exit

- [x] Grep clean
