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

## Evaluation (current tree, 2026-08-18)

### What the bug is
The Lhdn factory can route document types `03` (debit), `04` (refund note), and `11`–`14` (self-billed invoice/credit/debit/refund) to keyed UBL strategies. No production publisher or ops composer ever submits those types. Refunds hardcode type `02`. The Credit Notes page title still says “Credit & Debit Notes.” The Lhdn README still checkmarks debit, refund, and all four self-billed types as supported. `ViewModelMapper` really does the self-bill entity swap, so a future accidental publish would emit XML, but nothing in this tree files those types. Adjacent: a type `01` with a blank buyer TIN is treated as B2C consolidated (General Public), so a B2B integrator payload missing TIN becomes the wrong template.

### Still present?
**STILL BROKEN**

Factory routing is unchanged:

```19:42:apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs
        bool isB2c = string.IsNullOrWhiteSpace(request.Buyer_tin) || request.Buyer_tin == "EI00000000010";
        ...
            "01" when isB2c => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2CConsolidatedInvoice"),
            "01" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2BStandardInvoice"),
            "02" or "03" or "04" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
            "11" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledInvoice"),
            "12" or "13" or "14" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledCredit"),
```

Refund handler still hardcodes `_02` (`GatewayRefundCompletedIntegrationEventHandler.cs:113`). Grep of `Document_type._03` / `._04` / `._11` in `*.cs` is empty (no production or test publisher). Credit Notes title is still the overclaim:

```100:101:apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx
      title="Credit & Debit Notes" 
      description="Audit contra-revenue records, refunds, and e-Invoice cancellations."
```

Lhdn README still lists debit / refund / 11–14 as supported (`apps/lazuar-api/Modules/Lhdn/README.md:14–25`). Entity swap is real (`ViewModelMapper.cs:33–88`). `scripts/lhdn_sandbox/07_test_self_billed.sh` exists; `run_all.sh:7–12` still runs 00, 01, 02, 03, 06, 07 (includes 07, skips 04/05); no committed sandbox log.

### Related files
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs` — routes unused types and blank-TIN `01` → cons.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/Strategies/ViewModelMapper.cs` — self-bill entity swap with no publisher.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Services/Strategies/SelfBilledInvoiceStrategy.cs` / `SelfBilledCreditNoteStrategy.cs` / `CreditNoteStrategy.cs` — templates behind the factory.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` — only live CN type (`_02`).
- `apps/lazuar-api/Modules/Lhdn/README.md` — §2 overclaims 03/04/11–14.
- `apps/lazuar-ops/src/modules/invoicing/pages/CreditNotesPage.tsx` — “Credit & Debit Notes.”
- `scripts/lhdn_sandbox/07_test_self_billed.sh` + `run_all.sh` — sandbox script, no in-repo proof.
- `issues/105-p1-b06-d20-partial-refunds-skip-lhdn-entirely-commercial-cn-still-issued.md` / `issues/107-…` — live CN/cancel path, still type 02 only.

### Tests
- `GatewayRefundCompletedIntegrationEventHandlerTests` (e.g. the case at lines 68–72) asserts `Document_type == _02` only.
- `MyInvoisLoopTests` / `LhdnSingleCreditPathTests` / `LhdnRateLimitingTests` substitute `IDocumentStrategyFactory` and never construct types 03/04/11–14.
- No test would fail if the page title or README stayed overclaimed, or if 03/04/11–14 remained unpublished.
- First regression: (1) CreditNotesPage title does not contain “Debit”; (2) README §2 labels 03/04/11–14 as strategy-only / not product; (3) factory test that blank `Buyer_tin` on a B2B-shaped `_01` still selects consolidated (document the mixup or refuse it).

### Reproduction today
Open ops `/invoicing/credit-notes` and read the page title. Read Lhdn README §2. Refund a VALID B2B sale after 72h and inspect the submit payload: `Document_type` is `_02`. POST an integrator type `01` with empty `buyer_tin` and observe the factory pick `B2CConsolidatedInvoice`. There is no ops “issue debit note” or “self-bill” composer.

### Blast radius
Honesty / demo overclaim. Merchants and integrators will think debit notes and self-billed affiliate invoices ship. A blank-TIN type `01` can file the General Public template (legal-shape risk if an integrator hits submit). No live type-03/04/11–14 money path. Frequency: every Credit Notes visit and every Lhdn README read.

### Suggested fix
Rename the page to “Credit Notes.” Rewrite README §2 to “strategy exists; no product publisher.” Do not add an ops composer or affiliate self-bill flow in this ticket (that is a product epic, not a P2 copy fix). Optionally refuse type `01` with empty TIN instead of silently selecting consolidated. No TypeSpec regen. No Wave 5 / WhatsApp / Xero / homemade e-mandate.

### Evaluation notes
Still P2 (copy + unused strategy). Adjacent cons mixup is sharper than the title and could be filed separately if an integrator is live. 255’s honesty fence still applies to any “self-billed VALID” claim. Not fixed by 161–200. Do not sell 03/04/11–14 until a publisher + sandbox log exist.

