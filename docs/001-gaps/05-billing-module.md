<!-- Source subagent: 019fc650-3511-7762-8927-4f321f611fc4 -->
<!-- Full uncondensed subagent analysis — do not summarize -->

# Billing Module Gap Analysis

**Scope:** `/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/` (Domain, Application, Contracts, Infrastructure), plus prepaid wallet, double-entry ledger, document generation, workers, and cross-module consumers (Lhdn, Messaging, Communications, Payments).

**Method:** Source-file inventory (not README claims alone). Evidence quoted from actual code.

---

## Module Inventory & Architecture

### Stated purpose

`README.md` positions Billing as the **financial truth** layer:

- Double-entry ledger (`LedgerEntry` / `LedgerLine`)
- Revenue recognition (`DeferredRevenueSchedule`)
- Net profit after gateway fees & commissions
- Tax liability tracking
- AR/AP, LHDN status prep
- Explicit non-goals: no gateway integration, no access control, no cross-schema joins

### Actual layout

| Layer | Role | Notable contents |
|--------|------|------------------|
| **Domain** | Aggregates/entities | `LedgerEntry`, `LedgerLine`, `DeferredRevenueSchedule`, `TenantCreditBalance`, `CreditLedger`, `CreditHold`, `CreditDeductionIdempotencyLog`, `TenantBillingProfile`, `DocumentSequence`, `TenantBillingAddress` |
| **Application** | Thin | `ILedgerRepository`, draft-doc query, agent health query, empty `DependencyInjection`, LLM prompt provider |
| **Contracts** | Public surface | Commands (credit deduct/hold/clawback, sequence, profile, document), events (invoice/commission/consolidate/document/manual payment), `IBillingQueryService`, `ICreditCostService` |
| **Infrastructure** | Everything else | EF `BillingDbContext` (`billing` schema), command handlers, 13 event handlers, 4 workers, QuestPDF docs, Dapper query service, endpoints, 3 migrations |

### Architectural shape (as implemented)

```
Payments / Commerce / Lhdn / One  --events-->  Billing Inbox  --> handlers  --> Ledger + Wallet
Other modules  --MediatR commands-->  Deduct/Reserve/Clawback
Billing  --outbox-->  DocumentPublished, ConsolidatedInvoiceIssued
```

**Boundary drift vs README:**

1. README says Billing is a terminal sink that “does *not* publish events that trigger side-effects.” In reality it publishes:
   - `DocumentPublishedIntegrationEvent` (Communications consumes)
   - `ConsolidatedInvoiceIssuedIntegrationEvent` (Lhdn submits e-invoice)

2. README: “No Cross-Schema Joins.” Document generation and draft PDF **query `commerce` and `crm` schemas via Dapper**.

3. Application layer is nearly empty; handlers live in Infrastructure (common in this repo, but Application DI is a no-op).

4. Dual financial models that never reconcile:
   - **Accounting ledger** (`LedgerEntry`/`LedgerLine`) — double-entry style
   - **Utility wallet** (`TenantCreditBalance`/`CreditLedger`) — integer prepaid units, *not* double-entry accounts

---

## Domain Model (Ledger, Credits, Holds, Sequences, Deferred Revenue)

### `LedgerEntry` + `LedgerLine` (core ledger)

Evidence of balance invariant:

```51:64:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Domain/Aggregates/LedgerEntry.cs
    // This guarantees that it is impossible for Lazuar to lose track of a
    // single cent.
    // 
    // NOTE: Double-entry bookkeeping is a 500-year-old accounting rule: Every
    // financial transaction has equal and opposite reactions. Debits and
    // Credits must always equal zero.
    public void ValidateBalanced()
    {
        var netBaseAmount = _lines.Sum(l => l.BaseCurrencyAmount);
        if (netBaseAmount != 0)
        {
            throw new InvalidOperationException($"Ledger entry {Id} is unbalanced. Net base currency amount: {netBaseAmount}");
        }
    }
```

**What works**

- Aggregate root + child lines; unique index on `(ReferenceType, ReferenceId)` for event idempotency at DB level.
- Sign convention: cash/fees positive (debit-like), revenue/tax/liability negative (credit-like) in handlers.
- `ValidateBalanced()` called in most write handlers.
- Partial immutability: `BillingDbContext.SaveChangesAsync` forces `LedgerLine`/`CreditLedger` `Modified` → `Added` (append-only intent).

**Gaps**

| Gap | Detail |
|-----|--------|
| No chart of accounts type | Account types are free-form strings (`"REVENUE_GROSS"`, etc.); typos compile. |
| Balance only on base currency | Original currency lines not validated independently. |
| No period close / freeze | Ledger entries remain mutable for LHDN status (OK), but no hard immutability for amounts after post. |
| No account enum / constants class | Scattered magic strings across handlers, queries, PDF, B2C job. |
| FX | Relies on event `FxRate`; no FX gain/loss account. |
| No AR settlement path | `InvoiceIssued` books AR + deferred; no payment application entry exists (and `ManualPaymentRecorded` is dead). |

### `TenantCreditBalance` + `CreditLedger` (prepaid wallet)

```17:63:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Domain/Aggregates/TenantCreditBalance.cs
    /// Current available credits. Deduction throws on insufficient balance; the wallet's
    /// xmin system column provides optimistic concurrency (configured in BillingDbContext)
    /// so concurrent deductions cannot overdraw the wallet.
    public int AvailableCredits { get; private set; }
    ...
    public void Deduct(int credits, string reference)
    {
        if (credits <= 0) throw new ArgumentException("Deduction amount must be positive.");
        if (AvailableCredits < credits)
            throw new BusinessRuleValidationException(
                new GenericBusinessRule($"402: Insufficient credits. Available: {AvailableCredits}, requested: {credits}."));
        AvailableCredits -= credits;
        _transactions.Add(new CreditLedger(Id, -credits, reference));
        ...
    }
```

**Strengths:** sufficiency check, append-only ledger rows, clawback clamps at zero, unique wallet per org, `xmin` row version.

**Gaps:**

- Wallet is **not** integrated into the double-entry ledger (top-ups sometimes create a separate `SYSTEM_CREDIT_TOPUP` entry, deductions never do).
- No liability account for “unearned prepaid utility credits sold by platform.”
- Integer credits only; no multi-currency wallet.
- No expiry, no hold amount reflected in `AvailableCredits` vs “held” reporting (holds *do* deduct immediately, so balance is correct, but no “held credits” metric API).

### `CreditHold`

Reserves by **deducting wallet first**, tracks remainder, `Consume` / `ReleaseRemaining` → status `SETTLED`.

**Gaps:**

- Comment documents status `RELEASED`; code only ever sets `SETTLED`.
- Index on `(OrganizationId, CorrelationId)` is **non-unique** → double reserve possible for same correlation.
- No domain path to cancel/void a hold without release.
- **No production callers** of `ReserveCreditsCommand` / `ConsumeCreditHoldCommand` / `ReleaseCreditHoldCommand` after Communications “broadcasts free” pivot (holds are orphaned infrastructure).

### `DeferredRevenueSchedule`

Day-linear recognition with statuses `PENDING` / `RECOGNIZING` / `COMPLETED`.

**Critical gap:** `ILedgerRepository.AddDeferredRevenue` exists, but **no production code calls it**. Grep shows only repository definition + job read. **Revenue recognition is dead infrastructure.**

`InvoiceIssuedHandler` books:

```32:33:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs
        entry.AddLine("ASSET_ACCOUNTS_RECEIVABLE", @event.Amount, @event.Currency, @event.Amount, @event.Currency);
        entry.AddLine("LIABILITY_DEFERRED_REVENUE", -@event.Amount, @event.Currency, -@event.Amount, @event.Currency);
```

…but never creates a schedule, never uses invoice due dates / service period. Gateway payments book **immediate** `REVENUE_GROSS`, not deferred — no subscription period amortization.

### `DocumentSequence`

Domain entity is a bag of fields only (no `Next()`). Real sequencing is raw SQL upsert in `GenerateNextSequenceNumberCommandHandler` (good for concurrency). Domain entity is effectively unused as behavior.

### `TenantBillingProfile`

Minimal legal identity + owned address. Used by PDF generation. No validation of Malaysian TIN format, SST number, state codes.

### `CreditDeductionIdempotencyLog`

Unique `(OrganizationId, IdempotencyKey)` — solid primitive for wallet deductions.

---

## Command/Query Surface

### Commands (Contracts → Infrastructure handlers)

| Command | Handler | Notes |
|---------|---------|--------|
| `DeductTenantCreditCommand` | Yes | Idempotency + concurrency retries (3) |
| `ClawbackCreditsCommand` | Yes | **No** concurrency retry, **no** idempotency |
| `ReserveCreditsCommand` | Yes | No correlation uniqueness / no external use |
| `ConsumeCreditHoldCommand` | Yes | No concurrency retry, no idempotency |
| `ReleaseCreditHoldCommand` | Yes | Status short-circuit if not `HELD` |
| `GenerateNextSequenceNumberCommand` | Yes | Atomic SQL |
| `GenerateAndStoreDocumentCommand` | Yes | QuestPDF + R2 + outbox event |
| `UpdateTenantBillingProfileCommand` | Yes | Upsert |

### Queries / read ports

| Surface | Impl |
|---------|------|
| `IBillingQueryService` | Dapper `BillingQueryService` |
| `GenerateDraftDocumentQuery` | Cross-schema commerce/crm read + PDF |
| `GetFinancialHealthAgentQuery` | Thin wrapper over financial summary |
| `ICreditCostService` | Config-bound singleton |

### Missing command/query capabilities (product/ADR expectations)

- No “post journal entry” / manual adjustment command
- No AR payment application / `ManualPaymentRecorded` handler
- No refund of credits on top-up cancel (only dispute clawback of units)
- No hold status query API
- TypeSpec `GET /admin/billing/net-profit` **not implemented**
- TypeSpec `getFinancialSummary(from_date?, to_date?)` — **dates ignored** in endpoint

---

## Event Handlers & Cross-Module Coupling

### Registered subscriptions (`UseBillingSubscriptions`)

| Event | Handler | Source module |
|-------|---------|---------------|
| `GatewayPaymentCompletedIntegrationEvent` | `GatewayPaymentCompletedHandler` + `PlatformTopUpEventHandler` | Payments |
| `GatewayRefundCompletedIntegrationEvent` | `GatewayRefundCompletedHandler` | Payments |
| `GatewayDisputeCreatedIntegrationEvent` | `ChargebackClawbackHandler` | Payments |
| `InvoiceIssuedIntegrationEvent` | `InvoiceIssuedHandler` | Billing contracts (no in-repo publisher found) |
| `CommissionAccruedIntegrationEvent` | `CommissionAccruedHandler` | Billing contracts (no publisher found) |
| `LhdnDocumentValidated/Cancelled/Submitted` | three handlers | Lhdn |
| `ZeroAmountCheckoutCompletedIntegrationEvent` | `ZeroAmountCheckoutHandler` | Commerce |
| `ManualSubscriberEnrolledIntegrationEvent` | `ManualSubscriberEnrolled…` | Commerce |
| `AppEntitlementGrantedIntegrationEvent` | `StarterCreditSeederHandler` | One |

### Dead / orphaned handlers & events

| Item | Problem |
|------|---------|
| `ApiCreditPurchasedHandler` | **Not registered** in DI/subscriptions; parallel of platform top-up left dead |
| `ManualPaymentRecordedIntegrationEvent` | Contract only — **no handler, no publisher** |
| `InvoiceIssuedIntegrationEvent` | Handler + Lhdn consumer exist; **no publisher** in solution |
| `CommissionAccruedIntegrationEvent` | Handler only; **no publisher** (affiliate module removed/not present) |

### Dangerous dual-path: LHDN credit deduction

1. **On submit command** (`SubmitTaxDocumentCommand`): deducts config cost (`LhdnSubmit: 3`) via `DeductTenantCreditCommand` with idempotency key.
2. **On submission job event** (`LhdnDocumentSubmittedIntegrationEventHandler`): **always deducts hardcoded `1`** with no idempotency:

```21:33:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/LhdnDocumentSubmittedIntegrationEventHandler.cs
    public async Task HandleAsync(LhdnDocumentSubmittedIntegrationEvent @event)
    {
        if (@event.IsTestMode) return;
        var wallet = await _dbContext.TenantCreditBalances...
        if (wallet != null)
        {
            wallet.Deduct(1, $"LHDN Submission: {@event.InternalReferenceId}");
            await _dbContext.SaveChangesAsync();
        }
    }
```

**Result:** live LHDN submissions can charge **3 + 1 = 4 credits**, second path has no concurrency retry and no idempotency log. Silent skip if wallet missing (no fail-closed).

### Dual recording: utility top-up cash

Both handlers fire on `GatewayPaymentCompleted`:

1. `GatewayPaymentCompletedHandler` → `GATEWAY_PAYMENT` with revenue/tax/fee lines (merchant sale semantics).
2. `PlatformTopUpEventHandler` → wallet top-up + `SYSTEM_CREDIT_TOPUP` (`EXPENSE_SOFTWARE_SUBSCRIPTION` / `ASSET_CASH` outflow).

Same gateway txn becomes **two ledger entries with different economic interpretations**. Chargeback only claws **wallet units**, does not reverse either ledger entry.

### Document generation coupling

`GenerateAndStoreDocumentCommandHandler` joins:

```sql
FROM commerce."TransactionLogs"
```

Draft query joins `commerce.CheckoutSessions` + `crm.ClientProfiles`. Violates module isolation / README.

### Consumers of Billing

| Consumer | Usage |
|----------|--------|
| Lhdn | `HasSufficientCredits` + `DeductTenantCredit` |
| Messaging | WhatsApp cost check + deduct |
| Communications | credit preview (costs hard-zeroed for broadcasts) |
| Lhdn | `ConsolidatedInvoiceIssued` → `SubmitTaxDocumentCommand` |
| Communications | `DocumentPublished` → outbound messaging |

---

## Workers (Outbox, Inbox, Revenue Recognition, B2C Consolidation)

| Worker | Base | Behavior | Health |
|--------|------|----------|--------|
| `BillingInboxConsumerJob` | `InboxConsumerJob<BillingDbContext>` | Standard inbox drain | OK scaffolding |
| `BillingOutboxPublisherJob` | `OutboxPublisherJob<BillingDbContext>` | Standard outbox publish | OK scaffolding |
| `RevenueRecognitionJob` | Hourly poll | Loads non-`COMPLETED` schedules, posts recognition entries | **No schedules ever created → permanent no-op** |
| `B2cConsolidationJob` | Monthly 28th 02:00 MYT | Groups B2C lines, publishes consolidated invoice event | **Filter bug makes primary path empty** |

### B2C consolidation critical bug

Job selects:

```81:84:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/Workers/B2cConsolidationJob.cs
        var uninvoicedEntries = await db.LedgerEntries
            .Include(e => e.Lines)
            .Where(e => e.CustomerType == "B2C" && e.LhdnValidationStatus == null)
            .ToListAsync(ct);
```

But the main payment path **immediately** sets:

```69:69:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs
            entry.UpdateLhdnStatus(receiptNumber, "B2C_RECEIPT");
```

Same for manual enrollment. So legitimate B2C sales with receipts are **never eligible** for consolidation. ADR 021’s “28th consolidated LHDN invoice” story is currently **non-functional** for the happy path.

Additional B2C issues:

- Loads **all historical** null-status B2C entries (no period window) — if fixed filter, could re-include ancient junk.
- Daily `alreadyRan` keyed on any `TaxInvoiceId` starting `B2C-CONS-` **today** (UTC date), not per-org month.
- Uses `ABS` on amounts (sign blindness).
- Does not net refunds / cancellations into consolidated totals.
- Consolidation event idempotency key in Lhdn is a **new Guid every time** (`ConsolidatedInvoiceIssuedIntegrationEventHandler`) — weak for retries.

### Revenue recognition job issues

- Hourly reference id `{scheduleId}_{yyyyMMddHH}` — if job fails mid-batch after partial save, retries same hour may hit unique `(ReferenceType, ReferenceId)` or skip inconsistently depending on `Recognize` state.
- No tenant scoping / batching / advisory locks for multi-instance hosts.
- Floating-point day math (`double` elapsed fraction) for money.

---

## Endpoints

### Implemented (`Endpoints.cs`)

**Admin** (`/admin/billing`, `OrgAdmin`):

| Method | Path | Notes |
|--------|------|--------|
| GET | `/ledger` | Paginated, filters |
| GET | `/ledger/{id}/document` | R2 presign by convention path; **no ownership check that PDF exists** |
| GET | `/summary` | All-time only |
| GET | `/credits` | Balance + last 50 txs |
| GET | `/credits/packages` | Config packages |
| POST | `/credits/top-up` | Min RM50, Payments system checkout |
| GET/PUT | `/profile` | Billing profile |

**Public** (`/public/billing`):

| Method | Path | Notes |
|--------|------|--------|
| GET | `/{tenantSlug}/profile` | Via One slug lookup |
| GET | `/{tenantSlug}/documents/{ledgerEntryId}?sig&exp` | HMAC link (Jwt secret) |
| GET | `/{tenantSlug}/documents/draft/{sessionId}` | Proforma PDF |

### Contract gaps (TypeSpec vs runtime)

| TypeSpec | Runtime |
|----------|---------|
| `GET /net-profit` | **Missing** |
| Summary `from_date` / `to_date` | **Not wired** |
| Public signed document route | Present in code, **absent from TypeSpec routes** |
| Document download for admin | Present both sides |

Security notes:

- Document download builds key `vault/{tenantId}/documents/{id}.pdf` without verifying ledger entry belongs to tenant in admin path (tenant from auth context only — OK if IDs unguessable + R2 private; still no existence check).
- HMAC uses `Jwt:Secret` for document links (shared secret purpose coupling).

---

## Credit Deduction & Idempotency

### What is solid

`DeductTenantCreditCommandHandler`:

- Optional `IdempotencyKey` with unique DB index
- Re-check key every attempt
- `xmin` optimistic concurrency, clear tracker, max 3 attempts
- Domain throws on insufficient funds

Used correctly by Messaging (event id as key) and Lhdn submit (lhdn:idempotencyKey).

### Gaps / bugs

| Area | Issue | Severity |
|------|-------|----------|
| LHDN double deduct | Command path (3) + event handler (1, no idempotency) | **Critical** |
| LHDN submit swallows deduct failure | Document saved; credits may never bill | High |
| Messaging deduct after send | Send first, deduct second; failure only logged → free sends | High (product choice?) |
| `ClawbackCreditsCommand` | No concurrency retry; no idempotency; dispute replay risk | High |
| `ReserveCredits` | No unique correlation; no idempotency key | Medium |
| `ConsumeCreditHold` | No retry on xmin conflict | Medium |
| Optional null idempotency key | Callers can omit → non-idempotent deduct | Medium |
| Starter grant | Idempotent only if wallet exists; top-up creates wallet first → race could skip grant elsewhere | Low |
| Package matching on top-up | Highest package with `AmountMyr <= AmountPaid` — overpay grants next lower tier only, under-config amounts grant 0 | Medium |
| Chargeback amount | Uses `AmountDisputed` vs packages, not original granted credits stored on ledger | Medium |
| `LhdnDocumentSubmitted` | If wallet null, silently no-op | Medium |

### Credit holds vs product reality

Broadcast endpoints hardcode `CreditsPerRecipient = 0` and pass broadcast id as hold bypass. Hold subsystem is **inventory without product demand**.

---

## Financial Correctness Risks

### 1. Financial summary uses `ABS` on signed amounts (systemic)

```129:142:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/Services/BillingQueryService.cs
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'REVENUE_GROSS' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Gross_revenue"",
                ...
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'LIABILITY_DEFERRED_REVENUE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Deferred_revenue"",
```

With signed double-entry:

- Refunds / cancellations **inflate** gross, fees, tax (ABS of reverse lines).
- Deferred + recognized **double-count** once recognition posts opposite-signed liability lines.
- Net revenue formula also subtracts ABS of refunds *and* uses ABS gross — not a pure signed sum.

Integration test only seeds unidirectional sale lines; does not cover refunds/recognition.

### 2. Refunds do not reverse tax liability

`GatewayRefundCompletedHandler` balances cash/contra revenue/fee only — **no** `LIABILITY_TAX_PAYABLE` reversal. Tax summary stays high after refunds.

### 3. Zero-amount checkout balance depends on equality

```32:38:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ZeroAmountCheckoutHandler.cs
            entry.AddLine("EXPENSE_DISCOUNT", @event.DiscountAmount, ...);
            entry.AddLine("REVENUE_GROSS", -@event.OriginalAmount, ...);
        entry.ValidateBalanced();
```

Requires `DiscountAmount == OriginalAmount`. Mismatch throws and blocks inbox processing.

### 4. Gateway payment balance assumes `NetAmount + GatewayFee == AmountPaid`

No assertion of that invariant before post; bad gateway payloads fail at `ValidateBalanced` or post wrong economics if fee fields inconsistent with tax.

### 5. Top-up dual ledger pollution

Platform credit purchase posts merchant-style `GATEWAY_PAYMENT` **and** tenant software expense entry. Financial summary mixes creator GMV with Lazuar SaaS spend unless filtered — test documents ignoring `EXPENSE_SOFTWARE_SUBSCRIPTION` for net, but gross still includes any top-up path if misclassified as revenue on the first entry.

### 6. Manual enrollment books full cash = revenue, no tax/fee

```38:39:/Users/akmalfirdaus/Code/lazuar/lazuar-hub/apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs
        entry.AddLine("ASSET_CASH", @event.AmountPaid, ...);
        entry.AddLine("REVENUE_GROSS", -@event.AmountPaid, ...);
```

Tax-inclusive amounts would mis-state tax liability.

### 7. Cancellation reverse entry shares same `ReferenceId` under different type

OK for uniqueness; original entry status updated to CANCELLED. Cancellation of already-recognized deferred revenue not modeled.

### 8. No AR cash application / affiliate payout

Commission books payable; no payment settlement entry. Invoice AR never clears.

### 9. B2C consolidation broken (see workers)

Compliance path for monthly consolidated e-invoice does not see receipt-stamped sales.

### 10. Document totals from ledger lines

PDF takes only `REVENUE_GROSS`/`REVENUE_RECOGNIZED` as line items; multi-line SKUs from payment event `LineItems` are **ignored** (event carries line items unused by Billing handler).

---

## Gaps vs Stated Double-Entry Vision

| Vision (README / ADR 014 / 021) | Reality | Gap |
|----------------------------------|---------|-----|
| Every financial event balanced DE | Handlers mostly call `ValidateBalanced` | Strong on happy path |
| Chart of accounts for assets/liabilities/revenue/expense | Free strings, no enum | Soft model |
| Revenue recognition schedules | Entity + job only; **never created** | **Major** |
| Net profit after fees & commissions | Summary exists; ABS bugs; no period net-profit API | Partial |
| Tax liability never counted as profit | Tax line exists; refunds/ABS undermine | Partial |
| AR & AP tracking | Invoice AR + affiliate AP posts; no settlement | Incomplete |
| LHDN prep on ledger | Status fields + PDF + consolidation event | Consolidation filter broken |
| Terminal financial sink | Publishes side-effect events | Doc drift |
| No cross-schema joins | commerce/crm SQL in Billing | Violation |
| Query MRR/net only via Billing | Correct intent; summary math unreliable under reversals | Implementation risk |
| Prepaid utility wallet monorepo monetization | Working for WhatsApp/LHDN with bugs | Double LHDN charge |
| B2C batch on 28th | Job exists | **Does not select B2C_RECEIPT entries** |
| Immutable audit ledger | Lines append-only-ish; entry headers mutable | Partial |
| Affiliate commissions → Billing | Handler only, no producer | Dead |

**Bottom line:** The module has a **credible double-entry skeleton** for gateway sales/refunds/commissions, but **amortization, AR settlement, summary correctness under reversals, B2C consolidation, and wallet–ledger unity are unfinished or incorrect**. It is not yet “audit-grade financial truth.”

---

## Testing Coverage

| Suite | Coverage | Gaps |
|-------|----------|------|
| `Modules.Billing.Tests` | Domain unit: wallet top-up/deduct/clawback; hold consume/release | No ledger balance tests, no schedule math tests |
| `Lazuar.ModuleTests/Billing` | Gateway B2C **order of SaveChanges vs document**; manual enroll same | No balance equation tests, no tax/refund, no top-up dual handler |
| `Lazuar.IntegrationTests` | Summary SQL with simple sale + top-up expense; DbContext CreditLedger attach | No refunds/ABS, no concurrency, no idempotency, no workers |
| Architecture tests | Module boundary includes Billing | Does not catch Dapper cross-schema |
| Lhdn module tests | Mock `IBillingQueryService` / credits | Does not assert double-deduct event path |

**Missing high-value tests (recommended):**

1. `ValidateBalanced` matrix for payment/refund/top-up/zero-amount
2. Financial summary with refund + cancellation + recognition
3. Concurrent deduct with xmin (integration)
4. Idempotent deduct under parallel retry
5. B2C consolidation eligibility for `B2C_RECEIPT`
6. LHDN single charge path (command vs event)
7. Platform top-up: single economic story
8. Deferred schedule creation + recognition idempotency
9. Sequence uniqueness under concurrency

---

## Recommendations (Prioritized)

### P0 — Correctness / money bugs

1. **Eliminate double LHDN credit charge**  
   Remove wallet deduct from `LhdnDocumentSubmittedIntegrationEventHandler` *or* remove command-side deduct and charge only once with idempotency. Align amount to `ICreditCostService` (not hardcoded `1`).

2. **Fix B2C consolidation selection**  
   Select `CustomerType == "B2C" && LhdnValidationStatus == "B2C_RECEIPT"` (or a dedicated `PENDING_CONSOLIDATION` status set at payment time). Separate “customer receipt number” from LHDN consolidation state (today one field does both jobs).

3. **Rewrite financial summary with signed sums**  
   Drop `ABS` for nettable accounts; compute outstanding deferred as signed liability balance; net refunds via signed `CONTRA_REVENUE` / reverse tax lines.

4. **Refund tax & fee symmetry**  
   Reverse proportional `LIABILITY_TAX_PAYABLE` (and consider original tax codes) on refunds.

5. **Utility top-up accounting story**  
   Skip `GatewayPaymentCompletedHandler` revenue path when `metadata.type == utility_credit_topup`, *or* book platform-level revenue in a platform org only. Always reverse ledger on dispute, not only wallet units.

### P1 — Vision completion / safety

6. **Create `DeferredRevenueSchedule` when booking deferred revenue** (invoice / multi-period products); wire payment `LineItems.RevenueType` if present.

7. **Implement or delete dead paths:** `ManualPaymentRecorded` handler; `ApiCreditPurchased` registration; `InvoiceIssued` / `CommissionAccrued` publishers — or remove unused contracts.

8. **Clawback / reserve / consume:** concurrency retries + idempotency keys; unique `(OrganizationId, CorrelationId)` for open holds.

9. **Fail-closed wallet missing:** LHDN/messaging should not silently skip if wallet absent after entitlement seeding.

10. **Implement TypeSpec `/net-profit` and summary date filters** — or remove from public contract.

### P2 — Architecture hardening

11. **Account type constants / enum** shared in Domain; ban magic strings in handlers.

12. **Stop cross-schema SQL** — denormalize customer name/email into ledger description/event payload or read model owned by Billing.

13. **Update README** to match publishing side-effects, wallet dual model, and current event graph.

14. **Document generation:** use payment `LineItems`; verify R2 object exists; don’t overload `TaxInvoiceId` for receipt vs LHDN UUID.

15. **Worker multi-instance safety:** advisory locks for consolidation/recognition; period windows; stable consolidation idempotency keys (not random Guid in Lhdn).

16. **Credit hold product decision:** either re-enable broadcast reservation or delete hold aggregate to reduce attack surface / dead code.

17. **Expand tests** per list above before further financial features.

### P3 — Product polish

18. Held-credits / lifetime credits metrics API  
19. TIN/SST validation on billing profile  
20. Store granted credit amount on top-up ledger for exact clawback  
21. Separate platform (Lazuar) books vs tenant merchant books if multi-tenant SaaS accounting is required  

---

## File-by-File Notes

### Domain

| File | Notes |
|------|--------|
| `Domain/Aggregates/LedgerEntry.cs` | Good balance guard; string account types; LHDN fields dual-purpose |
| `Domain/Aggregates/TenantCreditBalance.cs` | Solid wallet rules; clawback clamp; not DE |
| `Domain/Aggregates/CreditHold.cs` | Clear reserve model; `RELEASED` unused; status strings |
| `Domain/Aggregates/DeferredRevenueSchedule.cs` | Linear recognition; double math; **unused producer** |
| `Domain/Aggregates/TenantBillingProfile.cs` | Minimal profile |
| `Domain/Entities/LedgerLine.cs` | Internal ctor; no side metadata |
| `Domain/Entities/CreditLedger.cs` | Append-only intent |
| `Domain/Entities/DocumentSequence.cs` | No behavior |
| `Domain/Entities/CreditDeductionIdempotencyLog.cs` | Good |
| `Domain/ValueObjects/TenantBillingAddress.cs` | Defaults country `MYS` |

### Application

| File | Notes |
|------|--------|
| `Application/DependencyInjection.cs` | Empty |
| `Application/ILedgerRepository.cs` | Minimal; `AddDeferredRevenue` unused |
| `Application/Queries/GenerateDraftDocumentQuery.cs` | Cross-module draft |
| `Application/Queries/Agent/GetFinancialHealthAgentQuery.cs` | Maps summary → agent DTO (labels Net as cash) |
| `Application/Llm/BillingPromptProvider.cs` | Prompt rules only |

### Contracts

| File | Notes |
|------|--------|
| `Commands/DeductTenantCreditCommand.cs` | Optional idempotency key |
| `Commands/CreditHoldCommands.cs` | Full hold lifecycle; no callers |
| `Commands/GenerateAndStoreDocumentCommand.cs` | OK |
| `Commands/GenerateNextSequenceNumberCommand.cs` | OK |
| `Commands/UpdateTenantBillingProfileCommand.cs` | OK |
| `Events/InvoiceIssuedIntegrationEvent.cs` | No publisher |
| `Events/ManualPaymentRecordedIntegrationEvent.cs` | Dead |
| `Events/CommissionAccruedIntegrationEvent.cs` | No publisher |
| `Events/ConsolidatedInvoiceIssuedIntegrationEvent.cs` | Used by B2C job → Lhdn |
| `Events/DocumentPublishedIntegrationEvent.cs` | Used by PDF handler |
| `IBillingQueryService.cs` | No period params on summary |
| `ICreditCostService.cs` | Actions include unused Email/Broadcast costs |

### Infrastructure — DbContext / repo / services

| File | Notes |
|------|--------|
| `BillingDbContext.cs` | Schema, xmin, unique refs, append-only lines/ledgers |
| `Repositories/LedgerRepository.cs` | Thin |
| `Services/BillingQueryService.cs` | **ABS summary bug**; good pagination builder |
| `Services/CreditCostService.cs` | Config-driven; default cost 1 if missing key |
| `DependencyInjection.cs` | Missing `ApiCreditPurchasedHandler` registration |
| `Endpoints.cs` | Missing net-profit; top-up OK; public HMAC docs |
| `Documents/*` | QuestPDF + QR; single description line simplification |

### Infrastructure — Commands

| File | Notes |
|------|--------|
| `DeductTenantCreditCommandHandler.cs` | Best credit path in module |
| `CreditHoldCommandHandlers.cs` | Reserve lacks correlation uniqueness |
| `ClawbackCreditsCommandHandler.cs` | No retry/idempotency |
| `GenerateAndStoreDocumentCommandHandler.cs` | Cross-schema customer lookup; publishes event |
| `GenerateNextSequenceNumberCommandHandler.cs` | Good atomic SQL |
| `UpdateTenantBillingProfileCommandHandler.cs` | Upsert OK |

### Infrastructure — Event handlers

| File | Notes |
|------|--------|
| `GatewayPaymentCompletedHandler.cs` | Core DE post; unused LineItems; sets B2C_RECEIPT blocking consolidation |
| `PlatformTopUpEventHandler.cs` | Dual-post risk; package ladder |
| `GatewayRefundCompletedHandler.cs` | No tax reverse |
| `InvoiceIssuedHandler.cs` | No schedule |
| `CommissionAccruedHandler.cs` | Orphan without producer |
| `ZeroAmountCheckoutHandler.cs` | Strict balance |
| `ManualSubscriberEnrolled…` | Cash=revenue, no tax |
| `StarterCreditSeederHandler.cs` | Wallet create on BILLING entitlement |
| `ChargebackClawbackHandler.cs` | Wallet only |
| `ApiCreditPurchasedHandler.cs` | Dead code |
| `LhdnDocumentValidated…` | Status + PDF regenerate |
| `LhdnDocumentCancelled…` | Full reverse lines (good pattern) |
| `LhdnDocumentSubmitted…` | **Hardcoded double-charge** |

### Infrastructure — Workers

| File | Notes |
|------|--------|
| `BillingInboxConsumerJob.cs` | Scaffold |
| `BillingOutboxPublisherJob.cs` | Scaffold |
| `RevenueRecognitionJob.cs` | No-op without schedules |
| `B2cConsolidationJob.cs` | Filter bug; monthly schedule OK in spirit |

### Migrations

| Migration | Notes |
|-----------|--------|
| `20260627124824_InitialBillingSchema` | Ledger, schedules, wallet, inbox/outbox |
| `20260630103955_AddBillingProfilesAndSequences` | Profile + sequences |
| `20260701160306_AddCreditHoldsAndIdempotencyLogs` | Holds + idempotency + xmin |

### Tests (related)

| File | Notes |
|------|--------|
| `tests/Modules.Billing.Tests/*` | Domain only |
| `tests/Lazuar.ModuleTests/Billing/EventHandlers/*` | Ordering only |
| `tests/Lazuar.IntegrationTests/Billing*` | Summary + EF attach |
| `README.md` | Overstates terminal sink / no joins / full DE vision |

### External touchpoints (not in Billing folder but material)

| Path | Notes |
|------|--------|
| `Modules/Lhdn/.../SubmitTaxDocumentCommand.cs` | Pre-check + deduct 3 credits |
| `Modules/Lhdn/.../LhdnSubmissionJob.cs` | Publishes submitted event → second deduct |
| `Modules/Messaging/.../DispatchMessageIntegrationEventHandler.cs` | WhatsApp deduct after send |
| `Modules/Communications/.../BroadcastEndpoints.cs` | Credits zeroed |
| `packages/api-spec/modules/billing/*` | net-profit + date filters ahead of API |
| `src/Lazuar.Api/appsettings.json` | `WhatsAppSend:2`, `LhdnSubmit:3`, packages, `StarterGrant:50` |

---

## Executive summary

Billing is a **well-scaffolded module** with real double-entry posts for gateway sales, refunds, commissions, and LHDN cancellation reversals; a **production-minded prepaid wallet** (xmin + idempotency); and working PDF/receipt generation. It is **not yet** the audit-grade financial core the README/ADRs describe.

**Highest-impact gaps to solidify the backend:**

1. LHDN **double credit deduction**  
2. B2C consolidation **status filter** that excludes all normal B2C receipts  
3. Financial summary **ABS** math under reversals/recognition  
4. **Deferred revenue never scheduled** (job is dead)  
5. Utility top-up **dual ledger** + chargeback incomplete  
6. Dead contracts/handlers vs missing AR settlement  
7. Thin tests that do not protect money invariants  

Closing P0–P1 items above would move the module from “skeleton that posts balanced journals sometimes” to a backend that can be trusted as **financial truth**.
