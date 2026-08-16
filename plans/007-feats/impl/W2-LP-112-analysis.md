# W2-LP-112 — TIN / taxpayer validation

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-112`. Tracker: *TIN / taxpayer validation* — Lazuar **B**. Alias `LP-TAX-005`, `INV-023`.  
**Not this ID:** Collecting TIN fields (`LP-022`). Merchant supplier TIN (`LP-122`). MyInvois submit (`LP-110`). AutoCount-style “type BRN, get name” as a standalone ERP widget.

**Invariant:** Before a B2B e-invoice is submitted, the buyer’s TIN + ID type + ID value are checked against MyInvois (`POST /lhdn/taxpayer/validate` / `TaxpayerValidationService`). Checkout may **warn**; submit must **refuse** invalid pairs. Cache stays (30d valid / 7d invalid).

---

## 0. Scope lock

In scope:

- Keep gateway + `TinValidateCache` + integrator POST
- Wire **id_type + id_value** next to TIN on B2B checkout / QuoteView
- Ops: validate button on billing profile supplier TIN (optional same ticket)
- Block `SubmitTaxDocument` for type `01` individual when last validation is invalid / missing

Out of scope:

- Validating every B2C consumer
- General public TIN `EI00000000010` (consolidation — skip validate)
- Replacing LHDN’s API with a regex

---

## 1. Verdict

The **API is real**. The **UX is absent**. Checkout TIN (LP-022) only stores a string. Initiate passes `IdType = null` and stuffed company name into `IdValue`. Submit handlers invent BRN `202001012345`.

`TaxpayerValidationService`: HMAC cache key (salt `Lhdn:TinHashSalt`), token, `ValidateTaxpayerTinAsync`, returns `Is_valid` + `Taxpayer_name`. Throws if tenant MyInvois creds missing.

Without `id_type`/`id_value`, the official API cannot validate. A TIN-only field is **not** LP-112.

---

## 2. Current files

| Path | Role |
|------|------|
| `DocumentEndpoints.cs` | `POST /lhdn/taxpayer/validate` (`IntegrationLhdnDocumentsRead`) |
| `ValidateTaxpayerTinCommand.cs` | Thin wrapper |
| `TaxpayerValidationService.cs` | Cache + gateway |
| `LhdnGatewayAdapter.Tin.cs` | HTTP |
| `CheckoutForm.tsx` | TIN only, hidden |
| `ClientProfileEntity` | `Tin`, `IdType`, `IdValue` — IdType never set from checkout |

OrgAdmin validate: not on `BillingProfilePage`. Integrators with LHDN read scope can call it today.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Checkout has no ID type / ID value |
| G2 | Company name written to `IdValue` (LP-022 must fix first) |
| G3 | Portal never calls validate |
| G4 | Submit does not require a successful validation |
| G5 | No ops “check TIN” on legal profile |
| G6 | Validate requires LHDN tenant config — product checkout will 400 without LP-122 credentials |

---

## 4. Recommended model

```
B2B form:
  company_name, tin,
  id_type: BRN | NRIC | PASSPORT | ARMY,
  id_value

on blur / before Pay:
  POST /public/…/validate-tin   OR   authenticated public commerce wrapper
  → { is_valid, taxpayer_name }
  show name; block pay if invalid (or allow pay + block submit only — pick one)

SubmitTaxDocument (type 01, not general public):
  if no cache hit valid → validate again → refuse
```

Prefer **warn on hop-1, hard-stop on submit**. Taking FPX then failing MyInvois is a support nightmare; blocking pay is better for B2B products.

Public validate must not leak other taxpayers’ data beyond `taxpayer_name` for the pair the buyer typed. Rate-limit. Do not expose raw LHDN errors with internals.

Do **not** put validate on the LHDN integrator group only — hop-1 is unauthenticated. Add `POST /public/commerce/{slug}/validate-tin` that uses the **tenant** MyInvois creds server-side (same as TaxpayerValidationService).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| TypeSpec checkout DTO | `id_type?`, `id_value?` |
| `CheckoutForm` / `QuoteView` | Select + value; call public validate |
| `InitiateCheckout` | Pass IdType/IdValue into CRM (after LP-022 arity fix) |
| New public commerce validate endpoint | Tenant-scoped; 429-friendly |
| `SubmitTaxDocument` or B2B handler | Require valid cache / live check |
| Optional `BillingProfilePage` | Validate supplier TIN |

Must not: validate B2C; store BRN samples; call validate without tenant creds (show “merchant has not connected MyInvois”).

---

## 6. Tests

| Case | Expect |
|------|--------|
| Cache hit unexpired | No gateway call |
| Invalid pair | `is_valid=false`; checkout can still be configured to 400 |
| Missing tenant config | 400 “LHDN not configured” |
| Submit type 01 invalid TIN | No TaxDocument |
| Consolidation EI00000000010 | No validate |

---

## 7. Acceptance

1. B2B checkout collects TIN + ID type + ID value.  
2. Buyer sees valid/invalid **before** or at pay.  
3. Invalid pair cannot produce a MyInvois type `01`.  
4. Integrator POST validate unchanged.

Tracker **B → Y** when 1–3 work on sandbox. API-only stays **B**.

---

Do **not** implement from this file.
