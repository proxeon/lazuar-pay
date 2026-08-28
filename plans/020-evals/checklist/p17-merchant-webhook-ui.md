# P17 — Merchant Plane C chrome (parked)

**Track:** Parked  
**Analysis:** [`../08-headless-vs-spa.md`](../08-headless-vs-spa.md); W14 labels  
**Unpark when:** W14+W16 exist and staff keep pasting the URL wrong.

**Why parked:** Hatch is HTTP PUT + README. UI is how we mix three `whsec_` (Stripe vault, One inbound, Pay outbound). Copy must be designed, not a third “webhook secret” textarea.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` | Stripe/CHIP `webhook_secret` |
| One inbound PUT | `/v1/orgs/{orgId}/one-webhook` — no merchant chrome required |
| W14 | Host door |
| Hub `ApiKeysPage` | Refuse copy |

**Current (`6d730d15`):** No Plane C UI. Gateway page is PSP secrets.

---

## P17.1 When unparking

- [ ] Labels: “Pay will POST here; you verify” vs “Stripe signs; Pay verifies”
- [ ] Rotate button; never echo secret
- [ ] Do not mint One keys in this page
