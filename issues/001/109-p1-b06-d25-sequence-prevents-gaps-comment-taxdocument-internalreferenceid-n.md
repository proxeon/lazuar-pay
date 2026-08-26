---
number: "109"
id: B06-D25
severity: P1
status: resolved
resolved_branch: fix/109-taxdocument-internal-unique
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 109 — B06-D25 — Sequence “prevents gaps” comment; `TaxDocument.InternalReferenceId` not unique

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/109-taxdocument-internal-unique`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D25 — Sequence “prevents gaps” comment; `TaxDocument.InternalReferenceId` not unique (P2 / P1)

Comment lie: `GenerateNextSequenceNumberCommandHandler.cs:26–27` (quoted §2.1).

Index:

```76:76:apps/lazuar-api/Modules/Lhdn/Infrastructure/LhdnDbContext.cs
            builder.HasIndex(x => new { x.OrganizationId, x.InternalReferenceId });
```

Not unique. Two PENDING rows can share `INV-2026-00001` if idempotency keys differ (cons Guid keys; credit-note race). GET by internal id is `FirstOrDefault` (`LhdnRepository.cs:37–41`). Cancel / poll / ops attach to **one** of them arbitrarily.

B2B handler’s key `b2b-inv:{org}:{invoiceNumber}` is the one honest idempotency string in this slice.

