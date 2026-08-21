# Parked — Bar C (product v1 after dogfood is boring)

**Do not start until B99.**  
**Analysis:** [01](../01-production-ready-bar.md) §3.2, [07](../07-fulfillment-ledger-docs.md)

---

- [ ] Renew / billing job mints checkout or off-session charge (vaulted rails only)
- [ ] Full refund: gateway then reverse journal **once**
- [ ] SST exclusive on unit then × seats (steal `SstTaxMath` fully)
- [ ] Buyer magic-link portal on **`:5179`** (receipt + update-payment). Not merchant `:5178`
- [ ] Small payer profile (not Zitadel, not Hub CRM TIN theatre)
- [ ] Receipt email in-process (`mail_outbox`), not a Notify service
- [ ] Second rail only after the first is boring
- [ ] Quotes / PAST_DUE sequences are **soon**, not Bar C start
