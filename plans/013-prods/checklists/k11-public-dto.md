# K11 — Buyer-safe public DTO

**Track:** Buyer page · **Depends:** K10  
**Analysis:** [05](../05-checkout-frontend.md) §4.5  
**Goal:** Bill fields only. snake_case. No org internals.

---

## K11.1 Return

- [ ] `amount`, `currency`, `status` (`open` / `paid` / `expired` as the host can produce)
- [ ] Merchant **display name** if you have it (else omit or a boring “Payment” — do not invent Hub branding GET)
- [ ] `payer_required` (or equivalent flags): whether name/email still needed before start
- [ ] Token / public id the URL already has is fine to echo

## K11.2 Must not return

- [ ] `org_id` / One tenant internals / One roles / whoami
- [ ] Gateway secrets, BYOK hints, PSP keys
- [ ] `success_url` / `cancel_url` if they would leak admin/merchant tokens or private paths
- [ ] Staff emails, `lzr_sk_`, webhook secrets

## K11.3 Shape

- [ ] JSON snake_case (Pay contract, not Hub camelCase)
- [ ] Subset of merchant GET — not the same DTO dumped public

## K11.4 Exit

- [ ] 200 body is buyer-safe
- [ ] Unblocked for K15
