# W2-LP-114 — B2C monthly consolidation

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 2 `LP-114`. Tracker: *B2C monthly consolidation* — Lazuar **B**. Alias `LP-TAX-002`, `INV-020` / `INV-027`.  
**Not this ID:** Individual B2B type `01` (`LP-103`/`LP-110`). Self-billed. Export zero-rate (`LP-119`).

**Invariant:** Small B2C receipts marked `PENDING` consolidation are batched once per **closed MYT calendar month** into one type `01` consolidated e-invoice (buyer General Public / TIN `EI00000000010` / classification `004`). From **1 Jan 2026**, a **single transaction above RM 10,000 must not** enter that batch. Merchants can see last-run status.

---

## 0. Scope lock

In scope:

- Keep `B2cConsolidationJob` (28th 02:00 MYT + 24-month catch-up)
- **RM 10,000 split**: those rows get individual type `01` (or “needs buyer TIN”) instead of `MarkConsolidatedPending`
- Ops: last period + ref + status (read-only)
- Optional “buyer requested individual e-invoice” flag later — if cheap, a ledger column; do not build a request portal in this ticket

Out of scope:

- Changing 28th vs LHDN “by the 7th” folklore without a tax advisor note in UI
- Merchant “run now” that double-issues (job already skip-if `TaxInvoiceId == B2C-CONS-…`)
- Tax type engine (LP-118) — still group by existing `TaxTypeCode`

---

## 1. Verdict

The worker is the most complete Wave 2 backend. It is still **B** because: no RM10k rule, no merchant visibility, default line tax type `06` + classification `004`, B2B flag never set so **everything** consolidates.

| Behavior | Code |
|----------|------|
| Schedule | 28th 02:00 MYT + catch-up on start |
| Eligibility | `CustomerType==B2C` and pending / legacy null |
| Ref | `B2C-CONS-{yyyyMM}-{org:N}` |
| Event | `ConsolidatedInvoiceIssued` → Lhdn general public submit |
| RM10k | **Not inspected** |
| Ops | None |

Confirm 2026 threshold on hasil.gov.my before baking `10000` — vendor blogs agree; official page wins.

---

## 2. Current files

| Path | Role |
|------|------|
| `Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` | Entire batch |
| `LedgerEntry.AssignB2cReceipt` / `MarkConsolidatedPending` | Status machine |
| `ConsolidatedInvoiceIssuedIntegrationEventHandler` | Submit type `01` |
| `ConsolidatedInvoiceStrategy` + `ConsolidatedInvoice.xml` | UBL |
| Tests `B2cConsolidationJobTests.cs` | Catch-up / skip / publish |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | No RM10k (or current official) split |
| G2 | All product checkouts are B2C (LP-022 metadata) so high-ticket B2B-shaped sales consolidate illegally |
| G3 | Tax lines default `06` / MSIC `004` (`LedgerEntry.AddLine`) — consolidation groups as “not applicable” |
| G4 | No month status UI |
| G5 | VALID join miss (LP-111) so badges stay `CONSOLIDATED_PENDING` |
| G6 | No “request individual e-invoice” (2026 buyer right) |

---

## 4. Recommended model

```
On B2C AssignB2cReceipt:
  if AmountPaid > threshold (MYR):
        MarkConsolidationNotRequired
        queue individual 01  OR  status NEEDS_BUYER_TIN
  else PENDING

Job: unchanged grouping for PENDING only
Ops: “B2C e-invoice {yyyy-MM}: N receipts, ref, LHDN status”
```

Threshold in config `Lhdn:B2cIndividualThresholdMyr` default `10000`. Document the date.

Do not consolidate `is_b2b_required` rows (already `NOT_REQUIRED` once LP-022 stamps metadata).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `AssignB2cReceipt` or payment handler | Threshold split |
| `B2cConsolidationJob` | Defense-in-depth: skip lines/entries over threshold if any slipped through |
| Config + tests | 9999 vs 10000.01 |
| Ops Tax Invoices or a thin banner | Last `B2C-CONS-*` + status |
| Copy | “Submitted on the 28th for the prior month; not a guarantee of the IRBM calendar.” |

Must not: stuff RM10k+ into the batch; add a “run now” that ignores `alreadyConsolidated`.

---

## 6. Tests

| Case | Expect |
|------|--------|
| RM 50 B2C | PENDING → job publishes CONS ref |
| RM 10000.01 B2C | `NOT_REQUIRED`; not in batch items |
| Catch-up two months | Two events (existing) |
| Duplicate period | Skip (existing) |
| B2B metadata | Never PENDING |

---

## 7. Acceptance

1. Below-threshold B2C still batch on the 28th / catch-up.  
2. Above-threshold B2C **never** appears in `B2C-CONS` XML.  
3. Ops shows last consolidation ref + LHDN status (after LP-111 join).  
4. UI does not claim “filed by the 7th” unless you change the schedule.

Tracker **B → P** after 1–2. **Y** when 3 exists. Do not mark **Y** on worker-only.

---

Do **not** implement from this file.
