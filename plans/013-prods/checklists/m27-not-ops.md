# M27 — Not ops

**Track:** Merchant · **Depends:** M17  
**Analysis:** [04](../04-merchant-frontend.md), [P60](./parked-p60-old-frontends.md)  
**Goal:** Do not retarget ops. Do not port the Hub catalog.

---

## M27.1 Must not port from ops

- [ ] LHDN / MyInvois / Tax Invoice / VALID
- [ ] Ops AI chat
- [ ] WhatsApp dunning / WhatsApp-required fields
- [ ] Hub CRM
- [ ] Quotes-as-tax / credit notes
- [ ] Hub credits / utility ledger / Hub pricing
- [ ] Password pages (forgot / reset / verify)
- [ ] `Sidebar` module catalog (Commerce / Invoicing / Developer cathedral)

## M27.2 Must not retarget

- [ ] Do **not** set ops `VITE_API_URL` to **8081**
- [ ] Ops stays on Hub `http://localhost:8080/api/v1` until kill
- [ ] Pay CORS still denies `:3003`

## M27.3 Steal judgment only

- [ ] New files in `lazuar-pay-merchant`; no import from `apps/lazuar-ops`
- [ ] Nav stays Products | Keys | Payments (+ receipt, workspace switcher)

## M27.4 Exit

- [ ] Merchant README / this phase notes the refuse list
- [ ] M track complete for Bar B chrome; money screens wait CAT / G / F
- [ ] Unblocked for B99 when other tracks catch up
