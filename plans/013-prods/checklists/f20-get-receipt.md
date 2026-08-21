# F20 — GET receipt by id

**Track:** Fulfillment · **Depends:** F14  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-DOC-005  
**Goal:** Member of the org can GET a receipt. Number is `RCPT-…` or `PENDING`, never a UUID.

---

## F20.1 Route

- [x] `GET /v1/orgs/{orgId}/receipts/{receiptId}` (name illustrative) on 8081
- [x] Member of that org. Other org → 403. Missing Bearer → 401
- [x] JSON of the header is enough (PDF not required for Bar B)

## F20.2 Number

- [x] `number` is `RCPT-` + MYT year + digits, **or** the string `PENDING`
- [x] Not `Guid.ToString`. Not journal id. Not checkout id
- [x] Title Official Receipt. No VALID

## F20.3 Must not

- [x] Not Hub `GET /admin/billing/ledger/{id}/document`
- [x] Not ops `:3003`. Not R2 required to GET
- [x] Buyer public download is Bar C (`NP-BUY-005`)

## F20.4 Exit

- [x] Member GET 200 with `RCPT-` or `PENDING`
- [x] Unblocked for F21 and F23
