# F20 — GET receipt by id

**Track:** Fulfillment · **Depends:** F14  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-DOC-005  
**Goal:** Member of the org can GET a receipt. Number is `RCPT-…` or `PENDING`, never a UUID.

---

## F20.1 Route

- [ ] `GET /v1/orgs/{orgId}/receipts/{receiptId}` (name illustrative) on 8081
- [ ] Member of that org. Other org → 403. Missing Bearer → 401
- [ ] JSON of the header is enough (PDF not required for Bar B)

## F20.2 Number

- [ ] `number` is `RCPT-` + MYT year + digits, **or** the string `PENDING`
- [ ] Not `Guid.ToString`. Not journal id. Not checkout id
- [ ] Title Official Receipt. No VALID

## F20.3 Must not

- [ ] Not Hub `GET /admin/billing/ledger/{id}/document`
- [ ] Not ops `:3003`. Not R2 required to GET
- [ ] Buyer public download is Bar C (`NP-BUY-005`)

## F20.4 Exit

- [ ] Member GET 200 with `RCPT-` or `PENDING`
- [ ] Unblocked for F21 and F23
