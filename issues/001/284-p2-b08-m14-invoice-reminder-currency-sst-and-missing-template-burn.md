---
number: "284"
id: B08-M14
severity: P2
status: open
source: plans/009-bugs/08-communications-messaging-crm.md
head: "297ba98"
---

# 284 — B08-M14 — Invoice reminder currency/SST and missing-template burn

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/08-communications-messaging-crm.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B08-M14 — P2 — Invoice reminder currency/SST and missing-template burn

**Where:** `InvoiceReminderJob.cs` 108–118; hydrate 147–154; job writes the dispatch log in the same loop after publish (133).

**What:** `currency = "MYR"` always. No SST field on ad-hoc lines (matches hop-1 custom charge — consistent, still a lie if they ever add SST to quotes). Missing template: Communications returns; Commerce already recorded the offset. Exact-day only, UTC, no catch-up (pre-existing 008).

No hydrator test for `EventType == "invoice.reminder"`.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Hourly `InvoiceReminderJob` emails OPEN custom (quote) sessions at UTC day offsets −3 / 0 / +3. The payload still hardcodes `currency = "MYR"` and `amount`/`total_price` as `Sum(qty * unitPrice)` of `AdHocLineItem` — no SST field exists on those lines, so a future SST-on-quote will lie the same way hop-1 custom charge already does. Offsets are exact-day only (`Offsets.Contains(dayOffset)`); a missed hour never catch-up. The job inserts `InvoiceReminderDispatchLog` and publishes `invoice.reminder` in the same loop, then `SaveChanges`. Communications `FulfillmentRequestedIntegrationEventHandler` loads catalog template **Invoice Reminder** and **returns** if it is missing — Commerce has already burned the `(SessionId, DayOffset)` unique log, so that offset never retries. There is still no Communications test that hydrates `EventType == "invoice.reminder"`.

### Still present?
**STILL BROKEN**

Money + currency still hardcoded MYR / line sum:

```108:120:apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs
            var total = session.AdHocLineItems.Sum(i => i.Quantity * i.UnitPrice);
            var payloadObj = new
            {
                client_profile_id = session.ClientProfileId.ToString(),
                session_id = session.Id.ToString(),
                document_number = session.DocumentNumber ?? string.Empty,
                checkout_url = payUrl,
                due_at = session.DueAt.Value.ToString("yyyy-MM-dd"),
                amount = total,
                total_price = total,
                currency = "MYR",
                day_offset = dayOffset,
                plan_name = session.DocumentNumber ?? "Quote"
            };
```

Exact-day UTC, no catch-up (`InvoiceReminderJob.cs:24`, `82–86`). Log is staged **with** the outbox event then saved (`127–136`) — slightly tighter than the audit’s “write log after publish,” but the burn vs Communications is unchanged. Missing template is a comms no-op:

```145:154:apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs
        else if (isInvoiceReminder)
        {
            var template = await _repository.GetTemplateByNameAsync(@event.OrganizationId, "Invoice Reminder");
            if (template == null)
            {
                _logger.LogWarning(
                    "Invoice reminder skipped: template missing. OrganizationId={OrganizationId} SessionId={SessionId}",
                    @event.OrganizationId, subIdStr);
                return;
            }
```

`InvoiceReminderJobTests.Day0Due_OpenCustom_SendsOnce` still only asserts `checkout_url` (`InvoiceReminderJobTests.cs:78–81`). Grep of Communications tests finds **zero** `invoice.reminder` hydrator cases.

168 added `MissingWorkspaceSlug_DoesNotDispatchOrLog` (slug fail-closed) — that is not currency / SST / template-burn.

### Related files
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Workers/InvoiceReminderJob.cs` — producer, MYR, offsets, dispatch log.
- `apps/lazuar-api/Modules/Communications/Infrastructure/EventHandlers/FulfillmentRequestedIntegrationEventHandler.cs` — invoice.reminder branch; returns on missing template.
- `apps/lazuar-api/Modules/Communications/Domain/DefaultMessageTemplates.cs` — Invoice Reminder body prints `{{amount}} {{currency}}` (lines 97–104).
- `apps/lazuar-api/Modules/Commerce/Application/Commands/InitiateCheckoutCommandHandler.cs` — hop-1 custom charge is the same line-sum / MYR story; keep consistent if you add SST.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/Workers/InvoiceReminderJobTests.cs` — URL + skip cases only.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Communications/DunningTemplateVariableSubstitutionTests.cs` — dunning hydrate only.

### Tests
- Existing: `InvoiceReminderJobTests.Day0Due_OpenCustom_SendsOnce`, `MissingWorkspaceSlug_DoesNotDispatchOrLog`, `Completed_IsSkipped`, `ProductSession_IsIgnored`. `DefaultMessageTemplatesTests.Catalog_IncludesLifecycleAndDocumentTemplatesOnly` only asserts the name exists.
- None would fail if currency stayed `"MYR"`, amount omitted SST, or Communications dropped the mail after the log committed. `Day0Due_OpenCustom_SendsOnce` would still pass.
- First regression: payload `currency` equals the session/quote currency (not a literal MYR) and `amount` equals whatever hop-1 actually charged (including SST if quotes gain it). Second: missing “Invoice Reminder” template → **no** dispatch log row (or a retryable failed log), so the next hour can send. Third: Communications test `EventType == "invoice.reminder"` substitutes `checkout_url`, `due_at`, amount, currency.

### Reproduction today
Arrange: OPEN custom checkout, `DueAt` = today UTC, one line `1 × 100`, workspace slug present, delete the tenant’s Invoice Reminder template. Act: run `InvoiceReminderJob.RunOnceAsync` (or wait the hourly tick). Assert: `invoice.reminder` outbox payload has `currency = "MYR"` and `amount = 100`; `InvoiceReminderDispatchLogs` has dayOffset 0; Communications handler logs “template missing” and sends nothing. Re-run the job: already-logged, still no mail. With template present, buyer sees “100.00 MYR” even if the tenant’s checkout currency is not MYR.

### Blast radius
Quote / AR buyers (custom sessions only; product checkouts ignored). Wrong currency is a teachability / trust bug; missing-template burn is a missed collection email the merchant cannot replay without deleting the log. No double-charge. Frequency: three UTC days per open quote (−3/0/+3). Still **P2**. SST lie is currently consistent with hop-1 custom (no SST on `AdHocLineItem`).

### Suggested fix
1) Pass the session’s real currency (or `"MYR"` only if the quote is actually MYR). 2) Do not insert `InvoiceReminderDispatchLog` until Communications has a template, **or** write the log as a claim and delete/mark failed if hydrate returns “no template” so the next tick retries. 3) Add the missing hydrator test. Do not add SST here unless hop-1 custom charge also gains it (otherwise reminder ≠ charge). Do not mark the session `PAST_DUE`. No TypeSpec, no WhatsApp.

### Evaluation notes
**166** (no claim) and **168** (GetService fail-open / slug) are siblings on this job; 168’s slug skip is already in tree. **049** is subscription reminder-log-before-send (different job). Do not fold those into this ticket. Still P2. Not blocked by 292.

