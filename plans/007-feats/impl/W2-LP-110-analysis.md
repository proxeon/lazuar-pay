# W2-LP-110 — MyInvois submit (UBL 2.1)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-110`. Tracker: *MyInvois submit (UBL 2.1)* — Lazuar **B**. Alias `LP-TAX-001`.  
**Not this ID:** Poll VALID/INVALID (`LP-111`). TIN API (`LP-112`). Signing (`LP-117`). Consolidation job (`LP-114`). Integrator SDK (already **Y** for API clients).

**Invariant:** A merchant (or the checkout pipeline) can submit a UBL 2.1 document that uses **real supplier + real buyer** identities, meters credits, and lands in `PENDING` → `LhdnSubmissionJob`. Stub TINs must not reach PROD or sandbox “happy path” demos.

---

## 0. Scope lock

In scope:

- Keep `SubmitTaxDocumentCommand` + worker + `POST /lhdn/documents`
- Replace stub B2B trigger with CRM-backed submit
- Bind supplier postal address in **templates** to `LhdnTenantConfig` (today mapper fills it; `StandardInvoice.xml` hardcodes Lot 66 / Bangunan Merdeka)
- Merchant-visible submit status on the remounted tax-invoice panel (read-only is enough)

Out of scope:

- Building a second MyInvois client in Aura
- Peppol
- JSON v1.1 sign (LP-117)
- Self-billed product
- Publishing `InvoiceIssued` with placeholders

---

## 1. Verdict

The **pipe** is production-shaped. The **product** is not.

| Piece | Status |
|-------|--------|
| Strategies + Scriban + XSD + SHA-256 + LF normalize | Real |
| `LhdnSubmissionJob` SKIP LOCKED, token, `format=XML` | Real |
| Credits (`LhdnSubmit=3`) | Real (test mode skips) |
| Integrator `POST /lhdn/documents` + Idempotency-Key | Real |
| B2C `ConsolidatedInvoiceIssued` → submit | Wired |
| B2B `InvoiceIssued` → submit | **Orphan + stub buyer `C1234567890`** |
| Ops submit button | None (list is ledger, not `TaxDocuments`) |
| Supplier address in XML | **Hardcoded sample HQ** |

Selling “e-invoice at checkout” is false until B2B trigger + template address + no stubs.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Lhdn/Application/Commands/SubmitTaxDocumentCommand.cs` | Validate, hash, persist, deduct |
| `Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs` | MyInvois submit |
| `Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs` | Integrator write/read |
| `Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` | **Stub — do not publish** |
| `Modules/Lhdn/Infrastructure/EventHandlers/ConsolidatedInvoiceIssuedIntegrationEventHandler.cs` | General public TIN `EI00000000010` — correct for B2C |
| `Modules/Lhdn/Infrastructure/Templates/StandardInvoice.xml` | Supplier `PostalAddress` hardcoded |
| `ViewModelMapper.cs` | Maps `config.AddressLine1` etc. — unused by that template block |
| `scripts/lhdn_sandbox/` | Eng proof, not merchant |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No honest B2B publisher (LP-103 hook) |
| G2 | Stub handler must stay dead or be rewritten |
| G3 | Template supplier address ignores config |
| G4 | Standard/Credit templates omit SST/TTX party IDs (LP-118) |
| G5 | No ops list of `TaxDocuments` (agent query exists) |
| G6 | Submit always XML `1.0` unless payload sets version |

---

## 4. Recommended model

```
B2B pay (LP-022 TIN on CRM)
  → INV- number (LP-101)
  → SubmitTaxDocument {
        internal_id: INV-…,
        buyer from ClientProfile (tin, name, address),
        id_type/id_value from LP-112 or collected fields,
        supplier from LhdnTenantConfig
    }
  → PENDING → worker → SUBMITTED
```

If `id_type` / `id_value` missing, **do not submit**. Persist “needs buyer ID” on the ledger. Integrator path unchanged.

Kill or rewrite `InvoiceIssuedIntegrationEventHandler` so a future accidental publish cannot file `C1234567890`.

Template: replace hardcoded supplier address with Scriban `supplier.*` (already on the view model).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `StandardInvoice.xml` (+ credit / consolidated if same HQ block) | Bind supplier city/postcode/state/lines |
| New `B2bSaleSubmitHandler` (name as you like) | CRM → `SubmitTaxDocumentCommand` |
| `InvoiceIssuedIntegrationEventHandler` | Guard: refuse stub; or delete subscription |
| Ops TaxInvoiceDetailPanel | Show LHDN status via `GET /lhdn/documents/{internalId}` using **invoice number**, not ledger GUID |
| `App.tsx` | Remount is LP-103; this ticket needs the GET to work |

Must not: submit without buyer TIN; enable stub publisher; add Aura LHDN scopes.

---

## 6. Tests

| Case | Expect |
|------|--------|
| Submit missing config | Existing throw |
| B2B handler, CRM TIN set | Payload buyer_tin = CRM; not `C1234567890` |
| B2B handler, no TIN | No TaxDocument row |
| Generated XML | Contains config city, not `Bangunan Merdeka` |
| Idempotency-Key replay | Same document id |
| Consolidation path | Unchanged general public TIN |

---

## 7. Acceptance

1. Sandbox: B2B pay with real buyer TIN creates a `PENDING`/`SUBMITTED` row whose XML has **tenant** legal address and **buyer** TIN.  
2. Integrator POST still works.  
3. Stub TIN never inserted.  
4. Merchant can see status on the remounted sales document (or `GET /lhdn/documents/{INV-…}`).  

Tracker stays **B** until 1+3. **Y** when a merchant (not only an API key) can complete a sandbox submit from a checkout.

---

Do **not** implement from this file.
