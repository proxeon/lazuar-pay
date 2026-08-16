# W1-LP-097 — CSV reconciliation export

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 row `LP-097` (“CSV reconciliation export”). Tracker in [00-checklist-tracker.md](../00-checklist-tracker.md): *Reconciliation export (CSV)* — Lazuar **N**.  
**Not this ID:** subscriber directory CSV (`GET /admin/commerce/subscribers/export`, tracker-adjacent SL-079 / inventory “CSV export”) is a **CRM list**, not a bookkeeper reconcile file. Do not expand this ticket into Xero (`LP-121`) or payout settlement reports (`LP-095`, refuse).

**Invariant:** A merchant (or their bookkeeper) can download a UTF-8 CSV of **money movements** that matches a Billplz / Stripe / CHIP payout or dashboard export on `external_reference` / gateway transaction id. The file is a **read of existing Commerce transaction logs + Billing ledger facts**, not a new GL product.

---

## 0. Scope lock

In scope:

- Ops export of confirmed / refunded money rows (Commerce `TransactionLogs`)
- Optional second sheet/file of Billing ledger entries (same date window)
- Date range + status filters
- Columns a human can join to a processor CSV

Out of scope:

- Subscriber export (already shipped)
- Settlement / payout batch entity (`payout.paid`, T+N bank file) — **LP-095 R**
- Xero / QuickBooks push — **LP-121**
- M2M key-auth export (console JWT is enough for Wave 1)
- Changing how fees are computed
- Multi-currency FX reports (`LP-096`)

---

## 1. Verdict

| Layer | Today |
|-------|--------|
| Money rows in DB | **Y** — `commerce.TransactionLogs` (gross / fee / net / `ExternalReference`) |
| Double-entry book | **Y** — `billing.LedgerEntries` + lines; `ReferenceId` = gateway txn id on `GATEWAY_PAYMENT` |
| Paginated JSON APIs | **Y** — `GET /admin/commerce/transactions` (max 100/page), `GET /admin/billing/ledger` |
| Ops Transactions UI | **Y** — table + detail; **no Download** |
| Reconcile CSV | **N** — tracker is correct |
| Subscriber CSV | **Y** — wrong artifact; keep it |

Bookkeepers today: click through pages or SQL. That is why the cell is **N**, not **P**.

---

## 2. Current files

### 2.1 Commerce money log (primary export source)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Domain/Entities/CommerceTransactionLog.cs` | `Amount`, `FeeAmount`, `NetAmount` (= amount − fee), `Currency`, `Status` (`CONFIRMED` / `REFUNDED`), `CustomerName`, `CustomerEmail`, `ProductName`, `RecordedByName` (gateway or staff), `ExternalReference` (gateway txn id), `CreatedAt` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayPaymentCompletedIntegrationEventHandler.Helpers.cs` | `LogTransactionAsync` — writes CONFIRMED + `@event.GatewayTransactionId` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/EventHandlers/GatewayRefundCompletedIntegrationEventHandler.cs` | Marks existing log `REFUNDED` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Transactions.cs` | Paginated list; **`payment_method` query param is unused** in SQL; `RecordedByName` aliased as method |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs` | `GET /transactions` (page size cap 100), `POST …/refund` — **no export** |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/packages/api-spec/modules/commerce/models/subscriber.tsp` | `TransactionLogDto` — no export route |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/TransactionsPage.tsx` | Filters + pagination; no CSV |

`ExternalReference` is the join key to Billplz bill id / Stripe PaymentIntent / CHIP purchase id. Billplz fee is often **0** in this log (adapter honesty). Label it as “Hub-recorded fee”, not “bank MDR”.

### 2.2 Billing ledger (optional second file)

| Path | Role |
|------|------|
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs` | `ReferenceType`, `ReferenceId`, timestamp, LHDN fields |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Domain/AccountTypes.cs` | `LedgerReferenceTypes.GatewayPayment` / `GatewayRefund` / `ZeroAmountCheckout` / `ManualEnrollment` |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` | Posts `GATEWAY_PAYMENT` with `ReferenceId` = gateway txn id |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs` | `GET /admin/billing/ledger` — paginated JSON, date filters exist |
| `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx` | **Utility credits**, not GMV |

Ledger is the double-entry book. Transaction log is the merchant-facing cash register. Export **transaction log first**; ledger flatten is a should-have on the same ticket if cheap (one row per entry + summed cash line).

### 2.3 Existing CSV (do not confuse)

| Path | Role |
|------|------|
| `SubscriberEndpoints.cs` `GET /subscribers/export` | Cap 10_000; columns id/name/email/phone/product/status/dates — **no money** |
| `SubscribersPage.tsx` `handleExport` | Downloads that file |

Reuse the UTF-8 BOM + RFC-4180 escape helper from `BuildSubscribersCsv`. Do not bolt money columns onto the subscriber file.

---

## 3. Gaps

### G1 — No money CSV (P0)

No route, no TypeSpec, no button. Tracker **N**.

### G2 — List API cannot dump a month

`limit` max 100. A bookkeeper would page 30+ times. Export must bypass the UI page size (cap e.g. 50_000, same spirit as subscribers 10_000).

### G3 — `payment_method` filter is a lie

Query param is accepted and ignored. Export should filter on `RecordedByName` / status / `CreatedAt` for real.

### G4 — Ledger and Commerce are parallel books

They share gateway txn id when both ran. Export must not pretend they are one table. Two files or a `source` column.

**Not gaps**

| Observation | Why not LP-097 |
|-------------|----------------|
| Billplz fee often 0 | Honesty of the adapter; CSV must print 0, not invent MDR |
| No payout id | LP-095 |
| Subscriber export exists | Different job |

---

## 4. Minimal changes

### 4.1 Must

| File | Change |
|------|--------|
| `CommerceQueryService.Transactions.cs` | Add `ExportTransactionsAsync(orgId, from, to, status)` — same SELECT as list, **no** 100 cap (hard cap 50_000), honor `CreatedAt` range + status. Return rows (or stream). |
| `TransactionEndpoints.cs` | `GET /admin/commerce/transactions/export?from=&to=&status=` → `text/csv`, filename `transactions_yyyyMMdd_yyyyMMdd.csv`, UTF-8 BOM. OrgAdmin (existing group). |
| `packages/api-spec/modules/commerce/admin-routes.tsp` | Document the GET + query params. |
| `TransactionsPage.tsx` | Download button next to filters; pass current date/status; same cookie + `X-Tenant-Id` pattern as subscriber export. |

**CSV columns (locked):**

```
id,created_at,status,amount,fee_amount,net_amount,currency,customer_name,customer_email,product_name,recorded_by,external_reference
```

ISO-8601 UTC for dates. Invariant culture decimals. Escape commas/quotes like `BuildSubscribersCsv`.

Default range if omitted: last 31 days (avoid unbounded full-table dump).

### 4.2 Should (same ticket if small)

| File | Change |
|------|--------|
| `AdminLedgerEndpoints.cs` | `GET /admin/billing/ledger/export` — flatten: `id,timestamp,reference_type,reference_id,description,cash_amount,currency` where `cash_amount` = sum of `ASSET_CASH` lines (signed). |
| Utility / Transactions copy | One line: “Match `external_reference` to Billplz bill id / Stripe PaymentIntent / CHIP id. Fees are Hub-recorded, not the bank payout file.” |

### 4.3 Do not

- Join CRM in the export SQL (names already denormalized on the log).
- Add payout batches.
- Change fee calculation.
- Key-auth this in Wave 1.

---

## 5. Tests

File: `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/TransactionExportTests.cs`

| Case | Expect |
|------|--------|
| Org A rows only | Org B id never appears |
| `from`/`to` | Inclusive UTC bounds |
| Status `CONFIRMED` | Refunded rows omitted |
| Cap | >50_000 → first 50_000 + (optional) `X-Export-Truncated: true` or 400 “narrow the range” — pick one and test it |
| CSV | Header exact; BOM present; email with comma quoted |
| Empty range | Header-only file, 200 |

Manual: Ops → Transactions → export last week → open in Numbers → VLOOKUP against a Billplz sandbox CSV on bill id.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Huge tenant dump | Default 31 days + hard cap |
| PII in email column | Same as on-screen table; OrgAdmin only |
| Fee ≠ processor MDR | Header comment / UI note |
| Ledger vs log mismatch | Two files; do not invent a join that drops zero-amount |

---

## 7. Acceptance

Close LP-097 when:

1. OrgAdmin can download a CSV of transaction logs for a date range from Ops → Transactions.
2. Every exported `external_reference` matches the gateway id stored on the CONFIRMED (or REFUNDED) row.
3. Amounts are Hub-recorded gross / fee / net; no invented payout batch.
4. Subscriber export is unchanged.
5. Tests in §5 pass.
6. Tracker Lazuar cell can move **N → Y** (or **P** if only commerce file ships and ledger export is deferred — prefer **Y** with commerce file alone; ledger is bonus).

---

## 8. Implement order

1. Query + CSV builder + endpoint + TypeSpec  
2. Ops button  
3. Tests  
4. Optional ledger export  
