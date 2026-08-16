# W2-LP-116 — Cancel / reject within IRBM rules

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-116`. Tracker: *Cancel / reject within IRBM rules* — Lazuar **B**. Alias `LP-TAX-006`.  
**Not this ID:** Post-72h credit note (`LP-104`). Money refund (`LP-091`). Inbound Peppol.

**Invariant:** Supplier can **cancel** a `VALID` document within **72 hours of `ValidatedAt`**, with a reason, via MyInvois `PUT …/state`. After 72h the button is dead and copy tells staff to issue a credit note. **Buyer reject is not implemented** — do not sell it. Tracker cell oversells “reject”.

---

## 0. Scope lock

In scope:

- Keep `CancelTaxDocumentCommand` + `CancelWindowMustBeValidRule` + gateway
- Remount ops cancel UI **on the correct internal id**
- Refund-in-window should call the **same** command (LP-104)
- Honest copy: cancel only

Out of scope:

- Buyer reject API / inbound document pull / “customer rejected your e-invoice”
- Cancel after 72h
- Auto ledger contra beyond what cancel event already does (if anything)

---

## 1. Verdict

Cancel **backend** exists. Cancel **button** is hidden and wired to the **ledger GUID**, which is not `TaxDocument.InternalReferenceId`. `doc.Cancel()` runs **before** the HTTP call; on gateway failure the in-memory cancel may still persist depending on exception path — handler throws after failed HTTP but **after** `doc.Cancel()`. Check: `doc.Cancel()` then HTTP; on failure throws **without** SaveChanges — OK if no save before throw. `SaveChanges` is after success. Good.

`TaxInvoiceDetailPanel`:

```ts
if (!invoice?.tax_invoice_id) throw …
POST /lhdn/documents/{internalId}/cancel
params: { internalId: invoice.id }  // ledger GUID
```

Lhdn looks up by internal id (`RCPT-` / `INV-` / `B2C-CONS-`). **Always “Document not found”.**

UI 72h clock uses `invoice.timestamp` (ledger time), not `ValidatedAt`. Can show Cancel when domain would refuse, or the reverse.

No reject endpoint anywhere (`LhdnGatewayAdapter.Cancel` only `status=cancelled`).

---

## 2. Current files

| Path | Role |
|------|------|
| `CancelTaxDocumentCommand.cs` | Domain cancel + gateway |
| `CancelWindowMustBeValidRule.cs` | 72h from `ValidatedAt` |
| `LhdnGatewayAdapter.Cancel.cs` | PUT state cancelled |
| `TaxDocument.Cancel()` | Status CANCELLED |
| `TaxInvoiceDetailPanel.tsx` | Wrong id; clock from ledger timestamp |
| `DocumentEndpoints.cs` | Integrator cancel |

`LhdnDocumentCancelledIntegrationEvent` is published. Check Billing subscriber — if none, ledger badge stays VALID. Include in this ticket if missing.

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Ops uses ledger id |
| G2 | 72h UI ≠ `ValidatedAt` |
| G3 | No Billing consumer for cancelled (verify) |
| G4 | Reject advertised, not built |
| G5 | Page unrouted |

---

## 4. Recommended model

```
Cancel button iff LHDN GET status==VALID && now < ValidatedAt+72h
internalId = CustomerDocumentNumber || consolidation ref || TaxDocument.InternalReferenceId
POST /lhdn/documents/{internalId}/cancel { reason }
→ MyInvois + TaxDocument CANCELLED + ledger status CANCELLED
else: disabled “Issue a credit note (LP-104)”
```

Do **not** add reject in Wave 2 unless a paying AP clerk appears. Flip tracker wording when implementing: **cancel only**.

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `TaxInvoiceDetailPanel` | Cancel with document number; clock from `validated_at` (add to ledger DTO or LHDN GET) |
| `App.tsx` | Remount with LP-103 |
| Billing cancelled handler | If missing, `UpdateLhdnStatus(..., CANCELLED)` |
| Tracker / ops copy | Drop “reject” |

Must not: buyer reject; cancel INVALID/PENDING; cancel by UUID in the path (API is internal id).

---

## 6. Tests

| Case | Expect |
|------|--------|
| VALID, 1h, correct internal id | Gateway cancel + status CANCELLED |
| VALID, 80h | Domain rule broken; 400 |
| Unknown internal id | 400 existing |
| Ops would have sent ledger GUID | Test documents the correct id (module test on command) |

---

## 7. Acceptance

1. From remounted ops, cancel a sandbox VALID invoice within 72h; MyInvois and our row show CANCELLED.  
2. After 72h, button disabled; no gateway call.  
3. Copy does not promise buyer reject.  
4. Integrator cancel still works.

Tracker: keep **B** until 1. After ship, consider cell **P** (cancel **Y**, reject **N**) rather than full **Y** unless you implement reject.

---

Do **not** implement from this file.
