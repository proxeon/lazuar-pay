---
number: "236"
id: B05-L32
severity: P2
status: resolved
resolved_branch: fix/236-ledger-document-404
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 236 — B05-L32 — `LedgerLine` and `CreditLedger` have no `OrganizationId`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/236-ledger-document-404`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L32 — P2 — `LedgerLine` and `CreditLedger` have no `OrganizationId`

Not `IMustHaveTenant`. No global filter on the child table. `GetLedgerEntriesAsync` loads lines by `LedgerEntryId = ANY(@EntryIds)` after the header query filtered by org — safe on that path. A future raw `FROM billing.LedgerLines` without a join is a cross-tenant read. `CreditLedgers` history is loaded by `TenantCreditBalanceId` from an org-scoped wallet — safe on that path.

Admin document download (`AdminLedgerEndpoints:36-46`) does not load the ledger row at all; it presigns `vault/{ctx.TenantId}/documents/{id}.pdf`. Guessing another org’s entry id looks in **your** prefix. Not an IDOR on their PDF. Guessing your own missing id returns a signed 404-from-R2.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`LedgerLine` and `CreditLedger` are still plain `Entity`s: no `OrganizationId`, not `IMustHaveTenant`, no global query filter on the child table. Tenant isolation is only by join/parent. `GetLedgerEntriesAsync` loads headers with `WHERE e.OrganizationId = @OrgId` then `FROM billing.LedgerLines WHERE LedgerEntryId = ANY(@EntryIds)` — safe on that path because ids came from the filtered header query. `GetFinancialSummaryAsync` joins lines to entries and filters the entry org. Credit history loads `CreditLedgers` by `TenantCreditBalanceId` from an org-scoped wallet — safe on that path. A future raw `FROM billing.LedgerLines` (or a worker with empty ambient and `DbSet<LedgerLine>`) is a cross-tenant read. Admin PDF download still does not load the ledger row: it presigns `vault/{ctx.TenantId}/documents/{id}.pdf`. Guessing another org’s entry id looks under **your** prefix (not their PDF). Guessing a missing id is a signed R2 404.

### Still present?
**STILL BROKEN**

```6:8:apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs
public class LedgerLine : Entity
{
    public Guid Id { get; private set; }
```

```6:9:apps/lazuar-api/Modules/Billing/Domain/Entities/CreditLedger.cs
public class CreditLedger : Entity
{
    public Guid Id { get; private set; }
    public Guid TenantCreditBalanceId { get; private set; }
```

`BillingDbContext` maps `LedgerLines` / `CreditLedgers` with no tenant index or filter (`:77-83`, `:138-142`). Live read path is still id-only after an org-scoped header:

```92:95:apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs
        var linesSql = @"
            SELECT ""Id"", ""LedgerEntryId"", ""AccountType"", ""Amount"", ""Currency"", ""BaseCurrencyAmount"", ""BaseCurrency""
            FROM billing.""LedgerLines""
            WHERE ""LedgerEntryId"" = ANY(@EntryIds)";
```

Document download unchanged:

```36:46:apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs
        admin.MapGet("/ledger/{id:guid}/document", Task<Ok<DocumentDownloadUrlDto>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var key = $"vault/{ctx.TenantId}/documents/{id}.pdf";
            var downloadUrl = r2Service.GetPresignedDownloadUrl(bucket, key, 5);
```

`TenantIsolationArchitectureTests` require `IMustHaveTenant` on `MessageDeliveryLog` (179) but do **not** mention `LedgerLine` / `CreditLedger`.

### Related files
- [`apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs`](apps/lazuar-api/Modules/Billing/Domain/Entities/LedgerLine.cs) — child without org.
- [`apps/lazuar-api/Modules/Billing/Domain/Entities/CreditLedger.cs`](apps/lazuar-api/Modules/Billing/Domain/Entities/CreditLedger.cs) — wallet movement without org.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/BillingDbContext.cs) — no filter on those tables.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs) — current safe join/id paths (`GetLedgerEntriesAsync`, `GetFinancialSummaryAsync`, `GetCreditBalanceWithHistoryAsync`).
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints/AdminLedgerEndpoints.cs) — presign without loading the row.
- [`apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs`](apps/lazuar-api/tests/Lazuar.ArchitectureTests/TenantIsolationArchitectureTests.cs) — 179 allow-list; does not cover these children.
- [`apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs`](apps/lazuar-api/tests/Lazuar.IntegrationTests/BillingQueryServiceTests.cs) — creates `LedgerLines` without an org column.

### Tests
- Existing: `BillingQueryServiceTests` (summary polarity on live-or-ignore Postgres); architecture `MessageDeliveryLog_Is_IMustHaveTenant_PaymentWebhookLog_Is_Allowlisted` (not these types). No test that `LedgerLine` implements `IMustHaveTenant`. No IDOR test on `/ledger/{id}/document`.
- No current test fails while the children lack `OrganizationId`. The live query paths are org-safe.
- First regression: architecture assert that every `billing` table used from a raw SQL string either has `OrganizationId` in the WHERE or joins `LedgerEntries`/`TenantCreditBalances` on org. Optional: document download must 404 unless a header exists for `ctx.TenantId` (does not require putting org on the line table).

### Reproduction today
As Org A, `GET /admin/billing/ledger` — lines come back only for A’s headers (safe). As Org A, `GET /admin/billing/ledger/{orgB-entry-id}/document` — JSON `{ url }` for `vault/{A}/documents/{orgB-id}.pdf`; R2 404, not B’s PDF. A new Dapper report that `SELECT * FROM billing.LedgerLines` without a join would see every tenant.

### Blast radius
Not an IDOR on the current admin/query paths. Risk is the next editor’s raw SQL or an unfiltered `DbSet<LedgerLine>`. Credit history is wallet-scoped. Money/PII: line amounts are financial; credit ledger references can mention gateway txs. Frequency: none until a new query. Still P2 (latent / hygiene), sibling of resolved **179** (messaging delivery log), not the same table.

### Suggested fix
Smallest useful change is **not** a wide migration unless you are already touching the snapshot: add an architecture allow-list comment that `LedgerLine`/`CreditLedger` are children and must only be queried via parent id + org-scoped header. If you do migrate, copy `OrganizationId` onto both tables, add `IMustHaveTenant`, backfill from the parent, and filter. Separately, load the ledger header in `AdminLedgerEndpoints` before presigning (404 if `entry.OrganizationId != ctx.TenantId`) — that closes the guess-id signed-404 footgun without a schema change. No TypeSpec. No Stripe.

### Evaluation notes
009 description still exact. 161–200 fail-closed scoped **headers** (`HasEntryBeenProcessed` now takes org — **081**) and unique key now includes org (**080**); children were left. Still P2. Not blocked. Do not confuse with 179’s resolved `MessageDeliveryLog` work.

