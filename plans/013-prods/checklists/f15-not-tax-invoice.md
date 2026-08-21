# F15 — Not a tax invoice

**Track:** Fulfillment · **Depends:** F14  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-DOC-003, NP-DOC-004, NP-XX-003  
**Goal:** Title is Official Receipt or payment receipt. No VALID. No LHDN submit.

---

## F15.1 Title

- [x] Stored / printed title: **Official Receipt** or **payment receipt**
- [x] Disclaimer: payment receipt, not a validated MyInvois tax invoice / not an LHDN e-invoice
- [x] Do not use H1 `Invoice` as a compromise

## F15.2 Grep lock (this path)

- [x] Grep `Tax Invoice` on the fulfillment / receipt path must be **absent**
- [x] Grep `VALID` on this path must be **absent** (also no `INVALID` / `SUBMITTED` badges)
- [x] No MyInvois UUID, QR, `B2C_RECEIPT`, `NEEDS_BUYER_TIN`

## F15.3 Must not

- [x] No LHDN submit (UBL, XAdES, consolidation, `SubmitTaxDocument`)
- [x] No `B2bTaxInvoiceRequestedIntegrationEvent`
- [x] No `InvoiceIssuedIntegrationEvent`

## F15.4 Exit

- [x] Grep lock holds on this path
- [x] Unblocked for F21 chrome (receipt JSON already honest)
