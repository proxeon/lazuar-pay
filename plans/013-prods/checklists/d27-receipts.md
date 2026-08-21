# D27 — Receipts + sequences

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-DOC-001, NP-DOC-002. Official Receipt. Series **RCPT**. Never UUID. Never default **INV**.

---

## D27.1 Documents

- [x] Table `documents` **or** `receipts` (pick one)
- [x] Columns: `org_id`, `number` (nullable until allocated), title stored **or** implied **Official Receipt**
- [x] Number is never a UUID. Missing at issue time is F14 `PENDING` — column may be null now

## D27.2 Sequences

- [x] `document_sequences`: `(org_id, series, year_myt, last_n)`
- [x] Series **`RCPT`**. Never **`INV`** as the default
- [x] Year is Malaysia (`year_myt`). Do not use UTC calendar year as the series year without MYT

## D27.3 Refuse

- [x] Do not title Tax Invoice
- [x] Do not copy `lhdn.TaxDocuments` / UBL / VALID
- [x] Do not default Hub `DocumentSeries.Invoice`

## D27.4 Exit

- [x] Document table + `document_sequences` exist; default series is RCPT
- [x] Unblocked for D28
