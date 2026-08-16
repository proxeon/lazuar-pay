# W2-LP-111 — VALID / INVALID poll

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-111`. Tracker: *Status poll VALID / INVALID* — Lazuar **B**.  
**Not this ID:** Submit (`LP-110`). QR rendering (`LP-113`). Outbound `invoice.valid` catalog polish (`LP-135`). Agent-only list as the merchant UI.

**Invariant:** After submit, a worker polls MyInvois until `VALID` or `INVALID`. Ledger + ops + (optional) PDF follow **that** status. `SUBMITTED` is not success. Browser redirect is not success.

---

## 0. Scope lock

In scope:

- Keep `LhdnStatusPollingJob` + `GET /lhdn/documents/{internalId}`
- Fix Billing’s VALID consumer so it **finds** the ledger row
- Show status on remounted Tax Invoice panel
- INVALID: persist error; do not generate a “Tax Invoice” PDF

Out of scope:

- Inbound MyInvois webhooks as primary (poll is the path)
- Buyer reject (LP-116)
- Schematron beyond XSD

---

## 1. Verdict

The poller is real. The **closed UI/ledger loop** is not.

`LhdnStatusPollingJob`: SKIP LOCKED on `SUBMITTED`, `GetDocumentStatusAsync`, `MarkAsValid(longId)` / `MarkAsInvalid`, publishes `LhdnDocumentValidated` **only on VALID**, dispatches One webhooks `invoice.valid` / `invoice.invalid`.

Billing `LhdnDocumentValidatedIntegrationEventHandler` loads

`LedgerEntries.ReferenceId == event.InternalReferenceId`.

Payment `ReferenceId` is the **gateway tx id**. LHDN `internal_id` is `RCPT-` / `INV-` / `B2C-CONS-…`. **Match misses.** Status stays `B2C_RECEIPT` / `CONSOLIDATED_PENDING`. PDF + QR never regenerate.

INVALID does not publish a Billing event — only the customer webhook. Ops ledger badge never becomes `INVALID` unless something else writes it.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs` | Poll |
| `Modules/Lhdn/Application/Queries/LhdnQueries.cs` | GET DTO + `qr_link` |
| `Modules/Lhdn/Contracts/Events/LhdnDocumentValidatedIntegrationEvent.cs` | VALID only |
| `Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentValidatedIntegrationEventHandler.cs` | Broken join |
| `TaxInvoicesPage.tsx` | Badge from `lhdn_validation_status` |
| One dispatcher | `invoice.valid` / `invoice.invalid` (R43 done) |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Ledger join key wrong |
| G2 | No INVALID → ledger update |
| G3 | No merchant list of TaxDocuments (stuck PENDING) |
| G4 | Panel does not poll GET `/lhdn/documents/{internalId}` |
| G5 | VALID PDF regen depends on G1 (LP-103 / LP-113) |

---

## 4. Recommended model

```
TaxDocument.InternalReferenceId
  = ledger.CustomerDocumentNumber  (RCPT-/INV-/CN-)
  or consolidation ref stored on TaxInvoiceId / a dedicated column

VALID  → UpdateLhdnStatus(uuid, VALID) on all matching rows
INVALID → UpdateLhdnStatus(null, INVALID) + ErrorMessage; no Tax Invoice PDF
```

Ops: badge from ledger (after G1/G2) **and** optional live GET by document number.

Publish a small `LhdnDocumentInvalidated` or reuse the same event with `Status=INVALID` so Billing has one handler.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `LhdnDocumentValidatedIntegrationEventHandler` | Lookup by `CustomerDocumentNumber` or `TaxInvoiceId` |
| Poller | Also notify Billing on INVALID |
| `LedgerEntry.UpdateLhdnStatus` | Allow INVALID without requiring UUID |
| TaxInvoiceDetailPanel | Fetch LHDN GET using **document number** |

Must not: treat `SUBMITTED` as VALID in the UI; join on ledger GUID.

---

## 6. Tests

| Case | Expect |
|------|--------|
| VALID event `internal_id=RCPT-2026-00001` | Row with that `CustomerDocumentNumber` → VALID + UUID |
| VALID `B2C-CONS-…` | All rows with that `TaxInvoiceId` updated |
| INVALID | Status INVALID; **no** `GenerateAndStoreDocument` |
| Poller VALID | Webhook `invoice.valid` still sent |
| Unknown internal_id | Handler no-throw |

---

## 7. Acceptance

1. Sandbox document becomes VALID in MyInvois → ops badge VALID without SQL.  
2. INVALID shows error, no tax-invoice PDF.  
3. Integrator GET `{internalId}` matches the poller.  
4. `invoice.valid` / `invoice.invalid` still fire.

Tracker **B → Y** when 1–2 are merchant-visible. Poller-only stays **B**.

---

Do **not** implement from this file.
