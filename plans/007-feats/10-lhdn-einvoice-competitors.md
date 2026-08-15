# 10 — LHDN e-invoice and compliance competitors

**Program:** `plans/007-feats`  
**Scope:** Compliance competitors around **Malaysia IRBM / LHDN MyInvois** — the free portal, Peppol Access Points, local accounting/POS, cloud accounting, checkout+invoice stores, dedicated e-invoice APIs/middleware, and global tax engines — compared to the **Lazuar Pay `Lhdn` module** (ADR 021 moat, ADR 023 UI lobotomy).  
**Stance:** Full uncondensed analysis. Do not treat a row as a commitment to ship. Do not claim Lazuar’s backend is a sellable compliance product while the UI is hidden and v1.1 signing is unimplemented. Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar’s SaaS fee.  
**Date of research:** 2026-08-16  
**Workspace:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**Does not implement product code.**

Standing constraints (do not contradict) — from [`README.md`](./README.md) and [`00-evaluation.md`](./00-evaluation.md):

- Lazuar Pay is **BYOK software**, not a Merchant of Record and not an acquiring bank.
- Buyer money on Billplz / Stripe / CHIP (tenant keys) is not Lazuar’s SaaS fee.
- Do **not** sell WhatsApp dunning or LHDN e-invoice as live product until those loops are closed and (for LHDN) un-hidden.
- Do not become a website builder, marketplace, POS, or ERP to “match competitors.”
- Wrap rails (Stripe, Billplz, CHIP, later Xendit) — do not rebuild acquiring.
- Aura (salon) is a **customer** of Hub, not a competitor. System A (Paddle SaaS) and System B (Hub guest money) stay separate.
- Aura guest Connect keys must stay on the **Payments integrator** preset. **Do not add LHDN scopes** to that key.

---

## How to read this document

This is **not** a salon-OS feature bake-off. Fresha, Booksy, Boulevard, Mangomint, GlossGenius, Phorest, Mindbody, and Zenoti do **not** submit UBL 2.1 to `preprod-api.myinvois.hasil.gov.my`. They issue commercial invoices / receipts in US/EU/AU tax vocabularies. Treating them as MyInvois competitors is how a Malaysia checkout product wastes a year building the wrong thing.

This document answers five product questions:

1. **What is legally true in August 2026** — phases, exemption, RM10,000 rule, 72-hour window, document types, tax types vs classification codes, consolidation, self-billed, Peppol vs MyInvois.
2. **Who actually sells MyInvois compliance** to the businesses Lazuar would charge — SMEs crossing RM 1 million, digital sellers who need B2B TIN invoices at checkout, F&B/retail POS estates, accountants migrating off UBS.
3. **What Lazuar Pay’s `Lhdn` module actually does** — from live code, TypeSpec, SDKs, XML templates, golden-master samples, workers, and ADRs — not from README slogans.
4. **Whether ADR 021’s “ultimate moat” is sellable today** or is backend dark matter behind ADR 023.
5. **Which tracker IDs** already exist in [`00-checklist-tracker.md`](./00-checklist-tracker.md) (`LP-110`–`LP-123`, `LP-139`) and which finer `LH-*` residuals should be promoted if Wave 2 un-hides Compliance CaaS.

Three reading rules:

1. **MyInvois is a clearance CTC, not “email a PDF.”** IRBM validates a structured UBL 2.1 XML or JSON document, stamps a UUID + LongId, and the supplier shares a QR. A PDF without a UUID is a commercial receipt. Aura’s booking PDF (if an Aura tenant uses Hub) is a commercial receipt. Stripe’s fee invoice to a Malaysian account is Stripe’s tax document, not the merchant’s guest ticket.
2. **There is no LHDN-approved software list.** LHDN does not certify AutoCount, SQL, StoreHub, or Lazuar. Compliance is “did a valid document land on MyInvois.” MDEC accreditation is a **Peppol Access Point** badge, a different rail.
3. **Accounting software already won the SME desk.** The sellable MyInvois product in 2026 is “I click Submit in SQL / AutoCount / Xero and my TIN lookup works.” Lazuar’s claimed wedge is **compliance at the point of sale** (checkout → ledger → consolidated or B2B e-invoice). That wedge is only real if the checkout collects TIN, the ledger is honest, and a human can see the UUID. Today two of those three are hidden or stubbed.

Companion files in this folder (do not re-litigate):

| File | Boundary |
|------|----------|
| [`00-evaluation.md`](./00-evaluation.md) | Parent judgment. LHDN is inventory, not product. Wave 2 is un-hide. |
| [`00-checklist-tracker.md`](./00-checklist-tracker.md) | Official `LP-110`–`LP-123` / `LP-139`. This file is the evidence, not a second matrix. |
| [`01-lazuar-feature-inventory.md`](./01-lazuar-feature-inventory.md) | Pay ground truth (when landed). |
| [`15-invoicing-quotes-receipts.md`](./15-invoicing-quotes-receipts.md) | Quotes / tax invoices / receipts UI (hidden). |
| [`18-pricing-onboarding-trust.md`](./18-pricing-onboarding-trust.md) | Pricing, KYC, PDPA. |
| [`19-refuse-list-and-adjacents.md`](./19-refuse-list-and-adjacents.md) | What we will not copy (POS, ERP, MoR). |
| [`20-sequencing-and-tracker-schema.md`](./20-sequencing-and-tracker-schema.md) | Waves. Official rows live in `00-checklist-tracker.md`. |

---

## Absolute paths (live anchors)

| Concern | Path |
|---------|------|
| ADR 021 Compliance CaaS | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/docs/architecture-decision-log/021-compliance-caas-pivot.md` |
| ADR 023 UI lobotomy | `.../023-pure-caas-mvp-ui-lobotomy.md` |
| ADR 010 XML templating + XAdES placeholder | `.../010-xml-templating-for-b2b-integrations.md` |
| Lhdn module README | `.../apps/lazuar-api/Modules/Lhdn/README.md` |
| Domain `TaxDocument` | `.../Modules/Lhdn/Domain/Aggregates/TaxDocument.cs` |
| Domain `LhdnTenantConfig` | `.../Modules/Lhdn/Domain/Aggregates/LhdnTenantConfig.cs` |
| 72-hour rule | `.../Modules/Lhdn/Domain/Rules/CancelWindowMustBeValidRule.cs` |
| Submit command | `.../Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` |
| Cancel command | `.../Modules/Lhdn/Application/Commands/CancelTaxDocumentCommand.cs` |
| TIN validate | `.../Modules/Lhdn/Application/Commands/ValidateTaxpayerTinCommand.cs` + `.../Services/TaxpayerValidationService.cs` |
| Strategy factory | `.../Modules/Lhdn/Infrastructure/Services/DocumentStrategyFactory.cs` |
| View model / entity swap | `.../Services/Strategies/ViewModelMapper.cs` |
| Templates | `.../Modules/Lhdn/Infrastructure/Templates/*.xml` |
| Embedded UBL XSD | `.../Modules/Lhdn/Infrastructure/Schemas/` |
| Gateway (token/submit/status/TIN/cancel) | `.../Modules/Lhdn/Infrastructure/Gateways/LhdnGatewayAdapter*.cs` |
| Submit worker | `.../Workers/LhdnSubmissionJob.cs` |
| Poll worker | `.../Workers/LhdnStatusPollingJob.cs` |
| B2C monthly job | `.../Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` |
| Invoice issued stub | `.../EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` |
| Consolidated handler | `.../EventHandlers/ConsolidatedInvoiceIssuedIntegrationEventHandler.cs` |
| Refund → cancel/CN | `.../EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` |
| HTTP surface | `.../Infrastructure/Endpoints/*.cs` |
| TypeSpec | `.../packages/api-spec/modules/lhdn/{models,routes}.tsp` |
| TS SDK | `.../packages/lhdn-sdk-ts/` (`@lazuar/lhdn-sdk` 0.1.0) |
| .NET SDK | `.../packages/lhdn-sdk-dotnet/` (`Lazuar.Lhdn.Sdk`) |
| XML sample corpus | `.../docs/xml/{invoice,credit,debit,refund,self-billed-*}/` |
| XML-DSig pain notes | `.../docs/lhdn/000-xml-vs-json.md`, `001-xml-vs-json-2.md` |
| Sandbox scripts | `.../scripts/lhdn_sandbox/` |
| Ops UI hide | `.../apps/lazuar-ops/src/App.tsx` (`[MVP-HIDE]`) |
| Portal TIN hide | `.../apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` |
| Quote checkout blocked | `.../apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx` |
| Official timeline | https://www.hasil.gov.my/en/e-invois/pelaksanaan-e-invois-di-malaysia/garis-masa-pelaksanaan-e-invois/ |
| Official SDK | https://sdk.myinvois.hasil.gov.my/ |
| Document types | https://sdk.myinvois.hasil.gov.my/codes/e-invoice-types/ |
| Tax types | https://sdk.myinvois.hasil.gov.my/codes/tax-types/ |
| Classification codes | https://sdk.myinvois.hasil.gov.my/codes/classification-codes/ |
| Submit API | https://sdk.myinvois.hasil.gov.my/einvoicingapi/02-submit-documents/ |

---

## Method

### Research method

This file was produced on **2026-08-16** by one subagent with two parallel workstreams.

### A. Lazuar ground truth (code wins)

Inspected, not summarized from marketing:

- Modular monolith `Modules/Lhdn/` (Application / Domain / Contracts / Infrastructure).
- Billing `B2cConsolidationJob` and the three Lhdn integration-event handlers.
- TypeSpec `packages/api-spec/modules/lhdn/` and generated Kiota trees in `packages/lhdn-sdk-ts` and `packages/lhdn-sdk-dotnet`.
- Five Scriban templates + OASIS UBL 2.1 XSD set.
- `docs/xml/` official-shaped samples (invoice, consolidated, foreign currency, multi-line, credit, debit, refund, four self-billed types, one signed XAdES example).
- ADRs 010, 021, 023; module README (honest about unsigned v1.0); `docs/lhdn/000`–`001` (XML-DSig “Root element is missing”).
- Ops / portal `[MVP-HIDE]` markers.

Older gap notes under `docs/001-gaps/09-lhdn-module.md` were treated as **historical**. Several P0s in that note are already closed in code (tenant config CRUD, TIN validate endpoint, SKIP LOCKED leases, legal address columns, One-owned API keys, durable webhooks). Residuals that remain are called out in the audit with file paths.

### B. Regulatory + competitor (primary pages, then secondary)

Official / near-official (fetched 2026-08-16):

- IRBM timeline page (updated **7 December 2025**): exemption **< RM 1,000,000**; Phase 4 “up to RM 5 million” from **1 January 2026**.
- MyInvois SDK code tables: e-invoice types, tax types, classification codes.
- Submit Documents API (XML **and** JSON; 100 RPM/client recommended).
- ClearTax Malaysia guide (updated 17 July 2026) and JomeInvoice guide (v4.6 / 22 April 2026 update) for relaxation, RM10,000 rule, self-billed list, penalties. These are **vendor pages quoting IRBM**. Where they disagree with hasil.gov.my, **hasil.gov.my wins**.

Competitor product pages fetched or searched the same day: StoreHub (blog still live 14 Aug 2026), AutoCount AIP, SQL Account / E Stream, Access UBS, Xero MY, QuickBooks MY + Sovos, EasyStore intermediary setup, JomeInvoice middleware ranking (22 July 2026), ClearTax MDEC Peppol, Stripe Tax supported-countries table (Malaysia = digital products / Service tax / **business location not supported**), Avalara Malaysia e-invoicing blog, MDEC Peppol SP page.

**Not found as first-class 2026 MyInvois products under the exact names requested:** Invoprint, B2Binvoice, Snapshot. Those names are recorded as **requested-but-unlocated** in the dedicated-API section, and the 2026 field is filled with the players that *do* appear (JomeInvoice, ClearTax, Flick Network, InvoisPro, Finexus AREMA, i-invoice.my, QNE e-Integrator, Daxonet, BDO middleware, Awantec, MountainTop / SL Info, Pagero).

**FastAccount:** no durable Malaysian MyInvois product page under that exact brand was located on 2026-08-16. Nearby names: Fast e-Invoice (generic mobile app), FastAccounts.io (non-MY), Financio, Bukku, QNE. The dossier is written as “requested name + nearest MY substitutes,” not as a fake product.

### Honesty rules used throughout

- **Code and official SDK tables win** over vendor blogs when they disagree.
- **A template placeholder is not a signature.**
- **A hidden React route is not a shipped UI.**
- **A stub buyer TIN is not TIN validation.**
- **Classification code `008` is e-commerce, not “export zero-rate.”** Official tax types are `01`–`06` and `E`. There is **no tax type `08`** on the 2026-08-16 SDK table. Export / foreign is usually tax type `E` or `06` plus classification `032` (Foreign income) and general TIN `EI00000000020` for foreign buyers.
- **Peppol ≠ MyInvois.** Peppol is B2B document exchange. MyInvois is IRBM clearance. A business can need both. Lazuar implements neither Peppol nor a complete inbound MyInvois loop.
- Dates move. IRBM updated the timeline on 7 Dec 2025; Specific Guideline went 4.6 (Jan 2026) then 4.7 (Apr/Jul 2026). Do not hardcode a go-live in marketing copy without re-checking hasil.gov.my.

### What this file will not do

- Will not invent a second MyInvois client inside Aura. Aura is a Hub customer; LHDN lives here.
- Will not recommend building a second SQL Account.
- Will not treat Stripe Tax or Avalara as MyInvois substitutes.
- Will not flip `LP-110`–`LP-116` from **B** to **Y** until merchant nav exists and stubs are gone.

---

## Regulatory reality 2026

### What MyInvois is

Malaysia’s e-invoice is a **Continuous Transaction Control (CTC) clearance** system run by **Lembaga Hasil Dalam Negeri Malaysia (LHDN / IRBM)**.

The legal hook vendors cite is **Income Tax Act 1967** — commonly **s.82C** for the obligation to issue a valid e-invoice and **s.120(1)(d)** for the offence / penalty band. Penalty quotes in circulation on 16 Aug 2026: **RM 200 – RM 20,000 per invoice**, and/or **up to 6 months’ imprisonment**. Treat the statute as the authority; vendor blogs compress section numbers.

Operationally:

1. Supplier (or a appointed **intermediary**) builds a structured document in **UBL 2.1**, serialized as **XML or JSON**.
2. Document is submitted to MyInvois (`POST /api/v1.0/documentsubmissions`, recommended **100 requests/minute/client id**).
3. IRBM validates in near real time (vendor FAQs say “about two seconds”; do not depend on that SLA).
4. On success IRBM returns a **UUID**, **submissionUid**, and later a **LongId**. The share URL shape Lazuar already constructs is `{portal}/{uuid}/share/{longId}` — this is the **QR payload**.
5. Supplier shares the validated e-invoice (human-readable PDF/JPG **plus** QR) with the buyer.
6. Buyer can **reject**; supplier can **cancel** — both inside **72 hours of validation**. After 72 hours the document is frozen; corrections are a new **Credit Note (02)**, **Debit Note (03)**, or **Refund Note (04)** that **references the original UUID**.

This is **not** SST-02 filing. SST is a **Customs / RMCD** tax recorded *on* the e-invoice as a tax type. MyInvois is an **income-tax audit trail**. A merchant can be SST-exempt on some supplies and still be in-scope for e-invoice once turnover crosses the IRBM threshold. Conversely, a business can be SST-registered and still exempt from e-invoice if turnover is under RM 1 million (subject to related-party caveats).

### Two rails that marketing collapses into one word

| Rail | Owner | Job | Mandatory for domestic tax clearance? |
|------|-------|-----|----------------------------------------|
| **MyInvois** | IRBM / LHDN | Validate structured invoices; stamp UUID | **Yes**, for in-scope taxpayers |
| **Peppol** (PINT-MY / National e-Invoicing Initiative) | **MDEC** is Malaysia’s Peppol Authority | Exchange structured invoices between Access Points, including cross-border | **No** for LHDN clearance. Useful for B2B interoperability and some government-linked trading |

Xero’s Malaysian marketing still leads with “e-Invoicing uses the Peppol network.” That is **true of Xero’s product story** and **incomplete as a legal description**. A Xero invoice that never lands on MyInvois is not an IRBM-validated e-invoice. Airwallex’s 2026 explainer is the cleaner split: Peppol = international document exchange; MyInvois = Malaysian tax validation.

Lazuar today: **MyInvois API client (outbound submit/poll/cancel/TIN)**. Not a Peppol Access Point. Not MDEC-accredited. `IntermediaryMode` + `onbehalfof` header means Lazuar *can* act as a MyInvois intermediary if the taxpayer appoints the platform in MyTax — the same appointment StoreHub and EasyStore ask merchants to click.

### Implementation timeline (official table, 7 Dec 2025)

From [hasil.gov.my — e-Invoice Implementation Timeline](https://www.hasil.gov.my/en/e-invois/pelaksanaan-e-invois-di-malaysia/garis-masa-pelaksanaan-e-invois/):

| Targeted taxpayers (annual turnover / revenue) | Implementation date |
|------------------------------------------------|---------------------|
| More than RM 100 million | 1 August 2024 |
| More than RM 25 million and up to RM 100 million | 1 January 2025 |
| More than RM 5 million and up to RM 25 million | 1 July 2025 |
| **Up to RM 5 million** | **1 January 2026** |

**Exemption (same page):** taxpayers with annual turnover or revenue **less than RM 1,000,000** are exempted. The page notes the timeline was updated **7 December 2025** (the Anwar / Cabinet raise from the earlier RM 500,000 floor).

Phase assignment is sticky. Public guidance (ClearTax 17 Jul 2026, JomeInvoice 22 Apr 2026, BDO May 2026):

- Default reference year is **YA 2022 audited accounts** (or first available year for new businesses).
- Once mandated, a later drop below RM 1 million **does not restore exemption**.
- Sole proprietors **aggregate** revenue across businesses they own.
- **Related-party / subsidiary / non-individual shareholder ≥ RM 1 million** can pull a small entity into scope (JomeInvoice: from **1 July 2026** regardless of own revenue). Confirm on the latest IRBM FAQ before using this in sales copy.
- New businesses that commenced 2023–2025 and now exceed RM 1 million: vendor tables give **1 July 2026** as an accommodation start. Official timeline page does not spell this row; treat as FAQ-dependent.

### Relaxation vs obligation (do not confuse)

Vendor pages disagree on the **end of Phase 4 relaxation**:

| Source (16 Aug 2026) | Phase 4 (RM 1–5 m) relaxation ends | Full enforcement |
|----------------------|------------------------------------|------------------|
| Official timeline page | Not tabulated | Obligation from 1 Jan 2026 |
| ClearTax (17 Jul 2026) | **31 Dec 2027** (extended; full from 1 Jan 2028) | 1 Jan 2028 |
| JomeInvoice (22 Apr 2026 update) | **31 Dec 2027** | 1 Jan 2028 |
| JomeInvoice body text elsewhere | “full penalty enforcement from 1 Jan 2027” | internally inconsistent |
| QuickBooks MY page | 30 Jun 2026 (and still lists a cancelled Phase 5) | stale |
| Vatcalc (21 Apr 2026) | further delay language toward 2028 | moving |

**Product rule:** obligation exists from **1 Jan 2026** for RM 1–5 m. **Penalty posture is soft through at least end-2026 and, on the better 2026 sources, through 2027.** The **RM 10,000 individual-invoice rule is not relaxed.** Do not tell a merchant “you can ignore MyInvois until 2028.”

**Phase 5 (sub-RM 1 m) was cancelled** after the 7 Dec 2025 announcement. QuickBooks’ public page still listing Phase 5 / 1 Jul 2026 is **stale**. Xero’s public initiative page still quoting RM 500k–25 m / 1 Jul 2025 is **stale**.

### What “exempt” means for Lazuar ICP

A typical neighbourhood shop under RM 1 million is **not** in the current mandatory wave. They still get the corporate buyer who says “please e-invoice this invoice.” That buyer is the **only** e-invoice pain most small tenants feel. The honest product is: export a CSV / send them to MyInvois Portal / (later) a Pay “issue this one B2B e-invoice” button. Building a full AIP to beat StoreHub for exempt shops is a company-shape trap (`19` refuse-list: do not become a POS/ERP).

Lazuar’s **stated** ICP in ADR 021 is not that shop. It is the **professional digital business / agency / high-volume creator** who is or will be in Phase 4+, and who sells **at checkout** (B2C consolidation + occasional B2B TIN). That ICP **is** in the 2026 mandate if they clear RM 1 million. [`00-evaluation.md`](./00-evaluation.md) restates the wedge: Malaysian/SEA software-shaped businesses that must take FPX, must run subscriptions, **must file MyInvois**, and must unlock a third-party app.

### Document types (official SDK)

https://sdk.myinvois.hasil.gov.my/codes/e-invoice-types/

| Code | Description | Typical trigger |
|------|-------------|-----------------|
| **01** | Invoice | Standard sale (B2B, B2C individual, B2G) |
| **02** | Credit Note | Reduce a validated invoice after 72 h (or as adjustment) **without** returning cash |
| **03** | Debit Note | Increase a validated invoice |
| **04** | Refund Note | Record cash returned to the buyer |
| **11** | Self-billed Invoice | Buyer issues on behalf of seller (closed list of scenarios) |
| **12** | Self-billed Credit Note | Adjustment of 11 |
| **13** | Self-billed Debit Note | Adjustment of 11 |
| **14** | Self-billed Refund Note | Refund against 11 |

Lazuar TypeSpec `DocumentType` is exactly this set. Factory routing:

- `01` + empty TIN or `EI00000000010` → `B2CConsolidatedInvoice`
- `01` otherwise → `B2BStandardInvoice`
- `02` / `03` / `04` → one `CreditNote` template with `{{ doc_type_code }}`
- `11` → `SelfBilledInvoice`
- `12` / `13` / `14` → `SelfBilledCredit` template

That grouping is structurally correct (UBL Invoice schema is reused). It is **not** the same as having dedicated Schematron for debit vs refund vs credit.

### Tax types vs classification codes (the “08” trap)

#### Official tax types (SDK, 16 Aug 2026)

https://sdk.myinvois.hasil.gov.my/codes/tax-types/

| Code | Description |
|------|-------------|
| **01** | Sales Tax |
| **02** | Service Tax |
| **03** | Tourism Tax |
| **04** | High-Value Goods Tax |
| **05** | Sales Tax on Low Value Goods |
| **06** | Not Applicable |
| **E** | Tax exemption (where applicable) |

Lazuar TypeSpec `TaxTypeCode = "01" | "02" | "03" | "04" | "05" | "06" | "E"`. **Matches the official table.**

There is **no tax type `08`**. Anyone selling “tax type 08 = export zero-rate” is mixing eras (GST-era codes) or mixing **classification** with **tax type**.

#### Official classification codes (subset that matters here)

https://sdk.myinvois.hasil.gov.my/codes/classification-codes/

| Code | Meaning | Lazuar usage today |
|------|---------|-------------------|
| **004** | Consolidated e-Invoice | Forced when buyer TIN `EI00000000010` **and** id value `NA` (`ViewModelMapper`) |
| **008** | e-Commerce — e-Invoice to buyer / purchaser | **Not** export. Marketplace / online sale to buyer |
| **009** | e-Commerce — Self-billed to seller, logistics, etc. | Platform commission / logistics self-bill |
| **022** | Others | Default when classification omitted (non-consolidated) |
| **032** | Foreign income | Closest official bucket for export / overseas supply **classification** |
| **034 / 035** | Self-billed — importation of goods / services | Import self-bill |
| **044** | Vouchers, gift cards, loyalty points | Relevant to CaaS credits / gifts |

**Export / zero-rate on a MyInvois document is not a magic `08`.** Typical correct shape:

- Buyer TIN **`EI00000000020`** (foreign buyer general TIN) when the buyer has no Malaysian TIN.
- Tax type **`E`** (exemption) with an exemption reason, **or** **`06`** (not applicable) when SST does not apply to the supply.
- Classification chosen from the 45 codes — often **`032` Foreign income** or a goods/services code, **not** `008` unless it is actually an e-commerce sale to a purchaser.

ADR 021 Pillar 3 (“automatically classifies the ledger entry as an export, applying the correct zero-rated tax codes”) is **product intent**, not current mapper behaviour. The mapper does not look at country code to pick `032` / `E` / `EI00000000020`. Foreign-currency sample XML exists in `docs/xml/invoice-v1-1/1.1-Invoice-ForeignCurrency-Sample.xml`; live templates hardcode **`MYR`**. Tracker row **`LP-119`** is **N** for a reason.

SST-registered suppliers must put **SST** (and TTX where relevant) on the **party identification** block. Official sample in `docs/xml/README.md` has `schemeID="SST"` and `schemeID="TTX"`. Lazuar live templates **omit SST/TTX party IDs**. Tax type `01`/`02` on a line is not a substitute for the supplier’s SST number. Tracker **`LP-118`** is **P**.

### General TINs (must be exact)

Public guidance (Specific Guideline appendix; repeated by every middleware):

| TIN | Meaning |
|-----|---------|
| **EI00000000010** | General public (B2C / consolidated) |
| **EI00000000020** | Foreign buyers |
| **EI00000000040** | Government entities |
| Individual prefix **IG…** | Individual taxpayer TIN |

Lazuar consolidated handler correctly uses `EI00000000010` + `NA` + name `General Public`. The B2B `InvoiceIssued` handler uses a **fake** `C1234567890`. That is not a general TIN; it is a stub that will fail TIN validate or produce junk UUIDs if it ever runs against production MyInvois.

### B2B vs B2C vs consolidation

**B2B / requested B2C:** individual e-invoice with real buyer TIN + ID type (BRN / NRIC / PASSPORT / ARMY) + ID value. Validate TIN **before** submit (`GET` taxpayer validate). Corporate buyers need this to claim.

**B2C walk-in / checkout with no request:** supplier may **consolidate** eligible transactions into one monthly e-invoice to general public TIN, classification **004**.

Hard rules in circulation for 2026 (Guideline v4.6 / v4.7 as quoted by JomeInvoice / ClearTax; re-check IRBM before coding deadlines):

1. Consolidated e-invoice must be submitted within **7 calendar days after month-end** (not “whenever the 28th job runs”).
2. From **1 January 2026**, any **single transaction ≥ RM 10,000** **cannot** be consolidated — all industries, all phases, **including** the relaxation period.
3. If the buyer requests an individual e-invoice **in the same calendar month**, that sale is pulled out of the consolidation.
4. Some industries **cannot consolidate at all** (Table 3.6): automotive vehicle sales; aviation tickets/charter; construction contracts under ITA; licensed betting/gaming; payments to agents/dealers/distributors (s.83A); **electricity** and **telecommunications** from 1 Jan 2026; luxury/jewellery was on hold. Digital-product CaaS is **not** on that prohibited list, but a RM 10k mastermind invoice is.

**Lazuar `B2cConsolidationJob`** fires around the **28th 02:00 MYT** with catch-up for closed months (24-month lookback). That is **earlier** than month-end+7, which is legally fine (early is ok; late is not). It does **not** implement the RM 10,000 split. It does **not** exclude buyer-requested individuals. It groups by `TaxTypeCode` + `MsicCode` and emits one `ConsolidatedInvoiceIssuedIntegrationEvent` per org-period. Classification on the event path is whatever the ledger put in `MsicCode` — the Lhdn mapper then **overwrites** to `004` when it sees general public TIN.

ADR 021 says “on the 28th of every month.” IRBM says “within 7 days after month-end.” 28th is a product choice that usually lands inside the window. A merchant who only has sales on the 30th/31st is still in the *next* calendar month’s job — those sales belong to that month’s consolidation, not the 28th run. Catch-up handles downtime; it does not invent a mid-month RM 10k individual invoice.

### 72-hour cancel vs rejection

| Actor | Window | API | After window |
|-------|--------|-----|--------------|
| Supplier **cancel** | 72 hours from **validation** | `PUT .../documents/state/{uuid}/state` | Issue 02/03/04 referencing UUID |
| Buyer **reject** | 72 hours from validation | Reject document API | Cannot reject; ask supplier for CN/DN/RN |

Lazuar implements **supplier cancel** (`CancelTaxDocumentCommand` + domain `CancelWindowMustBeValidRule` = 72 h from `ValidatedAt`). Refunds within 72 h call the same cancel. **Buyer reject is not implemented** — no inbound document pull, no reject endpoint, no “customer rejected your e-invoice” webhook. Tracker **`LP-116`** is **B** and oversells “reject”; code is cancel-only.

AutoCount V2 and JomeInvoice inbound modules exist specifically because the 72-hour reject window is how AP clerks stop junk supplier invoices. A checkout CaaS that only *issues* can live without inbound. A “compliance OS” cannot.

### Self-billed (closed list)

Self-billed is **not** “my supplier forgot.” Guideline §8.3 scenarios (vendor restatement):

- Payments to **agents, dealers, distributors**
- Payments to **foreign suppliers** (they are not required to issue MY e-invoices)
- Payments to **individuals not conducting a business** (personal landlord, personal asset sale)
- **e-Commerce platform fees and commissions** (Shopee / Lazada / Grab issue self-billed to merchants)
- **Profit distributions**
- **Import of goods and services**
- **Insurance claims / compensation / benefits**

Lazuar README positions self-billed as **affiliate / contractor payouts**. That can map to “agents/dealers” or “individuals not in business” depending on facts. `ViewModelMapper` does an **entity swap** (tenant → buyer nodes, request party → supplier). Factory accepts 11–14. There is **no** scenario enum, **no** classification default to 037/045/034/035, **no** product UI to explain why the merchant is allowed to self-bill. Tracker **`LP-115`** is Wave **4**, backend only.

### Digital certificate / v1.0 vs v1.1

IRBM issues (via licensed CAs: Pos Digicert, MSC Trustgate, TM Node, etc.) an **X.509** used to sign **v1.1** documents. v1.0 unsigned documents were the on-ramp. Production direction is signed v1.1. ClearTax’s 2026 guide still says “all e-invoices must be digitally signed.”

Lazuar:

- Templates wrap `<!-- SIGNATURE_PLACEHOLDER -->` when `document_version == "1.1"`.
- Certificate vault **stores** P12 + passphrase (AES-256-CBC; legacy plaintext PFX fallback).
- **No submit path calls `GetDecryptedCertificate` to sign.**
- Sandbox script `05_test_b2b_v1_1.sh` is the known-fail path (`Root element is missing` from LHDN’s XML-DSig/XPath).
- `docs/lhdn/000`–`001` recommend **JSON format + string hash** to dodge LHDN’s brittle XML C14N. **Not implemented.** Workers still submit `"format": "XML"`.

Unsigned v1.0 can still clear sandbox and, for some taxpayers, production — but it is not the 2026 “gold standard” competitors sell. Tracker **`LP-117`** is **N**.

### QR

Validated document → LongId → share URL → QR. Buyer (or LHDN app) scans to confirm the UUID is real.

Lazuar poller builds `qr_link` via `ILhdnLinkService.GetPortalUrl()` + `/{uuid}/share/{longId}` and puts it on the outbound webhook payload. Ops tax-invoice UI exists but is **unrouted**. Portal “Download Tax Invoice” is **hidden**. So the QR exists as a **string in a webhook**, not as a merchant or buyer surface. Tracker **`LP-113`** is **B**.

### Transmission models (how the market is actually segmented)

| Model | Who uses it | Cost shape | Lazuar analogue |
|-------|-------------|------------|-----------------|
| **MyInvois Portal** (manual + spreadsheet batch) | MSMEs, accountants for one-off B2B, exempt shops when a corporate asks | **Free** | None (and should not clone the portal) |
| **Native module** inside SQL / AutoCount / UBS / Xero / QBO / Bukku / QNE | Single-ledger SMEs | Included in accounting SKU or a small add-on | None — Lazuar is not a GL |
| **POS native** (StoreHub, EasyStore, AutoCount POS, SQL POS) | Retail / F&B / some services | “Included, appoint us as intermediary” | **This is the CaaS claim** |
| **Middleware / dedicated API** (JomeInvoice, ClearTax, Flick, InvoisPro, Finexus, QNE e-Integrator, Pagero) | Multi-system estates, ERPs, marketplaces | Subscription × volume × connectors | **This is what Lazuar’s API + SDKs actually are** |
| **Custom build** against SDK | In-house IT | Capex + forever maintenance | What Lazuar did for itself |

Lazuar’s TypeSpec/SDK posture is **model 4** (middleware API). ADR 021 marketing is **model 3** (POS/checkout native). ADR 023 shipped **neither** to a user.

### Incentives (do not sell as product)

Budget 2024–2027: MSMEs may claim a **tax deduction up to RM 50,000 per YA** for e-invoice implementation costs (consultation, qualifying ICT). AutoCount still advertises a 50% MSME digitalisation grant path. This is RMCD/MOF, not a Lazuar feature.

---

## Competitor dossiers

Each dossier: who they are, how they touch MyInvois, document coverage, what they do that Lazuar does not, what they cannot do that ADR 021 wants, and the honest competitive implication.

### 1. LHDN MyInvois Portal (manual) — the default incumbent

**What it is.** Free IRBM web app at `mytax.hasil.gov.my` / `myinvois.hasil.gov.my` (preprod twin for sandbox). Login via Digital Certificate / MyTax. Issue one document at a time or **upload a spreadsheet** in IRBM’s template. View sent/received, cancel, reject, download, QR.

**Who it wins.** Anyone under ~30 invoices/day (JomeInvoice’s own “you don’t need us” threshold), every accountant who does not want a new SKU, every exempt shop that needs **one** B2B e-invoice for a corporate buyer, every business in a fight with their software vendor.

**Coverage.** All eight document types. Consolidation. Self-billed. TIN search. 72-hour cancel/reject. Inbound. QR. Human language Bahasa + English. Mobile-usable (IRBM FAQ).

**What it is not.** An API. A checkout. A ledger. A dunning engine. It will not sit on a Billplz payment-complete webhook and emit 01/04. It will not validate TIN at the moment a guest types it into `/pay`.

**Implication for Lazuar.** The portal is the **price floor: RM 0**. Any CaaS compliance SKU must beat the portal on *labour*, not on “we have XML.” For a 12-invoice-a-month consultant, the portal wins forever. For 3,000 B2C checkouts a month, the portal is impossible — that is the only volume at which Lazuar’s workers matter.

**Do not compete by cloning the portal UI.** That is how you inherit 55-field form support debt. Compete by never showing those 55 fields.

The informal stack named in [`00-evaluation.md`](./00-evaluation.md) is WhatsApp catalogue + Instagram + a Billplz/ToyyibPay link + Excel + **this portal at month-end**. We do not beat that with more settings pages. We beat it by making the Billplz click *also* create a subscription, a receipt, and (later) a legal invoice.

### 2. MyInvois Intermediary + Peppol Access Points

**Intermediary (MyInvois).** A taxpayer appoints a company in MyTax (**Representatives → Intermediary**) by TIN + BRN + name. The intermediary’s client credentials then call MyInvois with header **`onbehalfof: {taxpayer TIN}`**. StoreHub, EasyStore, SQL, AutoCount, Access UBS, Xero, and Lazuar (`IntermediaryMode`) all use this.

This is **not** Peppol. It is IRBM OAuth + a header.

**Peppol Access Point.** MDEC-accredited Service Providers operate an AP + SMP registration (`list.malaysiasmp.my`). They move UBL/PINT documents between networks. ClearTax, Xero, Sovos (behind QuickBooks), Pagero, Basware, EDICOM, Flick (as marketed), and others sell this.

**Why both exist.** A GL company in Singapore wants a Peppol invoice; IRBM still wants a MyInvois UUID. Serious middleware (ClearTax, Pagero) does **both**. Local POS (StoreHub) usually does **only MyInvois intermediary**.

**Lazuar:** intermediary header yes; Peppol no; no SMP; no MDEC badge. Fine for a Malaysia-only checkout. Fatal if the sales story is “PEPPOL BIS 3.0 reusable for GSTN/Coretax/InvoiceNow” (ADR 010’s long-range claim) without actually speaking Peppol.

**Implication.** Do not spend a quarter becoming an Access Point unless a named enterprise deal requires PINT-MY. The moat ADR 021 described is **MyInvois at POS**, not the European AP market. Tracker: do **not** add a Peppol row as Wave 2.

### 3. StoreHub — POS that appointed itself intermediary

**Who.** SEA cloud POS (F&B, retail, some services). ~20,000 businesses in their 2026 footer copy. The till an informal MY shop actually names, alongside SQL POS.

**MyInvois motion (storehub.com blog, still dated Nov 2024, page last-seen 14 Aug 2026):**

- Appoint StoreHub as intermediary on MyTax.
- e-Invoicing API **included in the standard plan** — they explicitly contrast “accounting software that charges extra for API.”
- Staff do not learn MyInvois; they use the POS. Customer **scans a QR on the receipt** to request an individual e-invoice; StoreHub **batches the rest monthly** to LHDN.
- Accounting export to **QBO, Financio, SQL**.

Instagram/YouTube creative in 2025–2026 is the same sentence: “forget 55 fields; scan QR; we submit.”

**What they have that Lazuar does not**

- A **cashier** who already exists at the moment of sale.
- **Post-transaction request** via receipt QR (the B2C pattern AutoCount AIP also sells).
- Hardware + offline + inventory + loyalty — the reason the shop bought them.
- A **visible** e-invoice toggle and training path.

**What they do not have**

- A headless checkout API for digital products / subscriptions.
- WhatsApp dunning as a first-class CaaS loop (ADR 021 keep-list; still a stub here).
- Developer SDKs aimed at other SaaS products.
- Self-billed affiliate payouts as a platform primitive.

**Implication.** StoreHub is the **retail/F&B consolidation** competitor, not the API competitor. If Lazuar un-hides LHDN for a café, StoreHub already won. If Lazuar stays a **headless checkout for digital / services businesses**, StoreHub is adjacent, not a substitute — unless the same owner also has a till (many do). `19` already refuses becoming a POS to match them. The coexistence story: Hub is not the till.

`00-checklist-tracker.md` correctly **does not give StoreHub a column**. They would score **Y** on `LP-110`–`114` and **N** on checkout/dunning.

### 4. AutoCount — the SME accounting OS with its own AIP

**Who.** Dominant MY on-prem + cloud accounting + POS (Accounting V2.2, Cloud Accounting, POS, OneSales). Dealer network, Bahasa/Mandarin/English training (calendar still booking **Aug–Sep 2026** Zoom sessions on the product page).

**Product:** **AutoCount e-Invoice Platform (AIP)** — their own middleware in front of MyInvois, used by all AutoCount skins so they can “auto-sync minor LHDN API changes” without a full desktop upgrade.

Marketed capabilities (product page, 16 Aug 2026):

- Standard + consolidated + self-billed.
- Approval workflow before submit (cuts 72-hour cancels).
- Consolidated by **outlet**; receipt QR for post-sale individual request.
- Self-billed **Quick Copy** from Purchase Invoice / Payment Voucher / Journal.
- **Get TIN** from BRN or NRIC via LHDN.
- Upload taxpayer QR from MyInvois Portal to auto-fill entity.
- Auto-email validated e-invoice.
- V2: edit within 72 h **without changing document number**; inbound reject notifications; import supplier e-invoices; supplier e-invoice reconciliation.
- 24/7 queue + retry when MyInvois is down.
- MSIC / classification / UOM lists auto-updated from LHDN.

**What they have that Lazuar does not**

- A **general ledger**. Compliance is a button on an invoice the bookkeeper already typed.
- Inbound + AP reconciliation (the other half of CTC).
- TIN onboarding that is not a raw `POST /taxpayer/validate`.
- Dealer + training + grant paperwork.
- POS + accounting in one vendor (the SQL-killer pitch).

**What they do not have**

- A payment-complete → e-invoice loop for a third-party checkout (Billplz/Stripe) unless the merchant also runs AutoCount as the system of record.
- Public developer API for other SaaS to embed.

**Implication.** AutoCount is who the **accountant** installs when Phase 4 frightens the owner. Lazuar will not displace AutoCount. Lazuar can **feed** AutoCount (export / webhook) or sit **upstream** of it (checkout writes the UUID the GL later imports). Competing for the GL seat is a company-shape error (`LP-121` is Wave 4; `19` refuse ERP).

### 5. SQL Account / SQL POS / EBI Wellness — TIN database + dealer gravity

**Who.** E Stream MSC / SQL ecosystem. Perpetual-license culture + nationwide dealers. SQL Account, SQL Inventory, SQL POS, and **SQL EBI Wellness POS**.

**MyInvois motion (sql.com.my/e-invoice and docs.sql.com.my, 2026):**

- Connect MyInvois from **File → Company Profile → MyInvois** (BRN lookup, TIN, intermediary appointment).
- One-click submit; **batch / consolidated** submit; status refresh.
- **~1.4 million** built-in business records for TIN / BRN search (“no more chasing customers”).
- MyInvois transaction dashboard (sent + received).
- Self-billed.
- WhatsApp send of quotations / invoices / statements (informal stack glued to the GL).
- Journal entry → e-invoice (2026 marketing).
- SQL POS: offline till, “LHDN e-invoice ready,” feeds SQL Account.

**What they have that Lazuar does not**

- The **TIN master** bookkeepers actually trust.
- Dealer who drives to the shop and installs.
- Wellness POS + Account + e-invoice as **one quote**.
- Inbound dashboard.

**What they do not have**

- Hosted checkout + FPX + subscription dunning.
- An API product for other platforms (they are the platform).

**Implication.** Do not become SQL Account. SQL is **downstream**. Honest Hub story: “we are not your MyInvois system *until Wave 2 is sold*; when you cross RM 1 m, SQL/AutoCount/your accountant can take the UUID we emit; or we become the issuer if you never open SQL.” Lazuar CaaS only becomes the MyInvois system for merchants who **never open SQL** — digital-native, no bookkeeper, high B2C volume. That is the thin slice `00-evaluation.md` already named.

### 6. Access UBS — the legacy GL being migrated off

**Who.** The older Malaysian desktop accounting brand (now Access Group / Access UBS / UBS Evo). Still on many SME PCs. 2026 content exists: intermediary setup KB, eInvoicing FAQ (individual / batch / consolidated / self-bill from AP), YouTube “how UBS connects to MyInvois,” dealer migration pages **“How to Migrate from UBS to SQL Account (2026).”**

**MyInvois motion.** Intermediary appointment + desktop submit. Access UBS Evo markets AI-assisted transmit/receive. It works well enough that owners have not all left — and badly enough that SQL/AutoCount run migration webinars.

**Implication.** UBS is not a 2026 product strategy threat. It is a **data-migration** source. If Lazuar ever imports opening AR, UBS/SQL CSV is the format. Do not build a UBS connector as a feature; build a **CSV of invoices + TINs**.

### 7. FastAccount (requested name) and the cloud-MY substitutes

**Requested:** FastAccount. **Located on 16 Aug 2026:** no first-class Malaysian MyInvois product under that exact brand. Nearby:

| Name | What it is | MyInvois |
|------|------------|----------|
| **Fast e-Invoice** (Play Store) | Generic mobile e-invoice app | Unverified as IRBM API; treat as portal helper |
| **FastAccounts.io** | Non-MY cloud accounting | Not a MyInvois player |
| **Financio** | MY cloud accounting; StoreHub names it as an export target | Native / partner MyInvois (cloud-SME tier) |
| **Bukku** | MY cloud accounting | Native e-invoice webinars / module since 2024 |
| **QNE Accounting + QNE e-Integrator** | MY accounting + Excel/CSV middleware | Native module **and** a middleware SKU |

`00-evaluation.md`’s shorthand “FastAccount” sits in this **cloud-MY accounting** cluster, not a single URL.

**Implication.** The cloud-MY GL (Bukku / Financio / QNE / AutoCount Cloud) is the **Xero-price** competitor for the same digital SME Lazuar wants. They already submit 01–14 from a ledger UI. Lazuar’s only differentiation is **owning the checkout event**. If the merchant’s invoices are typed into Bukku after the fact, Lazuar LHDN is unused capacity.

### 8. Xero + Malaysia e-invoice

**Who.** Global cloud GL. MY entity. MDEC-accredited Peppol Service Provider. e-Invoicing **included** in Starter / Standard / Premium (promo pricing on the initiative page still showing ~USD 14.50–37.50 then $29/$50/$75 — confirm at quote time). Accountants can register on behalf of clients.

**What the page actually says (fetched 16 Aug 2026).** A lot of **Peppol** (“send invoices between accounting systems via Peppol,” directory at `list.malaysiasmp.my`). Register at MyInvois with TIN; connect Xero. Self-billed guide exists (Caltrix 2026). Timeline copy on the initiative page is **stale** (RM 500k band, July 2025).

**Practical 2026 shape (partner blogs + Xero Central):** Xero is used as the **ledger**; MyInvois connection is a first-party or tightly partnered path (Invoici appears in setup guides). Peppol covers the “send to another Xero/Peppol inbox” story IRBM does not care about.

**What they have that Lazuar does not**

- Bank rec, GST/SST worksheets, accountant ecosystem, multi-currency as a daily tool.
- Peppol send/receive.
- A UI every bookkeeper already knows.

**What they do not have**

- Billplz/FPX checkout, WhatsApp dunning, productized LHDN XML templates as a **developer API**.
- POS consolidation QR (that is StoreHub/AutoCount).

**Implication.** Xero is the **CFO completion** ADR 021 explicitly **kept** (“Keep: Xero / Cloud Accounting Sync”). That sync is **not built**. Tracker **`LP-121`** is Wave **4**, **N**. Until it is built, every Xero merchant will treat Lazuar invoices as a second set of books — the fastest way to get a UUID rejected for duplicate `codeNumber` or wrong totals.

### 9. QuickBooks Online (Intuit) + Sovos

**Who.** QBO MY. e-invoice is a **Sovos** partnership (Sovos = global CTC/Peppol vendor). Unlimited e-invoicing on all plans (marketing). Individual + **consolidated** + status dashboard. Free 45-minute onboarding for paid plans, pitched at the 1 Jan 2026 mandate.

**Stale timeline on the same page:** still lists Phase 5 / sub-RM 1 m / 1 Jul 2026. Do not copy their dates.

**What they have that Lazuar does not**

- A global compliance vendor (Sovos) maintaining IRBM spec changes.
- Consolidated e-invoice as a **button**, not a 28th job the merchant cannot see.
- Intuit accountant channel.

**What they do not have**

- MY POS/FPX culture. MY SMEs who picked QBO are already “cloud accounting” people — overlapping Xero, not StoreHub.

**Implication.** Same as Xero: partner or export (`LP-121`), do not recreate. Sovos behind QBO is also why **Avalara/Stripe Tax** are not the MY e-invoice answer — Intuit had to **buy a CTC specialist**.

### 10. EasyStore (and StoreHub’s checkout cousin)

**Who.** MY-first commerce platform (online store + retail + marketplaces). 50,000-brand marketing claim. Direct competitor to a **hosted checkout**, unlike SQL.

**MyInvois motion (blog updated 27 May 2025; support article “e-Invoice Integration” dated 14 May 2026):**

- Appoint **EasyStore Commerce Sdn. Bhd.** as intermediary — they publish **TIN `C22494172000`**, **BRN `201201036069`** on the blog. That is how confident a real intermediary is.
- Install **E-Invoice Malaysia** app; fill MSIC / TIN / profile.
- **Free API submission**; “order details submitted to LHDN within 2 seconds.”
- Store customer TIN/IC; QR on online store, shopping app, **and POS orders**.
- Dashboard; optional “collect tax info, submit via external accounting.”
- Cancel **within 72 h auto-cancel + resubmit**; after 72 h debit/credit notes.

**What they have that Lazuar does not**

- A **live storefront + POS** where the buyer already typed a shipping address (half of UBL).
- Intermediary appointment as a **documented 6-click recipe** with legal identifiers.
- Classification **008/009** is their native world (e-commerce codes).
- Visible QR to the **end customer**.

**What they do not have**

- Subscription dunning / BYOK multi-gateway orchestration as a platform (Lazuar’s actual shipping CaaS).
- Self-billed affiliate engine as a first-class API.

**Implication.** EasyStore is the **honest comparison for ADR 021 Pillar 1+2** if the merchant is selling physical/digital goods online. They already do “compliance at the point of sale.” Lazuar’s XML engine is not a differentiator against EasyStore’s 2-second submit. Differentiation would have to be **payments + dunning + headless API**, not UBL.

StoreHub = till-first. EasyStore = cart-first. Lazuar = API-first. Only the third is unoccupied — and only if the API is **callable and documented**, which it is, while the **merchant UI is hidden**.

### 11. Dedicated e-invoice APIs / middleware (2026 field)

LHDN does not certify these. They sell “we speak UBL so you don’t.” JomeInvoice’s 22 Jul 2026 ranking (vendor-authored, but the *category* is real):

| Provider | Best-fit 2026 story | Notes |
|----------|---------------------|--------|
| **JomeInvoice** | MY-only middleware; Shopify / Woo / Loyverse / Salesplay / Cloudbeds connectors; CSV/SFTP/API for ERP | MySTI STI202501062; ISO 9001/20000-1/27001; claims 500+ enterprises / 1,000+ SMEs; inbound + TIN check + 72 h + consolidation + retry |
| **ClearTax (Defmacro)** | MDEC Peppol AP **and** MyInvois; ERP extract → UBL → UUID | Auto-upgrades cited against Guideline v4.6; enterprise onboarding; KL entity |
| **Pagero (Thomson Reuters)** | Multi-country network + MyInvois | Overkill for MY-only CaaS |
| **BDO e-Invoice Middleware** | Advisory-wrapped | You buy the firm, not a self-serve API |
| **QNE e-Integrator** | Excel template / batch for legacy | The “no API” path |
| **Awantec (AwanBiru)** | Bursa-listed local bridge | Newer in category |
| **MountainTop (SL Info)** | Single-ERP bridge | Narrow connectors |
| **Flick Network** | Standalone e-invoicing (directory listings) | Peppol-oriented |
| **InvoisPro** | Standalone e-invoicing (directory) | SME SKU |
| **Finexus AREMA** | Standalone / enterprise MY | Payments-adjacent local vendor |
| **i-invoice.my** | “Trusted middleware” + Peppol marketing | Local landing page live |
| **Daxonet** | Mid-layer, 53-field pitch | Local |
| **Sovos** | Behind QBO; global CTC | Not a MY SMB brand |
| **Taxilla / Covoro (GSTHero)** | Regional e-invoice vendors with MY pages | India-origin playbooks |

**Requested names not located as 2026 MY leaders**

| Requested | Result of 16 Aug 2026 search |
|-----------|------------------------------|
| **B2Binvoice** | No durable MY MyInvois product page. Do not invent one. |
| **Invoprint** | No durable MY MyInvois product page. |
| **Snapshot** | No durable MY MyInvois product page. |

If those brands existed in 2024 slideware, they did not survive as searchable 2026 products. The **category** they were meant to represent is the table above.

**What the category has that Lazuar’s API almost has**

- Pre-submission validation beyond XSD (Schematron / field matrix / TIN).
- **Inbound** purchase invoices + reject in 72 h.
- Connector catalogue (Shopify, SAP, Loyverse…).
- Guideline auto-update as a **sales promise**.
- Retry when MyInvois 500s (Lazuar has lease + backoff; not a merchant-visible dead-letter UI).
- Certifications (ISO 27001, MySTI) a procurement team can tick.

**What Lazuar has that most middleware does not**

- A **payments + ledger** in the same monolith (`InvoiceIssued`, `GatewayRefundCompleted`, `B2cConsolidationJob`).
- Idempotent submit + credit metering (`CreditAction.LhdnSubmit`).
- Dual Kiota SDKs and TypeSpec as a **product** (`LP-139` = **Y**).
- Intermediary mode already in the gateway.

**Implication.** If Lazuar sells **LHDN-as-API** to other SaaS (Aura, a Woo plugin, an agency ERP), the competitive set is JomeInvoice/ClearTax, not HitPay. Today the API exists and the **Ops invoicing UI is unrouted**, so the sellable surface is **curl + SDK**, which only a developer buyer will touch.

### 12. Global tax: Avalara and Stripe Tax — why they do not do MyInvois UBL

#### Stripe Tax

Stripe’s own supported-countries table (fetched 16 Aug 2026):

| MY row | Value |
|--------|--------|
| Product type | **Digital products only** |
| Tax type | **Service tax** |
| Your **business location** | **Not supported** |
| Your **customer location** | Supported |

Meaning: Stripe Tax can calculate **Malaysian service tax on digital supplies to a MY customer** for a business **not based in Malaysia**. It will **not** be the tax engine for a MY-incorporated merchant’s full SST/e-invoice life. It does **not** emit UBL 2.1, does **not** call `documentsubmissions`, does **not** return an IRBM UUID.

Separate Stripe help articles (2025 vintage):

- Stripe asked MY **connected accounts** to fill **TIN, SST ID, BRN** so **Stripe can e-invoice Stripe’s own fees** to those accounts. That is Stripe as **supplier**, not Stripe as the merchant’s MyInvois intermediary.
- “Updated our systems to support e-invoicing for businesses in Malaysia by January 2025” in that article is about **account tax identity**, not a merchant UBL API.

ADR 021’s line *“Global payment processors (Stripe) do not generate local XML tax invoices”* is **still true** for the merchant’s guest tickets. Tracker **`LP-120`** is **R** (refuse Avalara-class global tax as a product).

#### Avalara

Avalara is a global determination + (in some countries) e-invoicing network. They **write about** MyInvois (2024 APAC blog; 2026 Stripe partnership posts about UBL in **other** countries). AvaTax on Stripe Marketplace calculates tax on Checkout/Billing; the 2026 “Avalara for Stripe” e-invoicing story is **Peppol/UBL in the countries that connector lists**, not a documented MY MyInvois submit.

Malaysia is a **government-gated CTC** with its own OAuth, intermediary model, and JSON-UBL dialect. Avalara’s money is determination (what rate?) + multi-country transport. IRBM wants **their** UUID on **their** schema. Until Avalara publishes a MY Access Point / MyInvois connector as a SKU a KL SME can buy, they are **not** a StoreHub/SQL competitor.

**Why they will not casually “add MyInvois UBL”**

1. **Not VAT.** Determination models are VAT/GST/sales-tax. MY is **SST (multi-type 01–05) + income-tax CTC**. Exemption lists (beauty services stayed out of the 2025 SST expansion) are political, not a rate table.
2. **Clearance, not post-audit.** The invoice is not legal until IRBM says so. That is a **stateful** submit/poll/cancel machine, not a calculate-and-print.
3. **Intermediary appointment + MY digital cert ecosystem** (Pos Digicert et al.), not a global One Certificate.
4. **JSON-UBL with `[{"_": value}]` arrays** and a proprietary JSON signature — not PEPPOL BIS XML they already generate for Europe.
5. **Bahasa, MSIC, state codes 01–17, general TINs, 004 consolidation, 72-hour reject** — local product management, not a content update.
6. **Economics.** The MY SME pays SQL RM 1,499 once or StoreHub RM 99/mo. Avalara’s ACV is a different planet.

**Implication.** ADR 021 is right that Stripe/Avalara leave a hole. The hole is **already filled** by SQL/AutoCount/StoreHub/EasyStore/JomeInvoice. Lazuar is not uniquely qualified by “Stripe won’t do it.” Lazuar is uniquely qualified only if **checkout ownership + MyInvois** is one motion — which ADR 023 currently refuses to show.

### 13. Players that are not MyInvois competitors (so we stop comparing them)

| Player | What they issue | MyInvois? |
|--------|-----------------|-----------|
| HitPay / Xendit / Billplz / CHIP | Payment receipts, sometimes commercial invoices | **No** (HitPay invoices are not IRBM UUIDs) |
| Stripe Payment Links / Billing | Stripe invoices; Stripe Tax on digital SST for non-MY businesses | **No** merchant UBL |
| Paddle / Polar / Lemon Squeezy | MoR invoices in *their* name | **Breaks** LHDN (the Malaysian seller must issue). ADR 019/021 refused MoR for this reason among others |
| Chargebee / Recurly / Maxio / Lago | Subscription invoices, US/EU tax | **No** MyInvois |
| Fresha / Booksy / Boulevard / salon OS | Commercial invoices | **No** |
| Square / Shopify (global) | Receipts; MY Shopify needs an app (Sufio et al.) | **Not native** |
| Aura booking PDF | Appointment confirmation | **No** — Aura is a Hub customer |

Paddle-as-MoR is hostile to MyInvois because the **seller of record** on the tax invoice would be Paddle, not the Malaysian merchant. That is a hard reason ADR 021 exists.

---

## Lazuar Lhdn module audit

### What the module is

A **compliance gateway** inside the .NET 10 modular monolith. It turns a `SubmitDocumentRequestDto` (or a Billing/Payments integration event) into UBL 2.1 XML via Scriban, XSD-preflights it, SHA-256-hashes the LF-normalized UTF-8, persists a `TaxDocument`, and lets workers submit + poll MyInvois.

It is **not** a general ledger. It is **not** a Peppol AP. It is **not** currently a merchant product.

### Public API (TypeSpec = server, 16 Aug 2026)

`packages/api-spec/modules/lhdn/routes.tsp` + `Infrastructure/Endpoints/*.cs`:

| Method | Path | Auth | Implemented |
|--------|------|------|-------------|
| POST | `/lhdn/taxpayer/validate` | `lhdn.documents:read` (write satisfies read) | **Yes** — cached 30d valid / 7d invalid; HMAC of id value |
| POST | `/lhdn/documents` | `lhdn.documents:write` + **Idempotency-Key required** | **Yes** — 200 `accepted_for_processing` |
| GET | `/lhdn/documents/{internalId}` | read | **Yes** — status, UUID, LongId, QR, test flag, timestamps |
| POST | `/lhdn/documents/{internalId}/cancel` | write | **Yes** — 72 h domain rule + gateway |
| GET/PUT | `/lhdn/workspaces/{id}/lhdn-config` | OrgAdmin JWT | **Yes** — TIN/BRN/MSIC/env/credentials/legal address; secrets masked |
| PUT | `/lhdn/workspaces/{id}/lhdn-certificate` | OrgAdmin | **Yes** — P12 + passphrase into vault |
| POST/GET/DELETE | `/lhdn/api-keys` | OrgAdmin | **Façade** over One `ApiCredentials` |
| POST/GET/DELETE | `/lhdn/webhooks` | (admin) | Registry path retired toward **One** durable dispatcher |

**Missing vs MyInvois SDK / vs competitors**

- List/search documents (paginated).
- Buyer **reject**.
- Get recent / inbound documents (MyInvois 31-day recent API).
- Download raw XML / PDF.
- Batch submit (always a one-element `documents[]`).
- Notification webhook **from** LHDN (status is poll-only).
- v1.1 sign.
- Schematron.

### Document pipeline (happy path)

```
POST /lhdn/documents + Idempotency-Key
  SubmitTaxDocumentCommand
    credit check (live only, ICreditCostService / LhdnSubmit)
    strategy.Generate (Scriban) → LF normalize
    UblValidatorService XSD
    SHA-256 hex
    TaxDocument PENDING + IdempotencyLog
    DeductTenantCreditCommand (failure logged, doc already saved)
  → 200 accepted_for_processing

LhdnSubmissionJob
  FOR UPDATE SKIP LOCKED + ClaimProcessingLease
  format=XML, documentHash, codeNumber, document=base64
  onbehalfof if IntermediaryMode
  → SUBMITTED + LhdnDocumentSubmittedIntegrationEvent

LhdnStatusPollingJob
  SKIP LOCKED + lease
  VALID → MarkAsValid(longId) + Validated event (args: org, internalId, uuid, "VALID", qrLink)
        + DispatchExternalWebhookCommand → One outbox (invoice.valid)
  INVALID → MarkAsInvalid + invoice.invalid webhook
  else ScheduleNextPoll (3^min(n,10) seconds)
```

This is a **real** async clearance loop. Rate limiters are in-process token buckets (login 12, submit 100, poll 300, TIN 60, cancel 12 / min / client id) — not cluster-redistributed.

### Strategy matrix vs official types

| Official | Lazuar strategy | Template | Notes |
|----------|-----------------|----------|-------|
| 01 B2B | `StandardInvoiceStrategy` | `StandardInvoice.xml` | Type code hardcoded `01` |
| 01 B2C consolidated | `ConsolidatedInvoiceStrategy` | `ConsolidatedInvoice.xml` | Triggered by empty TIN or `EI00000000010` |
| 02 / 03 / 04 | `CreditNoteStrategy` | `CreditNote.xml` | Dynamic `doc_type_code`; billing reference UUID |
| 11 | `SelfBilledInvoiceStrategy` | `SelfBilledInvoice.xml` | Entity swap |
| 12 / 13 / 14 | `SelfBilledCreditNoteStrategy` | `SelfBilledCreditNote.xml` | Entity swap |
| Peppol / PINT | — | — | Not a type; not implemented |
| Reject | — | — | Not implemented |

### Templates: what is bound vs what is still sample HQ

`ViewModelMapper` now binds tenant **legal name, TIN, ID, MSIC, address line 1, city, postal, state, country** from `LhdnTenantConfig`. Phone is still `+60000000000`; email empty.

**`StandardInvoice.xml` (and the credit/self-billed siblings) still hardcode supplier postal address** as Lot 66 / Bangunan Merdeka / Persiaran Jaya / KL 50480 / state 14 — the **official sample address** from IRBM’s own XML. Buyer address is bound. Supplier address in the template is **not** using `supplier.city` / `supplier.address_line1`.

That is a **compliance defect**, not a style nit. IRBM validates address fields. Shipping “Bangunan Merdeka” on every live invoice is how a demo UUID becomes a production invalid.

Also hardcoded in live templates:

- `DocumentCurrencyCode` / `TaxCurrencyCode` = **MYR** (foreign-currency sample exists only under `docs/xml/`).
- Invoice period description `Monthly`.
- No `PaymentMeans`, `PrepaidPayment`, SST/TTX party IDs, customs/FTA additional document references (all present in `docs/xml/README.md` official-shaped example).
- v1.1 block is an empty `UBLExtensions` + `<!-- SIGNATURE_PLACEHOLDER -->` — **not** ADR 010’s inject-after-hash workflow.

`docs/xml/` is a **reference corpus** (invoice, consolidated, foreign currency, multi-line, credit, debit, refund, four self-billed, `signature/one-doc-signed.xml`). It is richer than the Scriban templates. The signed sample is a **static teaching file**, not produced by `CertificateVaultService`.

### Event-driven intake (the CaaS loop)

| Event | Handler | Production-ready? |
|-------|---------|-------------------|
| `InvoiceIssuedIntegrationEvent` | Builds DTO with buyer **“Resolved via CRM”**, TIN **`C1234567890`**, BRN `202001012345`, address Line 1 / KL, one line “Standard B2B Invoice”, tax type **06**, classification **022** | **No — stub. Must not hit PROD MyInvois.** |
| `ConsolidatedInvoiceIssuedIntegrationEvent` | General public TIN, `NA`, classification from event then forced `004` | **Shape is correct.** Depends on ledger quality. |
| `GatewayRefundCompletedIntegrationEvent` | If original VALID and ≤72 h → cancel; else build CN **bypassing** `SubmitTaxDocumentCommand` (no XSD path parity, no credit deduct, no idempotency). Buyer again **stub**. Tax type 06, class 022 | **Half-right legally, stub-wrong on party data, pipeline-wrong on CN.** |

Until `InvoiceIssued` resolves a real CRM/checkout party (TIN validated), **Pillar 2 (B2B at checkout) is fiction.** ADR 023 hid the TIN fields that would have fed it.

### B2C consolidation job

`B2cConsolidationJob`:

- Time zone: Malaysia (with a resolver, not a naive `+8` only).
- Schedule: next **28th 02:00 MYT**, plus **catch-up of all closed months** with pending B2C (24-month cap).
- Eligibility: `CustomerType == B2C` and (`ConsolidationStatus == Pending` or legacy null + `B2cReceipt` / null LHDN status).
- Idempotency: `TaxInvoiceId == B2C-CONS-{yyyyMM}-{orgGuidN}`.
- Groups revenue/tax/refund lines by `TaxTypeCode` + `MsicCode`.
- Description always `"Consolidated B2C Sales"`.

Gaps vs 2026 rules:

- No **RM 10,000** individual split.
- No “buyer requested e-invoice this month” exclusion (and checkout no longer collects that request).
- Submits on the **28th**, not “after month-end within 7 days” — usually OK; not aligned to IRBM’s wording.
- Classification field on the DTO is stuffed with **MsicCode** then overwritten to 004 — naming confusion waiting to happen.
- Merchant cannot see or retry a failed month in UI (hidden).

Tracker **`LP-114`** is **B**. Do not mark **Y** until RM10k + request-out + visible month status exist.

### TIN validation

`TaxpayerValidationService` + gateway `ValidateTaxpayerTin`:

- Normalizes TIN/id type.
- HMAC-SHA256 of id value with `Lhdn:TinHashSalt` (default string is a **local placeholder** — must be set in prod).
- Cache hit if unexpired.
- Else OAuth + MyInvois validate; cache 30d / 7d.
- Returns `is_valid`, `tin`, `taxpayer_name`.

This is the **right primitive** for ADR 021 Pillar 2. It is **not wired to checkout** (TIN fields `[MVP-HIDE]`). AutoCount/SQL wrap the same API in “type BRN, get name.” Lazuar exposes the raw POST to API-key holders. Tracker **`LP-112`** is **B**.

### Cancel / QR / webhooks / credits

- Cancel: domain 72 h + gateway; publishes `LhdnDocumentCancelledIntegrationEvent`. **No** customer webhook for cancel (only valid/invalid).
- QR: string on valid webhook only.
- Webhooks: One durable path (`invoice.valid` / `invoice.invalid`), Standard Webhooks-style `t=,v1=` HMAC. R43 retired fire-and-forget. **Good.**
- Credits: single deduct on accept with idempotency key `lhdn:{key}`. Older double-charge via submitted-event is **intended closed** (do not reintroduce). Test keys skip credits. Tracker **`LP-005`** is the credit wallet row.

### Security posture

| Secret | State |
|--------|--------|
| MyInvois client secret | Encrypted via `ISecretVault` on update; `DecryptOrPlaintext` at token time; **legacy plaintext rows** until re-saved |
| PFX bytes | Encrypted in vault **now**; legacy raw base64 fallback |
| PFX password | Encrypted |
| TIN id values in cache | HMAC, not plaintext |
| Master key | `Kms:MasterKey` or `Jwt:Secret` padded to 32 bytes — **not HSM** |
| Sandbox scripts | `00_provision.sh` historically had hardcoded secrets (gap note); treat as rotate-and-env |

### Tests

Module tests exist for workers (claim/lease), B2C consolidation (catch-up, idempotent, excludes B2B), credit single-path. Sandbox E2E remains **opt-in**. Golden-master strategy tests were historically commented — do not claim they are green without running them. Architecture tests carry `lhdn-golden-master.json`.

### SDKs

| Package | Version | Notes |
|---------|---------|-------|
| `@lazuar/lhdn-sdk` | 0.1.0 | `initLhdnClient`; Bearer prefix normalized; Kiota **1.0.0-preview.20** |
| `Lazuar.Lhdn.Sdk` | 0.1.0 | `LhdnClientFactory`; **auto Idempotency-Key GUID on POST** (good for retries, bad if caller wanted a semantic key and forgot to set one) |

Both are **integrator** artefacts. They do not help a salon owner or a WhatsApp seller. Tracker **`LP-139`** = **Y** (the SDK exists). That is not the same as a sold LHDN product.

### UI lobotomy (ADR 023) — exact cuts

**Ops (`lazuar-ops/src/App.tsx`):**

```text
[MVP-HIDE] Phase D.3
  /workspace/billing-profile
  /invoicing/quotes
  /invoicing/tax-invoices
  /invoicing/credit-notes
  /ops/chat
```

Pages **still exist** (`TaxInvoicesPage` shows LHDN status badges; `TaxInvoiceDetailPanel` cancels via `POST /lhdn/documents/{id}/cancel` when status `VALID`). Sidebar does not link them. Vite tree-shakes the islands.

**Commerce product form:** “Require Company Name & Tax ID (LHDN B2B)” forced `false`.

**Portal checkout:** TIN, company name, tax id **undefined**; fields commented. Quote route `/pay/[sessionId]` **notFound()**. Buyer portal download tax invoice hidden.

**Still visible:** API keys page (LHDN scope preset), billing credits copy that **names** “automated LHDN tax submissions,” workspace provision includes `"LHDN"`. So the product **talks** about LHDN in developer/billing chrome while **hiding** the only screens that would make it real for a merchant.

This is exactly why [`00-evaluation.md`](./00-evaluation.md) says: *“Moat is inventory, not product. Wave 2 is turning inventory into a sale.”*

---

## Moat honesty (backend vs sellable)

### What ADR 021 claimed

> Writing W3C Canonicalized XML, managing X.509 cryptography, and balancing double-entry ledgers is incredibly difficult. AI wrappers cannot easily clone this.  
> If a business cancels Lazuar, their cash flow stops, and they immediately violate government tax laws.

### What is actually true on 16 Aug 2026

| Claim | Backend | Sellable |
|-------|---------|----------|
| UBL 2.1 XML generation | **Yes** (Scriban + XSD) | Only via API; templates still emit sample HQ address |
| X.509 / XAdES v1.1 | Vault stores cert; **does not sign**; XML-DSig path known-broken; JSON-sign pivot **not done** | **No** (`LP-117` = N) |
| Double-entry ledger | Billing module yes | Ledger UI exists; tax identity UI hidden |
| B2C monthly consolidation | Job + event + general TIN | **No merchant control**; no RM10k split (`LP-114` = B) |
| B2B TIN at checkout | TIN API exists | **Fields hidden**; handler uses stub TIN (`LP-112` = B) |
| Escrow + e-sign at B2B checkout | ADR 021 prose | **Not this module** |
| Export zero-rate auto-class | Intent | **No** (`EI00000000020` / `032` / `E` not auto; `LP-119` = N) |
| Negative churn “un-fireable” | Only if production UUIDs are live | **Zero merchants on the hidden UI** |
| Replace a RM 2,000/mo data-entry shop | — | SQL+bookkeeper is RM hundreds and **already submits** |

The **engineering** is a credible v1.0 outbound gateway — better than a weekend wrapper, worse than AutoCount AIP / JomeInvoice. The **moat** is not the XML. Every middleware listed above generates UBL. The moat, if any, is **owning the payment event** so the e-invoice is not re-typed.

That moat is **disconnected**:

```
Payment completes
    → Billing ledger (silent)
    → InvoiceIssued (STUB BUYER)     ← would pollute MyInvois if enabled
    → Lhdn workers                   ← real
    → webhook invoice.valid          ← real, developer-only
    → Ops tax invoice screen         ← UNROUTED
    → Portal QR / download           ← HIDDEN
    → Checkout TIN                   ← HIDDEN
```

ADR 023 says this is temporary and reactivation is comment-removal. That is true for **routes**. It is **false** for stubs, unsigned v1.1, hardcoded Bangunan Merdeka, missing SST IDs, missing reject/inbound, missing RM10k rule, and missing Xero sync.

### Who would buy what, honestly

| Buyer | What they should buy | Who they buy it from today | Lazuar fit |
|-------|----------------------|----------------------------|------------|
| Exempt shop, 1 corporate request/month | MyInvois Portal | Free | **None** (honest export) |
| Retail / F&B till | StoreHub / AutoCount POS / SQL POS | Included e-invoice | Do not fight (`19` refuse POS) |
| Bookkeeper-led SME | SQL / AutoCount / UBS / Xero / QBO | Native submit + TIN DB | Downstream export only (`LP-121`) |
| Digital seller on EasyStore | EasyStore app | Free API submit | Already solved |
| Headless / custom checkout, high B2C + some B2B | **Middleware API or CaaS** | JomeInvoice / ClearTax / custom | **This is the only seat** |
| Other SaaS (Aura, agency ERP) | White-label submit/poll/TIN | JomeInvoice, or Lazuar SDK | **API is real; productize it** |
| Multi-country enterprise | Peppol + MyInvois | Pagero / ClearTax / Sovos | Walk away |
| Indie global SaaS who wanted Paddle tax | Paddle / Polar / Stripe Tax | MoR | **Refuse** — breaks MyInvois seller-of-record |

### What “un-hide Wave 2 / Phase D.3” would still owe

1. Stop stub buyers. Resolve checkout/CRM party or **do not submit**.
2. Bind supplier address from config; add SST/TTX IDs (`LP-118`, `LP-122`).
3. Implement v1.1 **JSON** sign (the path `docs/lhdn` already chose) or stay on v1.0 with an honest banner (`LP-117`).
4. RM 10,000 split + “buyer requested individual” flag on the ledger (`LP-114`).
5. TIN field on checkout **only** when product requires B2B; validate live; cache (`LP-112`).
6. Show UUID + QR + cancel on Ops **and** buyer portal (`LP-113`, `LP-106`).
7. Credit note path through `SubmitTaxDocumentCommand`.
8. Foreign buyer TIN `EI00000000020` + tax `E`/`06` + classification policy for export (`LP-119`).
9. Decide intermediary legal entity (EasyStore publishes TIN/BRN; Lazuar must too if `IntermediaryMode` is the SKU).
10. Do **not** tell Aura tenants this is included in their salon Plan. It is a Pay product. Do not add LHDN scopes to Aura Connect keys.

Until those land, calling LHDN a moat is **internal mythology**. The honest public sentence is:

> Lazuar can submit and poll MyInvois documents from a headless API. Merchant invoicing screens are not shipped. Use SQL/AutoCount/MyInvois Portal if you need e-invoices today.

[`00-evaluation.md`](./00-evaluation.md) Wave 0 already requires README/marketing to mark LHDN as **roadmap**. Do not contradict that.

---

## Feature tables

Legend: **Y** = sold/used · **P** = partial/workaround · **N** = not a job · **—** = n/a · **B** = backend / hidden (Lazuar only)  
**Lazuar** column is **backend / sellable** (`B` / `S`).

### Table A — Document types and CTC verbs

| Capability | Portal | StoreHub | AutoCount | SQL | UBS | Xero | QBO+Sovos | EasyStore | Jome/ClearTax | Stripe Tax | Avalara | Lazuar B | Lazuar S |
|------------|:------:|:--------:|:---------:|:---:|:---:|:----:|:---------:|:---------:|:-------------:|:----------:|:-------:|:--------:|:--------:|
| 01 Invoice | Y | Y | Y | Y | Y | Y | Y | Y | Y | N | N | Y | N |
| 02 Credit note | Y | P | Y | Y | Y | Y | Y | Y | Y | N | N | Y | N |
| 03 Debit note | Y | P | Y | Y | P | P | P | P | Y | N | N | Y | N |
| 04 Refund note | Y | P | Y | Y | P | P | P | P | Y | N | N | Y | N |
| 11–14 Self-billed | Y | N/P | Y | Y | P | P | P | N | Y | N | N | Y | N |
| Submit XML | Y | Y | Y | Y | Y | P | Y | Y | Y | N | N | Y | P |
| Submit JSON | Y | ? | Y | ? | ? | ? | Y | ? | Y | N | N | N | N |
| v1.1 digital sign | Y | Y | Y | Y | P | Y | Y | Y | Y | N | N | N | N |
| Status poll / dashboard | Y | Y | Y | Y | P | Y | Y | Y | Y | — | — | Y | N |
| 72 h cancel | Y | Y | Y | Y | P | P | P | Y | Y | — | — | Y | N |
| 72 h buyer reject | Y | N | Y | Y | P | P | P | N | Y | — | — | N | N |
| Inbound supplier invoices | Y | N | Y | Y | P | Y | P | N | Y | — | — | N | N |
| QR share | Y | Y | Y | Y | P | P | P | Y | Y | — | — | Y | N |
| TIN validate | Y | P | Y | Y | P | P | P | Y | Y | — | — | Y | N |
| TIN master (1.4M) | N | N | P | Y | N | N | N | N | P | — | — | N | N |
| Intermediary mode | — | Y | Y | Y | Y | Y | Y | Y | Y | N | N | Y | N |
| Peppol AP | N | N | N | N | N | Y | Y (Sovos) | N | Y (ClearTax) | N | P | N | N |

Lazuar **S** is N wherever the only surface is a hidden route or a webhook a merchant cannot see. This is why tracker cells are **B**, not **Y**.

### Table B — B2B / B2C / tax classification

| Capability | Portal | StoreHub | AutoCount | SQL | EasyStore | Middleware | Lazuar B | Lazuar S |
|------------|:------:|:--------:|:---------:|:---:|:---------:|:----------:|:--------:|:--------:|
| Individual B2B with real TIN | Y | P | Y | Y | Y | Y | P (API yes; event stub) | N |
| TIN at checkout / till | Y (form) | QR request | QR request | Counter | Checkout field | Connector | Hidden | N |
| B2C consolidation 004 + EI00000000010 | Y | Y | Y | Y | Y | Y | Y | N |
| Month-end +7 day rule | Human | Auto | Auto | Auto | Auto | Auto | 28th job | N |
| RM 10,000 cannot consolidate | Human | ? | ? | ? | ? | Y (Jome markets it) | N | N |
| Foreign buyer EI00000000020 | Y | P | Y | Y | P | Y | N auto | N |
| Tax types 01–06 + E | Y | P | Y | Y | P | Y | Y (DTO) | N |
| Tax type “08 export” | **Does not exist** | — | — | — | — | — | — | — |
| Classification 008 e-commerce | Y | P | Y | Y | Y | Y | Caller-supplied | N |
| Classification 032 foreign income | Y | P | Y | Y | P | Y | N auto | N |
| SST / TTX party IDs | Y | P | Y | Y | P | Y | N in templates | N |
| Multi-currency | Y | P | Y | Y | P | Y | Sample only | N |
| Export zero-rate policy | Human | N | P | P | P | P | Intent only | N |

### Table C — Product shape vs ADR 021 pillars

| Pillar | Competitor who already ships it | Lazuar backend | Lazuar sellable |
|--------|----------------------------------|----------------|-----------------|
| 1 Low-ticket B2C + monthly consolidated | StoreHub, EasyStore, AutoCount POS | Job + handler + 004 | Hidden |
| 2 High-ticket B2B + TIN before pay + QR | SQL/AutoCount (after the fact); EasyStore (at cart) | TIN API + 01 strategy | TIN UI hidden; stub buyer |
| 3 Cross-border + export codes | Portal + GL (manual); middleware (partial) | Foreign XML sample | No |
| Checkout ownership | EasyStore, StoreHub, HitPay, Billplz links | Payments module | **This is the shipped CaaS** |
| Un-fireable tax dependency | SQL/AutoCount once live | Possible | **Not live** |

### Table D — Pay vs rails vs Aura (do not collapse)

| Job | Lazuar Pay | Billplz/CHIP | Stripe | Aura (customer) |
|-----|------------|--------------|--------|-----------------|
| SST rate on a ticket | Ledger tax lines if fed (`LP-118` P) | Receipt | Stripe Tax (limited) | Storefront toggle only |
| MyInvois submit | Backend yes, UI no (`LP-110` B) | No | No merchant UBL | **None** — must not add LHDN scopes |
| Guest checkout | Hosted portal + BYOK | Their hosted page | Payment Links | `/book` + Hub Connect |
| LHDN scopes on Connect key | Separate integrator key | — | — | **Must stay off** |

---

## Tracker IDs

### Already official (`00-checklist-tracker.md`)

These IDs **already exist**. This file does not invent a second taxonomy. Flip a cell only when code changes.

| ID | Feature | Wave | Lazuar cell | This research |
|----|---------|------|-------------|---------------|
| **LP-005** | Prepaid utility credits (LHDN / WhatsApp) | 1 | P | Charge path exists; do not sell “credits for live e-invoice” while UI is hidden. |
| **LP-106** | Buyer download of documents | 2 | B | Portal download is `[MVP-HIDE]`. |
| **LP-110** | MyInvois submit (UBL 2.1) | 2 | B | Real XML submit. Stay **B** until merchant nav + no stub buyers. |
| **LP-111** | Status poll VALID / INVALID | 2 | B | Poller + `invoice.valid`/`invalid` webhooks. No merchant dashboard. |
| **LP-112** | TIN / taxpayer validation | 2 | B | API + cache. Checkout fields hidden. |
| **LP-113** | LHDN QR on validated invoice | 2 | B | `qr_link` on webhook only. |
| **LP-114** | B2C monthly consolidation | 2 | B | 28th job; no RM10k split; no request-out. |
| **LP-115** | Self-billed documents (11–14) | 4 | B | Strategies exist; no scenario pack / UI. |
| **LP-116** | Cancel / reject within IRBM rules | 2 | B | **Cancel only.** Reject + inbound = N. Cell oversells reject. |
| **LP-117** | XAdES V1.1 signing | 2 | N | Placeholder + vault; no sign path. Prefer JSON sign. |
| **LP-118** | SST line codes | 2 | P | DTO has 01–06+E; templates omit SST/TTX party IDs. |
| **LP-119** | Export zero-rate (foreign buyer) | 4 | N | No `EI00000000020` / `032` / `E` auto. **Not tax type 08.** |
| **LP-120** | Stripe Tax / Avalara-class global tax | R | R | Confirmed refuse. They do not do MyInvois UBL. |
| **LP-121** | Xero / QuickBooks sync | 4 | N | ADR 021 “keep”; not built. |
| **LP-122** | Merchant legal profile (TIN, BRN, address) | 2 | B | Config API yes; billing-profile route hidden; templates ignore supplier address. |
| **LP-123** | PDPA buyer-data deletion / anonymize | 1 | P | TIN cache HMAC is good; not a deletion program. |
| **LP-139** | LHDN SDK (npm + NuGet) | — | Y | `@lazuar/lhdn-sdk` + `Lazuar.Lhdn.Sdk` 0.1.0. |

`00-checklist-tracker.md` already notes: StoreHub / AutoCount / Xero / MyInvois portal would score **Y** on `LP-110`–`114` and **N** on checkout/dunning. They are the compliance-only column we did not add — this file is that column, written out.

### Residual checklist mapped to official IDs (do not mint a parallel family)

Finer work items belong **under** the `LP-*` rows, not as a second official family. Use these as Wave 2 implementation notes:

| Residual | Belongs under | Why it is not optional |
|----------|---------------|------------------------|
| Kill stub buyers on `InvoiceIssued` / refund CN | `LP-110` | P0. Stub TIN `C1234567890` must not hit PROD. |
| Bind supplier address + SST/TTX in templates | `LP-118`, `LP-122` | Templates still emit Bangunan Merdeka. |
| v1.1 JSON sign (or documented v1.0-only banner) | `LP-117` | XML-DSig known-broken (`docs/lhdn/000`). |
| Checkout TIN + live validate | `LP-112` | Without this Pillar 2 is fake. |
| Visible UUID + QR + cancel in Ops | `LP-110`–`113`, `LP-116` | Pages exist, unrouted. |
| Buyer portal download validated e-invoice | `LP-106`, `LP-113` | Hidden. |
| Consolidation respects RM 10,000 + request-out | `LP-114` | 2026 rule; job does not implement it. |
| Credit/debit/refund always via submit pipeline | `LP-110`, `LP-116` | Refund CN bypasses XSD/credits/idempotency. |
| Foreign / export policy | `LP-119` | Demand-gated Wave 4. |
| Self-billed scenario pack | `LP-115` | Wave 4. |
| Inbound + reject (72 h) | `LP-116` | Only if AP clerks appear; CaaS can stay outbound-only. |
| Peppol AP / MDEC | — | **Do not add.** Not Wave 2. |
| Publish intermediary legal identifiers | `LP-122` | EasyStore publishes TIN/BRN; we must if we invite appointments. |
| List documents + dead-letter UI | `LP-111` | Agent query only today. |
| Aura deep-link “issue this booking in Pay” | `LP-143` + `LP-110` | After Pay UI exists. Do not build MyInvois inside Aura. |
| Schematron / totals validation | `LP-110` | XSD only today. |
| Do **not** add LHDN scopes to Aura Connect keys | standing lock | Already a constraint. |

**Never / trap (already R or refuse-list)**

- Building MyInvois inside Aura to match StoreHub.
- Becoming SQL Account (TIN database of 1.4M, JE-to-e-invoice, dealer channel).
- Becoming a Peppol Access Point to “look enterprise.”
- Calling classification `008` “export zero-rate.”
- Marketing Stripe Tax as MyInvois (`LP-120` = R).
- Becoming MoR (Paddle) — breaks seller-of-record on the e-invoice (`LP-002` = R).

### Verdict for the tracker

- **`LP-110`–`LP-116`, `LP-122`:** keep **B**, Wave **2**. Un-hide is a product program, not a comment-removal.
- **Correctness before un-hide:** stub buyers, Bangunan Merdeka, unsigned-vs-honest-v1.0. Un-hiding UI on top of those creates **invalid UUIDs** — worse than staying hidden.
- **`LP-117`:** stay **N** until JSON sign ships. Do not market “digitally signed.”
- **`LP-119` / `LP-115` / `LP-121`:** stay Wave **4**.
- **`LP-120`:** stay **R**.
- **Competitive frame to write on those rows:** opponents are **Portal (free), SQL/AutoCount (accountant), StoreHub/EasyStore (POS/cart), Jome/ClearTax (API)** — not HitPay, not Fresha, not Stripe Tax.

---

## Sources

### Official

- IRBM e-Invoice implementation timeline (updated 7 Dec 2025): https://www.hasil.gov.my/en/e-invois/pelaksanaan-e-invois-di-malaysia/garis-masa-pelaksanaan-e-invois/
- MyInvois SDK home: https://sdk.myinvois.hasil.gov.my/
- e-Invoice types: https://sdk.myinvois.hasil.gov.my/codes/e-invoice-types/
- Tax types: https://sdk.myinvois.hasil.gov.my/codes/tax-types/
- Classification codes: https://sdk.myinvois.hasil.gov.my/codes/classification-codes/
- Submit Documents API: https://sdk.myinvois.hasil.gov.my/einvoicingapi/02-submit-documents/
- e-Invoice APIs index (validate TIN, cancel, reject, recent, details, search): https://sdk.myinvois.hasil.gov.my/einvoicingapi/
- MDEC Peppol Service Providers: https://mdec.my/programmes/national-e-invoicing-initiative/peppol-service-providers
- OpenPeppol Malaysia profile: https://peppol.org/learn-more/country-profiles/malaysia/

### Secondary (dated; IRBM wins on conflict)

- ClearTax “e-Invoicing in Malaysia 2026” (updated 17 Jul 2026): https://www.cleartax.com/my/en/e-invoicing-malaysia
- JomeInvoice complete guide (v4.6 / 22 Apr 2026 note): https://jomeinvoice.my/article/lhdn-e-invoice-malaysia-2026-complete-guide/
- JomeInvoice middleware ranking (22 Jul 2026): https://jomeinvoice.my/article/best-e-invoice-middleware-malaysia/
- BDO Malaysia e-invoicing guide (updated 11 May 2026): https://www.bdo.my/en-gb/insights/featured-insights/guide-to-e-invoicing-in-malaysia
- Airwallex Peppol vs MyInvois (2026): https://www.airwallex.com/en-my/blog/what-is-peppol-malaysia

### Competitors

- StoreHub e-invoicing tips (page last-seen 14 Aug 2026): https://www.storehub.com/blog/transitioning-einvoicing-tips-malaysian-businesses
- AutoCount AIP: https://www.autocountsoft.com/autocount-einvoice-solution-malaysia_Why_AC.html
- SQL Account e-invoice: https://www.sql.com.my/e-invoice/ and https://docs.sql.com.my/sqlacc/usage/myinvois/e-invoice-operation
- Access UBS e-invoicing: https://www.theaccessgroup.com/en-my/finance/software/invoicing/einvoicing/
- Xero MY initiative: https://www.xero.com/my/initiative/e-invoicing-malaysia/
- QuickBooks MY + Sovos: https://quickbooks.intuit.com/my/e-invoicing/
- EasyStore guide: https://blog.easystore.co/en-us/malaysia-einvoice-easystore-guide
- EasyStore support: https://support.easystore.co/en/article/e-invoice-integration-ygul8u/
- Stripe Tax countries (MY row): https://docs.stripe.com/tax/supported-countries
- Stripe MY e-invoicing identity (account fees, not merchant UBL): https://support.stripe.com/questions/understanding-e-invoicing-requirements-for-malaysia
- Avalara MY e-invoicing note: https://www.avalara.com/blog/en/apac/2024/07/malaysia-e-invoicing-updates.html
- einvoicingmalaysia.com vendor directory (secondary): https://einvoicingmalaysia.com/myinvois-api

### Lazuar (this repo)

- ADRs 010, 021, 023; `Modules/Lhdn/**`; `packages/lhdn-sdk-*`; `packages/api-spec/modules/lhdn/**`; `docs/xml/**`; `docs/lhdn/**`; `scripts/lhdn_sandbox/**`; Ops/Portal `[MVP-HIDE]` call sites listed in Absolute paths.
- This program: `plans/007-feats/00-evaluation.md`, `00-checklist-tracker.md`, `README.md`.

---

*End of uncondensed analysis. Do not summarize this file into the tracker; flip `LP-*` cells only when code changes. Keep `LP-110`–`116` as **B** until Wave 2 is actually sold.*
