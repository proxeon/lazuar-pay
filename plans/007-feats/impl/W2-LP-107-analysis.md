# W2-LP-107 — PDF branding

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-107`. Tracker: *PDF branding* — Lazuar **P**. Inventory `INV-011` / `INV-029`.  
**Not this ID:** Checkout logo/color (`LP-025` — **workspace / trading** plane). Merchant legal profile editor (`LP-122` — this ticket **consumes** it). Custom fonts / theme kits (refuse `LP-200`).

**Invariant:** Official Receipt, Tax Invoice, Credit Note, and draft Proforma print the **legal** seller: logo (if any), legal name, TIN, SSM, SST number (if any), and **full** registered address. Accent may stay platform blue. This is stationery for IRBM / expense claims, not a storefront skin.

---

## 0. Scope lock

In scope:

- `InvoiceDocumentModel` + `BaseInvoiceDocument` + draft handler
- Read `TenantBillingProfile` (legal plane)
- Print SST + SSM + address lines 1–2 + city/postcode/state
- Draft PDF: fetch logo the same way as final (today draft never sets `CompanyLogo`)

Out of scope:

- `--brand` / primary color from `Organization` (LP-025)
- Memo, payment instructions, custom footer HTML
- Per-document templates
- Mixing public billing profile into product checkout

---

## 1. Verdict

Tracker **P**: final PDFs already print legal name + TIN + **address line 1** + optional logo. They omit SST, SSM, rest of address, and buyer TIN. Drafts omit logo. Profile editor is hidden (LP-122), so most tenants print **“Lazuar Merchant” / TIN N/A**.

`ViewModelMapper` already maps LHDN supplier address from `LhdnTenantConfig`. **UBL `StandardInvoice.xml` still hardcodes Bangunan Merdeka.** That is LP-122 / LP-110, not this QuestPDF ticket — but do not claim “branded e-invoice” until that template is bound.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Billing/Infrastructure/Documents/InvoiceDocumentModel.cs` | CompanyName, Tin, Address (single string), Logo; no SST/SSM; no buyer TIN |
| `Modules/Billing/Infrastructure/Documents/BaseInvoiceDocument.cs` | Helvetica; `Colors.Blue.Darken2`; logo 50px; tax row unlabeled “Tax” |
| `GenerateAndStoreDocumentCommandHandler.cs` | Logo HTTP GET swallowed; `CompanyName = profile?.LegalName ?? "Lazuar Merchant"` |
| `GenerateDraftDocumentQueryHandler.cs` | Same name/TIN/line1; **no logo bytes** |
| `TenantBillingProfile.cs` | Has SST, SSM, full address, logo_url |
| `BillingProfilePage.tsx` | Editor; `[MVP-HIDE]` |

Workspace `Organization.LogoUrl` (if LP-025 adds it) is the **wrong** default for a tax invoice unless legal logo is empty **and** copy says so. Prefer legal logo only.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | SST # and SSM never printed |
| G2 | Address is line 1 only |
| G3 | Draft has no logo |
| G4 | Buyer block is name + email (no buyer TIN/address) — needed once LP-022 stores them |
| G5 | Fallback “Lazuar Merchant” is a lie if profile missing — show workspace name **or** refuse PDF until LP-122 saved |
| G6 | Tax line says “Tax” not “SST 8%” (LP-118) |

---

## 4. Recommended model

Extend `InvoiceDocumentModel`:

- `CompanyRegistrationNumber?`, `CompanySstNumber?`
- `CompanyAddress` = formatted multi-line from profile
- `CustomerTin?`, `CustomerCompanyName?` (from CRM)

`BaseInvoiceDocument` header: logo, legal name, TIN, SSM, SST (omit row if null), full address. Buyer: company/name, TIN if any, email.

Draft handler: same logo fetch as final (best-effort).

If no billing profile: still generate Official Receipt with **workspace name** + “TIN not on file” **or** skip legal-looking “Tax Invoice” title. Do not print Lazuar as the seller of tenant GMV.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `InvoiceDocumentModel` / `BaseInvoiceDocument` | Extra fields + layout |
| Both generate handlers | Map full profile + CRM buyer TIN |
| Tests | `GenerateAndStoreDocumentCommandHandlerTests` assert model fields |

Must not: QuestPDF color from checkout brand; fonts upload; change UBL templates here.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Profile with logo + SST + 2 address lines | Model filled; PDF generate does not throw |
| No profile | CompanyName ≠ `"Lazuar Merchant"` if workspace name exists |
| Draft | Logo bytes set when `LogoUrl` reachable (mock HttpClient) |
| Buyer TIN on CRM | Printed on B2B tax invoice model |

---

## 7. Acceptance

1. A tenant who saved Legal & Billing (LP-122) sees logo + legal name + TIN + SSM + SST + full address on receipt **and** draft quote.  
2. Product checkout branding can differ (trading name) without changing the PDF seller.  
3. No custom CSS/fonts.

Tracker **P → Y** after 1. Stay **P** if only logo works and SST/SSM still missing.

---

Do **not** implement from this file.
