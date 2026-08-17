---
number: "248"
id: B06-D28
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 248 — B06-D28 — Lhdn README still says signatures unimplemented / XAdES

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D28 — Lhdn README still says signatures unimplemented / XAdES (P2)

`Modules/Lhdn/README.md:32–36` still says XMLDSig/XAdES unimplemented and wait for `.p12`. Wave 2 added `JsonUblDocumentSigner`. Default path is unsigned 1.0, which the README’s “V1.0 stability” half gets right. The “signatures unimplemented” half is stale. Claiming XAdES in a demo is a lie. Claiming “we have no signer” is also a lie.

