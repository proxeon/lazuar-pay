---
number: "253"
id: B06-D34
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 253 — B06-D34 — Stationery empty TIN is omitted, not “TIN not on file”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D34 — Stationery empty TIN is omitted, not “TIN not on file” (P2)

Factory fallback seller name is workspace name, then `"Merchant"` (`InvoiceDocumentFactory.cs:30`). Empty TIN is omitted (`BaseInvoiceDocument.cs:50–51`). W2-LP-107-done.md’s “TIN not on file” string is not in the factory. `InvoiceDocumentFactoryTests` locks “not Lazuar Merchant.” That part of the done-file is still overstated. Not a customer-facing lie today.

Legal profile Card 2 never auto-provisions. Submit without config throws “LHDN Tenant Configuration is missing.” (`SubmitTaxDocumentCommand.cs:99–103`). Seed genesis row is a hardcoded org + sandbox-looking TIN (`LhdnDbContext.cs:47–61`). Irrelevant unless that GUID is a live tenant.

## Evaluation (current tree, 2026-08-18)

### What the bug is
W2-LP-107-done.md claimed a missing billing profile prints workspace name + the string “TIN not on file.” The factory never emits that string. Empty seller TIN is omitted (no `TIN: N/A`). Fallback seller name is workspace name, then `"Merchant"`, never “Lazuar Merchant.” W4-LP-100-done.md later documented the omit-empty-TIN behavior as intentional. The live customer PDF is not lying; the Wave 2 done-file is. Adjacent: Lhdn tenant config is not auto-provisioned — submit without a row throws — and `LhdnDbContext` still seeds a genesis org + sandbox-looking TIN that only matters if that GUID is a real tenant.

### Still present?
**DOCS / HONESTY ONLY**

Factory fallback and omit-empty-TIN are unchanged:

```30:31:apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs
            CompanyName = FirstNonEmpty(profile?.LegalName, workspace?.Name, "Merchant"),
            CompanyTin = NullIfWhiteSpace(profile?.Tin) ?? "",
```

```50:51:apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs
                if (!string.IsNullOrWhiteSpace(_model.CompanyTin))
                    column.Item().Text($"TIN: {_model.CompanyTin}").FontSize(10).FontColor(Colors.Grey.Darken2);
```

Grep of `TIN not on file` in `*.cs` is empty; it remains only in `plans/007-feats/impl/W2-LP-107-done.md` and `W2-LP-107-analysis.md`. `W4-LP-100-done.md` already says Official Receipts no longer print `TIN: N/A` / `TIN not on file`. Tests still lock “not Lazuar Merchant” + empty TIN:

```50:64:apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Documents/InvoiceDocumentFactoryTests.cs
    public void CreateHeader_WithoutProfile_UsesWorkspaceName_NotLazuarMerchant()
    ...
        model.CompanyName.Should().Be("Studio Nine");
        model.CompanyName.Should().NotBe("Lazuar Merchant");
        model.CompanyTin.Should().BeEmpty();
```

Submit still throws if config is missing (`SubmitTaxDocumentCommand.cs:99–103`). Genesis seed is still `OrganizationId = 7d97963c-063c-4598-86cc-9ddd9d47d9b1`, `SupplierTin = "C12345678901"` (`LhdnDbContext.cs:47–61`). Ops legal fields are manual on `BillingProfilePage.tsx` (no auto-provision).

### Related files
- `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/InvoiceDocumentFactory.cs` — fallback name + empty TIN.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs` — print TIN only if present.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Documents/InvoiceDocumentFactoryTests.cs` — locks omit / not Lazuar Merchant.
- `plans/007-feats/impl/W2-LP-107-done.md` — stale “TIN not on file.”
- `plans/007-feats/impl/W4-LP-100-done.md` — later honest omit.
- `apps/lazuar-api/Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` — missing config throws.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/LhdnDbContext.cs` — genesis seed.
- `apps/lazuar-ops/src/modules/workspace/pages/BillingProfilePage.tsx` — manual legal / LHDN fields.

### Tests
- `InvoiceDocumentFactoryTests.CreateHeader_WithoutProfile_UsesWorkspaceName_NotLazuarMerchant`
- `InvoiceDocumentFactoryTests.CreateHeader_MapsSstSsmAndFullAddress` (full TIN present)
- `InvoiceDocumentFactoryTests.CreateHeader_TaxInvoice_DoesNotAddReceiptDisclaimer` / `CreateHeader_PendingInvoice_AddsValidationDisclaimer` / `OfficialReceiptDisclaimer_OnlyForReceiptsAndPendingInvoices`
- These would **fail** if someone printed “Lazuar Merchant.” They would **not** fail if W2-LP-107-done.md stayed wrong. Adding “TIN not on file” to empty TIN would **not** fail `CompanyTin.Should().BeEmpty()` (the model field is still empty; the string would have to be asserted in Notes or the PDF).
- First regression if you “fix” the done-file: a docs review, or a test that `OfficialReceiptDisclaimer` / header text does not contain “TIN not on file.”

### Reproduction today
Generate an Official Receipt for a workspace with no billing profile TIN. PDF seller line is the workspace name; there is no TIN line and no “TIN not on file.” Read W2-LP-107-done.md and see the stale sentence. Submit MyInvois without an Lhdn tenant config: exception “LHDN Tenant Configuration is missing.”

### Blast radius
Not customer-facing today. Done-file overclaim can be copied into a demo script. Genesis seed is a footgun only if `7d97963c-…` is provisioned as a live org. Frequency: every unpaid-legal-profile PDF (omit is fine); every reader of W2-LP-107-done.md.

### Suggested fix
Edit W2-LP-107-done.md to match W4-LP-100-done.md / the factory (omit empty TIN; workspace name or `"Merchant"`). Do not add “TIN not on file” to QuestPDF unless product wants a visible gap. Do not auto-provision Lhdn config from Card 2 in this ticket. Leave the genesis seed unless that GUID is confirmed live. No TypeSpec regen.

### Evaluation notes
This is an honesty leftover, not a stationery bug. 012/094 are the real title/VALID lies. Still P2 as docs. Not blocked. Do not change YAML status.

