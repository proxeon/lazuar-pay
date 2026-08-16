# W2-LP-118 — SST line codes

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-118`. Tracker: *SST line codes* — Lazuar **P**. Alias reserved `LP-TAX-012`.  
**Not this ID:** Stripe Tax / Avalara (`LP-120` refuse). Export zero-rate product (`LP-119`). Printing SST **registration number** on PDF (also LP-107). Full RMCD SST-02 filing.

**Invariant:** Lines that we submit to MyInvois carry the correct **tax type code** (`01`–`06`, `E`) and, when the supplier is SST-registered, the UBL party identification `schemeID="SST"` (and TTX if ever needed). Ledger `LIABILITY_TAX_PAYABLE` matches that amount. Default `06` (Not applicable) is only for truly untaxed lines.

---

## 0. Scope lock

In scope:

- Stop defaulting every `LedgerLine` to `taxTypeCode=06` + `msicCode=004` without a merchant choice
- Product (and quote line) optional SST: type `02` Service Tax + rate **or** `06`
- Emit SST id on UBL supplier party from `TenantBillingProfile.SstRegistrationNumber` / Lhdn config
- PDF tax label “SST” when type `02` (with LP-107)

Out of scope:

- Tourism tax / LVG / HVGT as first-class products (`03`–`05`)
- Inclusive vs exclusive engine beyond “store the amount we were given”
- Computing SST when Billplz `TaxAmount=0` without a merchant rate
- Classification code catalog UI (keep `022` default for individual, `004` for consolidation)

---

## 1. Verdict

Tracker **P**: TypeSpec `TaxTypeCode` and ledger columns exist. Almost every writer leaves **06 + 004**. Standard/Credit templates **omit** SST/TTX party IDs (self-billed templates hardcode `NA`). Profile SST number is stored and unused. Gateways do not supply Malaysian SST (Billplz/CHIP tax = 0; Stripe Tax is the wrong tax).

There is **no product SST rate**. Un-hiding invoicing without this still files consolidated sales as tax type 06.

Official SDK (2026-08-16): `01` Sales Tax, `02` Service Tax, `03` TTx, `04` HVGT, `05` LVG, `06` N/A, `E` Exempt. Classification `004` is **consolidated B2C**, not “e-commerce SST”.

---

## 2. Current files

| Path | Role |
|------|------|
| `packages/api-spec/modules/lhdn/models.tsp` | `TaxTypeCode` |
| `LedgerEntry.AddLine(..., taxTypeCode = "06", msicCode = "004")` | Silent default |
| `GatewayPaymentCompletedHandler` | Tax amount from event only; does not set type |
| `ViewModelMapper` | Passes `TaxTypeCode` through; classification 004 if general public |
| `StandardInvoice.xml` / `CreditNote.xml` / `ConsolidatedInvoice.xml` | TIN + BRN/NRIC only |
| `SelfBilled*.xml` | `schemeID="SST">NA` |
| `TenantBillingProfile.SstRegistrationNumber` | Stored |
| `docs/xml/invoice-v1-1` samples | Have SST/TTX — **thicker than live templates** |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No merchant SST rate / type on Product or quote lines |
| G2 | Ledger default 06/004 |
| G3 | UBL supplier missing SST schemeID |
| G4 | PDF “Tax” unlabeled |
| G5 | Consolidation inherits 06 |
| G6 | Billplz will never fill `TaxAmount` — must compute from our rate if SST applies |

---

## 4. Recommended model

```
Product (optional):
  sst_tax_type: 06 | 02   // Wave 2 only these two
  sst_rate_percent: 0 | 8  // or config; do not invent 8% if merchant not registered

On pay:
  if type 02 and rate > 0:
        tax = round(net * rate / 100)   // exclusive, document it
        LIABILITY_TAX_PAYABLE
        line TaxTypeCode=02
  else:
        tax 0, type 06

UBL supplier:
  if profile.SstRegistrationNumber:
        PartyIdentification schemeID=SST
  else:
        omit or NA per latest SDK (match official sample)

Consolidated lines:
  group by TaxTypeCode (already) — now 02 can appear
```

If merchant has no SST number, **force type 06** even if they typed 8%. Do not file service tax without a registration.

MSIC: use `LhdnTenantConfig.MsicCode` on supplier; do not put `004` on individual B2B lines (`022` already the mapper default).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| Product + TypeSpec + `ProductForm` | Optional SST type/rate |
| `InitiateCheckout` / quote totals | Compute tax when configured |
| `GatewayPaymentCompletedHandler` | Prefer our tax if event tax is 0 and product has SST |
| `AddLine` callers | Pass real type; stop relying on default for revenue lines |
| `StandardInvoice.xml` (+ credit/cons) | SST party id from view model |
| `ViewModelMapper` / party VM | `SstNumber?` from config/profile |
| `BaseInvoiceDocument` | Label SST when tax > 0 |

Must not: Stripe Tax amounts as SST; 8% without SST id.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Product 06 | Tax 0; UBL tax type 06; no SST party id |
| Product 02 / 8% / SST id set | Tax amount; type 02; SST scheme in XML string |
| Product 02 but no SST id | Coerce 06 or 400 on save product |
| Consolidation mixed | Separate grouped lines (existing grouping) |
| Billplz event TaxAmount=0 + product 02 | Still books tax from rate |

---

## 7. Acceptance

1. Merchant can mark a checkout link as SST service tax **only if** legal profile has SST #.  
2. Sandbox UBL for that sale includes tax type `02` and `schemeID="SST"`.  
3. Untaxed products stay `06`.  
4. PDF says SST, not a blank “Tax”, when applicable.

Tracker **P → Y** after 1–3. Codes-on-DTO-only stays **P**.

---

Do **not** implement from this file.
