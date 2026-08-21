# D27 — Receipts + sequences

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-DOC-001, NP-DOC-002. Official Receipt. Series **RCPT**. Never UUID. Never default **INV**.

---

## D27.1 Documents

- [ ] Table `documents` **or** `receipts` (pick one)
- [ ] Columns: `org_id`, `number` (nullable until allocated), title stored **or** implied **Official Receipt**
- [ ] Number is never a UUID. Missing at issue time is F14 `PENDING` — column may be null now

## D27.2 Sequences

- [ ] `document_sequences`: `(org_id, series, year_myt, last_n)`
- [ ] Series **`RCPT`**. Never **`INV`** as the default
- [ ] Year is Malaysia (`year_myt`). Do not use UTC calendar year as the series year without MYT

## D27.3 Refuse

- [ ] Do not title Tax Invoice
- [ ] Do not copy `lhdn.TaxDocuments` / UBL / VALID
- [ ] Do not default Hub `DocumentSeries.Invoice`

## D27.4 Exit

- [ ] Document table + `document_sequences` exist; default series is RCPT
- [ ] Unblocked for D28
