---
number: "110"
id: B06-D26
severity: P1
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 110 — B06-D26 — Country / address / phone placeholders that INVALID a real submit

- **Severity:** P1
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D26 — Country / address / phone placeholders that INVALID a real submit (P1)

Empty buyer address becomes `NA` / `00000` / state `17` (`LhdnBuyerMapper.cs:35–42`). ViewModelMapper default state for missing address is **`14`** (WP KL) (`ViewModelMapper.cs:48`, `64`). Two different “we don’t know” states.

Phone is `+60000000000` when missing (`ViewModelMapper.cs:43`, `60`). InvoicePeriod Description is always `"Monthly"` (`StandardInvoice.xml:21`, `UblJsonDocumentBuilder.cs:27`) even for a one-time quote. Credit note original ID is `"NA"` (B06-D19).

None of these are proven INVALID in-repo. They are the bytes we would send on the first real sandbox run.

