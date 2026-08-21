# F23 — `pay-spec` payments + receipts

**Track:** Fulfillment · **Depends:** F19, F20  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**Goal:** TypeSpec models for payments list + receipt match the host. Not Hub.

---

## F23.1 Add

- [x] Models for payments list and receipt (title, `number`, amounts, disclaimer)
- [x] Ops on `LazuarPay`: `GET /v1/orgs/{orgId}/payments` and GET receipt
- [x] snake_case. Server still `http://localhost:8081`

## F23.2 Must not

- [x] No `packages/api-spec` import. No Hub `task gen`
- [x] No LHDN / Tax Invoice / VALID fields
- [x] No `InvoiceIssued` / `TaxInvoiceId`

## F23.3 Compile

- [x] `task pay:spec` succeeds
- [x] Dist stays gitignored
- [x] OpenAPI shows the new ops; existing whoami/health remain

## F23.4 Exit

- [x] Spec field names match host
- [x] Unblocked for B99 (this track)
