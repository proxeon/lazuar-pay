---
number: "246"
id: B06-D23
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 246 — B06-D23 — Types `03` / `04` / `11`–`14` are strategy-only; page title overclaims

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D23 — Types `03` / `04` / `11`–`14` are strategy-only; page title overclaims (P2)

**Status:** open.

```33:40:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs
            "02" or "03" or "04" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
            "11" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledInvoice"),
            "12" or "13" or "14" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledCredit"),
```

Refund handler hardcodes `_02`. No ops composer. Credit Notes page title is **“Credit & Debit Notes”** (`CreditNotesPage.tsx:100`). Lhdn README claims debit, refund, and all four self-billed types as supported (`README.md:14–25`). ViewModelMapper entity-swap for self-bill is real code (`ViewModelMapper.cs:33–85`) with **no production publisher**. `scripts/lhdn_sandbox/07_test_self_billed.sh` exists; `run_all.sh` runs it; no committed log.

Empty buyer TIN on an integrator type `01` selects **B2C consolidated** strategy (`DocumentStrategyFactory.cs:19`, `27–28`). A B2B payload with a blank TIN becomes a General Public template. That is a self-bill / cons mixup adjacent.

