# W3-LP-161 — Honest MRR / ARR (catalog snapshot, not live price join)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 3 `LP-161`. Tracker: *Honest MRR / ARR (ledger-based)* — Lazuar **P**. Aliases `LP-TRU-005` / dashboard `mrr` unused.  
**Not this ID:** ChartMogul waterfall / cohorts (`LP-165` skip). Recovered revenue (`LP-077`). Net cash card (billing summary). Deferred RevRec job (parked 00.3). ERP (`LP-206` refuse).

**Invariant:** MRR is the monthly equivalent of **committed recurring amounts on `ACTIVE` rows**, using a **snapshot** (`UnitAmount × Quantity`, yearly÷12). It is **not** `SUM(products.price)` after a merchant edits the catalog. PAST_DUE is **at-risk**, not MRR. Dashboard shows the number with a one-line glossary.

“Ledger-based” in the tracker means “reconciled to what we actually bill,” not “sum of `RevenueGross` this month” (that is cash). `RevenueRecognitionJob` is parked — do not unpark it here.

---

## 0. Scope lock

In scope:

- `Subscription.UnitAmount` snapshot (if LP-063 did not already add it)  
- `GetStatsAsync` formula change  
- Dashboard KPI for MRR + ARR (`×12`)  
- Short glossary on the card

Out of scope:

- New / expansion / contraction / churn movements  
- Fee toggle  
- CMRR  
- Writing MRR into the ledger  
- Including `TRIALING` (0) or collection-paused (exclude)

---

## 1. Verdict

API already computes `mrr` as `ACTIVE|PAST_DUE` × **live** `p.Price` (yr/12). Dashboard **does not render it**. Editing a product silently rewrites MRR. PAST_DUE is counted as if it will pay.

That is why the cell is **P**: a number exists and is the wrong definition.

---

## 2. Current files

| Path | Role |
|------|------|
| `CommerceQueryService.Stats.cs` | Join `Products.Price`; includes PAST_DUE |
| `CommerceStatsDto.mrr` | Unused in UI |
| `DashboardPage.tsx` | Net cash, actives, past due, churn, recovered |
| `BillingQueryService.GetFinancialSummaryAsync` | Cash / fees / tax — not MRR |
| `Subscription` | No unit snapshot |

---

## 3. Exact gaps

| # | Gap |
|---|-----|
| G1 | Live catalog join |
| G2 | PAST_DUE included |
| G3 | No qty / pending plan |
| G4 | Card hidden |
| G5 | No written definition |

---

## 4. Recommended model

```
On Activate / ActivateTrial / ApplyPendingPlan / ApplyPendingQty:
  UnitAmount = chosen price (major units, one seat)
  Quantity    = N (LP-060, else 1)

MRR = SUM over Status==ACTIVE
        AND CollectionPausedUntil is null
        AND Interval in (mo, yr)
      of (Interval==yr ? UnitAmount*Qty/12 : UnitAmount*Qty)

ARR = MRR * 12

PAST_DUE count stays its own card (already).
CancelAtPeriodEnd ACTIVE: still in MRR until finalize (they paid through).
TRIALING: 0.
```

Backfill: `UnitAmount = product.Price` for existing rows (one-time migration SQL). After that, catalog edits do not move MRR until the next apply/renewal snapshot (renewal success should refresh snapshot from current catalog or pending price — pick **refresh on successful period payment** so a merchant price change hits at the same moment as the charge).

---

## 5. Minimal code changes

| File | Change |
|------|--------|
| `Subscription` + migration | `UnitAmount numeric` not null default 0 |
| Activate / recover / plan apply | Set snapshot |
| `CommerceQueryService.Stats.cs` | Formula §4; stop joining price for MRR |
| `DashboardPage.tsx` | Fifth/sixth card: MRR, ARR; tooltip glossary |
| Tests | Price edit does not change MRR until snapshot refresh |

Must not: waterfall; unpark RevRec; relabel net cash as MRR.

---

## 6. Tests

| Case | Expect |
|------|--------|
| 2× ACTIVE RM100/mo | MRR 200 |
| 1× ACTIVE RM1200/yr | MRR 100 |
| PAST_DUE RM100 | MRR 0, past_due card 1 |
| Edit product 100→200 | Stats unchanged until paid renewal refreshes snapshot |
| Collection pause (LP-057) | Excluded |
| TRIALING | Excluded |

`CommerceHonestyDtoTests` or new `CommerceMrrTests`.

---

## 7. Acceptance

1. Dashboard shows MRR/ARR matching §4 on a fixture workspace.  
2. Changing a product price does **not** move the card until the next successful cycle (or explicit snapshot apply).  
3. Tooltip: “Committed monthly equivalent of active memberships. Not cash. Past-due is excluded.”  
4. No waterfall.

Tracker **P → Y** after 1–3. Stay **P** if the formula is fixed but still hidden.

---

## 8. Order

1. Snapshot column + write paths  
2. Stats SQL  
3. Dashboard card  
4. Tests  

Do **not** implement from this file.
