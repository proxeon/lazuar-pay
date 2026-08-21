# M27 — Not ops

**Track:** Merchant · **Depends:** M17  
**Analysis:** [04](../04-merchant-frontend.md), [P60](./parked-p60-old-frontends.md)  
**Goal:** Do not retarget ops. Do not port the Hub catalog.

---

## M27.1 Must not port from ops

- [x] LHDN / MyInvois / Tax Invoice / VALID
- [x] Ops AI chat
- [x] WhatsApp dunning / WhatsApp-required fields
- [x] Hub CRM
- [x] Quotes-as-tax / credit notes
- [x] Hub credits / utility ledger / Hub pricing
- [x] Password pages (forgot / reset / verify)
- [x] `Sidebar` module catalog (Commerce / Invoicing / Developer cathedral)

## M27.2 Must not retarget

- [x] Do **not** set ops `VITE_API_URL` to **8081**
- [x] Ops stays on Hub `http://localhost:8080/api/v1` until kill
- [x] Pay CORS still denies `:3003`

## M27.3 Steal judgment only

- [x] New files in `lazuar-pay-merchant`; no import from `apps/lazuar-ops`
- [x] Nav stays Products | Keys | Payments (+ receipt, workspace switcher)

## M27.4 Exit

- [x] Merchant README / this phase notes the refuse list
- [x] M track complete for Bar B chrome; money screens wait CAT / G / F
- [x] Unblocked for B99 when other tracks catch up
