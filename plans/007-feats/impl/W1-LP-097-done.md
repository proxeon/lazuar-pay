# W1-LP-097 — done

OrgAdmin can download a UTF-8 BOM CSV of Commerce transaction logs. Default last 31 days, hard cap 50_000 (`X-Export-Truncated: true`). Columns locked: id, created_at, status, amount, fee_amount, net_amount, currency, customer_name, customer_email, product_name, recorded_by, external_reference. Subscriber export unchanged. Ledger flatten deferred (tracker **Y** on commerce file alone).

## Files

- `TransactionExportCsv` + `ExportTransactionsAsync`
- `GET /admin/commerce/transactions/export`
- Ops Transactions **CSV** button + Hub-recorded-fee copy
- TypeSpec admin route
- `TransactionExportCsvTests`

## Tests run

- `TransactionExportCsvTests` — **passed**
- Ops `tsc` — clean

Not committed. Not pushed.

Tracker `LP-097` **N → Y**.
