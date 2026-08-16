# W3-LP-105 — done

Custom quotes accept `due_at` / `terms` (`due_on_receipt` | `net_7` | `net_15` | `net_30`). `ExpiresAt` is raised to `DueAt + 14d`. Hourly `InvoiceReminderJob` emails the public `/pay/{id}` link at offsets -3 / 0 / +3. Session stays OPEN (no PAST_DUE). Catalog template **Invoice Reminder**.

## Files

- `CheckoutSession.DueAt` + `SetDueAt`
- `CreateCustomCheckoutCommand` + TypeSpec + CreateQuoteModal terms
- `InvoiceReminderDispatchLog` + `InvoiceReminderJob`
- `DefaultMessageTemplates` Invoice Reminder + comms `invoice.reminder`

## Tests

- Net 30 DueAt; day 0 sends once; COMPLETED skipped; product sessions ignored

Not committed. Not pushed.

Tracker `LP-105` **N → Y** (quote UI + job).
