---
number: "229"
id: B05-L25
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 229 — B05-L25 — Document year is UTC, not MYT

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L25 — P2 — Document year is UTC, not MYT

`DocumentSeries.Prefix` uses `DateTime.UtcNow`. Consolidation periods are MYT. A 1 Jan 02:00 MYT sale can be `RCPT-2025-#####` and fall in the 2026-01 consolidation month. Ugly, not a cent-wrong.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Customer-facing series (`RCPT-` / `INV-` / `CN-` / `QT-`) bake the calendar year into the sequence prefix via `DocumentSeries.Prefix` → `$"{series}-{(utcNow ?? DateTime.UtcNow):yyyy}"`. Malaysia is UTC+8. From 16:00 UTC on 31 Dec through 15:59 UTC on 1 Jan it is already (or still) the next (or previous) calendar day in MYT. A sale at 1 Jan 02:00 MYT (31 Dec 18:00 UTC) mints `RCPT-2025-#####` while `B2cConsolidationJob` buckets that row into the **2026-01** MYT month (`Asia/Kuala_Lumpur`). The receipt year and the consolidation month disagree. Amounts are correct. LHDN XML year-of-issue can also disagree with the printed number if anyone later uses the prefix as the legal year. Ugly, not a cent-wrong.

### Still present?
**STILL BROKEN**

```16:22:apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs
    public static string Prefix(string series, DateTime? utcNow = null) =>
        $"{series}-{(utcNow ?? DateTime.UtcNow):yyyy}";

    public static string ReceiptPrefix(DateTime? utcNow = null) => Prefix(Receipt, utcNow);
    public static string QuotePrefix(DateTime? utcNow = null) => Prefix(Quote, utcNow);
    public static string InvoicePrefix(DateTime? utcNow = null) => Prefix(Invoice, utcNow);
    public static string CreditNotePrefix(DateTime? utcNow = null) => Prefix(CreditNote, utcNow);
```

Callers pass no clock and get UTC now:

```101:113:apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
                var receiptNumber = await _mediator.Send(
                    new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.ReceiptPrefix()), ct);
                // ...
                var invoiceNumber = await _mediator.Send(
                    new GenerateNextSequenceNumberCommand(@event.OrganizationId, DocumentSeries.InvoicePrefix()), ct);
```

Same UTC prefix on refunds (`GatewayRefundCompletedHandler.cs:93`) and manual enroll (`ManualSubscriberEnrolledIntegrationEventHandler.cs:66, 72`). Consolidation is MYT:

```20:20:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
    private static readonly TimeZoneInfo MalaysiaTimeZone = ResolveMalaysiaTimeZone();
```

```99:100:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone);
        var currentMonthStartMyt = new DateTime(nowMyt.Year, nowMyt.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
```

`ResolveMalaysiaTimeZone` already handles Linux (`Asia/Kuala_Lumpur`) vs Windows (`Singapore Standard Time`) (`B2cConsolidationJob.cs:346-355`). `DocumentSeries` does not reuse it. `DocumentSeriesTests.Prefix_BakesYearIntoSeries` passes a UTC `2026-08-16` and expects `RCPT-2026` — it does **not** pin NYE.

### Related files
- `apps/lazuar-api/Modules/Billing/Contracts/DocumentSeries.cs` — UTC year.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Commands/GenerateNextSequenceNumberCommandHandler.cs` — allocates `{Prefix}-{value:D5}` (prefix is already year-stamped).
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — RCPT / INV mint.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayRefundCompletedHandler.cs` — CN mint.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` — INV/RCPT.
- `apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs` — MYT period SSoT + timezone helper to copy.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/DocumentSeriesTests.cs` — UTC fixture.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/Workers/B2cConsolidationJobTests.cs` — MYT catch-up.

### Tests
- Existing tests: `DocumentSeriesTests.Prefix_BakesYearIntoSeries` (UTC 2026-08-16). `DocumentSeriesTests.CustomerFacingNumber_NeverUsesRawUuid`. Consolidation tests convert `DateTime.UtcNow` to MYT but do not pair a receipt prefix with that month.
- Whether any test would fail if the bug is still there: **no**. The UTC test would fail if you switched the default clock to MYT **and** kept the same fixture without converting.
- What a first regression test should assert: `DocumentSeries.ReceiptPrefix(new DateTime(2025, 12, 31, 18, 0, 0, DateTimeKind.Utc))` is `RCPT-2026` (1 Jan 02:00 MYT). Mid-year UTC still matches. Reuse `B2cConsolidationJob`’s TZ resolver (extract to a shared helper) so Linux/Windows CI both pass.

### Reproduction today
Arrange: freeze or pick a payment whose `DateTime.UtcNow` is `2025-12-31T18:00:00Z`. Act: complete a B2C GMV payment so `GatewayPaymentCompletedHandler` mints a receipt. Assert `CustomerDocumentNumber` starts with `RCPT-2025`. The same row’s timestamp in MYT is 2026-01-01 02:00 and `B2cConsolidationJob` January 2026 catch-up will pick it up. Ugly pairing, amounts unchanged.

### Blast radius
Every commercial number minted in the UTC/MYT year gap (16:00–23:59 UTC on 31 Dec, and the symmetric first eight hours of 1 Jan UTC still in the previous MYT year — wait: 1 Jan 00:00–07:59 UTC is 1 Jan 08:00–15:59 MYT, same year. The mismatch window is **31 Dec 16:00–23:59 UTC = 1 Jan 00:00–07:59 MYT** (prefix still 31 Dec UTC year, consolidation already January). Merchants who reconcile “RCPT-YYYY” to LHDN monthly cons will see one night of stragglers. Not a cent-wrong. Not PII. Frequency: once a year per active merchant, plus any backdated test clocks.

### Suggested fix
Change `DocumentSeries.Prefix` to convert `utcNow ?? DateTime.UtcNow` through the same `Asia/Kuala_Lumpur` / `Singapore Standard Time` helper `B2cConsolidationJob` already uses (extract `MalaysiaTime` to `Modules.Billing.Domain` or `Contracts` so Billing does not grow a second TZ table). Keep the optional `utcNow` parameter so tests inject. Do not invent a second sequence per year-gap. Do not change LHDN XML issue date here (slice 06). No TypeSpec. LP-059 irrelevant.

### Evaluation notes
Duplicates: none numbered. Related 079 (sequence in ledger txn) does not touch the year. Severity still **P2**. Not blocked. Residual after 197 (`cycle-key-utc` documented SSoT for **Commerce** cycle dates): Billing documents stay MYT-for-cons / UTC-for-prefix — two clocks, still.


