# F23 — `pay-spec` payments + receipts

**Track:** Fulfillment · **Depends:** F19, F20  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** TypeSpec models for payments list + receipt match the host. Not Hub.

---

## F23.1 Add

- [ ] Models for payments list and receipt (title, `number`, amounts, disclaimer)
- [ ] Ops on `LazuarPay`: `GET /v1/orgs/{orgId}/payments` and GET receipt
- [ ] snake_case. Server still `http://localhost:8081`

## F23.2 Must not

- [ ] No `packages/api-spec` import. No Hub `task gen`
- [ ] No LHDN / Tax Invoice / VALID fields
- [ ] No `InvoiceIssued` / `TaxInvoiceId`

## F23.3 Compile

- [ ] `task pay:spec` succeeds
- [ ] Dist stays gitignored
- [ ] OpenAPI shows the new ops; existing whoami/health remain

## F23.4 Exit

- [ ] Spec field names match host
- [ ] Unblocked for B99 (this track)
