---
number: "099"
id: B06-D13
severity: P1
status: resolved
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
resolved_branch: fix/099-stub-tin-lists
---

# 099 — B06-D13 — Stub TIN lists are not the same

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/099-stub-tin-lists`

`MyInvoisBuyerRules.IsStubTin` and `LhdnBuyerMapper` share C / IG / EI stub TINs. Integrator submit now blocks IG as well.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D13 — Stub TIN lists are not the same (P1)

**Status:** open.

`MyInvoisBuyerRules.IsStubTin` only refuses `C1234567890` (`MyInvoisBuyerRules.cs:16–17`). `LhdnBuyerMapper.StubTins` is `C1234567890`, `IG1234567890`, `EI00000000010` (`LhdnBuyerMapper.cs:11–16`).

B2B handler uses the mapper (skips IG / General Public). Integrator `POST /lhdn/documents` uses `EnsureBuyerTinValidAsync`, which only blocks `C1234567890` and then asks MyInvois. `LhdnSingleCreditPathTests` uses buyer TIN `IG1234567890` and stubs validation as **valid** (`LhdnSingleCreditPathTests.cs:52`, `173`). The credit test treats a mapper-stub TIN as a happy-path buyer.

`EI00000000010` + `NA` is correctly skipped at submit (`SubmitTaxDocumentCommand.cs:177–181`). The mapper also treats that TIN as a stub so a B2B handler cannot accidentally file General Public as a named buyer. Good. The IG mismatch is the hole.

