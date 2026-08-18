---
number: "234"
id: B05-L30
severity: P2
status: resolved
resolved_branch: fix/234-billing-dead-writers
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 234 — B05-L30 — Dead / parked writers that will confuse the next editor

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/234-billing-dead-writers`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L30 — P2 — Dead / parked writers that will confuse the next editor

- `InvoiceIssuedHandler` subscribed; `new InvoiceIssuedIntegrationEvent` in production: **zero**.  
- `ManualPaymentRecordedIntegrationEvent`: contract only, no handler.  
- `RevenueRecognitionJob` unregistered. If someone hosts it, `DeferredRevenueSchedules.Where(...)` **without** `IgnoreQueryFilters` sees 0 rows under empty worker tenant.  
- `ApiCreditPurchasedHandler` unregistered.  
- Recognition would write `REVENUE_RECOGNIZED`; summary `recognized_revenue` / `deferred_revenue` are almost always 0.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
Several Billing writers look live in DI / README but do not run a real money path. `InvoiceIssuedHandler` is subscribed and would book AR vs deferred revenue, but `new InvoiceIssuedIntegrationEvent` does not exist in production (only Lhdn’s `MyInvoisLoopTests` constructs one). Lhdn’s own `InvoiceIssuedIntegrationEventHandler` is a documented no-op (“never file stub TIN”). `ManualPaymentRecordedIntegrationEvent` is still a contract-only event; architecture tests allow-list it as unused. `RevenueRecognitionJob` is still commented out of `AddHostedService`; if someone uncommented it, `DeferredRevenueSchedules.Where(...)` has no `IgnoreQueryFilters` and an empty worker tenant would see 0 rows. `ApiCreditPurchasedHandler` **is now registered** (audit said it was not) but `new ApiCreditPurchasedIntegrationEvent` still does not exist anywhere — a dead twin of `PlatformTopUpEventHandler` that would `TopUp` without a wallet idempotency key if anyone published it. Summary `recognized_revenue` / `deferred_revenue` stay 0 because no schedule writer exists. README §5 still lists InvoiceIssued + ManualPaymentRecorded as consumed “From B2B/Invoicing”.

### Still present?
**PARTIAL**

Still subscribed, never published in production:

```93:93:apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs
        eventBus.Subscribe<InvoiceIssuedIntegrationEvent, InvoiceIssuedHandler>();
```

`InvoiceIssuedHandler.cs:33-37` still books AR / deferred and comments that recognition is parked. Lhdn ignore-handler: `InvoiceIssuedIntegrationEventHandler.cs:8-26`.

Still unused (explicit allow-list):

```19:23:apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs
    private static readonly HashSet<string> EventsWithoutInProcessHandlers = new(StringComparer.Ordinal)
    {
        "Modules.Billing.Contracts.Events.ManualPaymentRecordedIntegrationEvent",
        "Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent",
    };
```

Recognition still unregistered, now with a long park comment (`DependencyInjection.cs:72-77`, `RevenueRecognitionJob.cs:14-30`, `README.md:47`). Query is still unscoped:

```66:68:apps/lazuar-api/Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs
        var pendingSchedules = await db.DeferredRevenueSchedules
            .Where(s => s.Status != "COMPLETED")
            .ToListAsync(ct);
```

Changed since the audit: `ApiCreditPurchasedHandler` **is** `AddTransient` + `Subscribe` (`DependencyInjection.cs:70, 103`). Grep of `new ApiCreditPurchasedIntegrationEvent` across `*.cs` is empty. README §5:39 still names both dead B2B events as consumed. `PlatformSaasFeeHandler` still holds `typeof(InvoiceIssuedIntegrationEvent)` so reviewers see the refusal (`:116-117`).

### Related files
- [`apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs) — subscriptions + parked job comment.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs) — AR/deferred writer with no publisher.
- [`apps/lazuar-api/Modules/Billing/Contracts/Events/InvoiceIssuedIntegrationEvent.cs`](apps/lazuar-api/Modules/Billing/Contracts/Events/InvoiceIssuedIntegrationEvent.cs) / [`ManualPaymentRecordedIntegrationEvent.cs`](apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs) — dead contracts.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/Workers/RevenueRecognitionJob.cs) — unregistered; filter landmine.
- [`apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ApiCreditPurchasedHandler.cs`](apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ApiCreditPurchasedHandler.cs) — subscribed twin of top-up; no publisher.
- [`apps/lazuar-api/Modules/Billing/README.md`](apps/lazuar-api/Modules/Billing/README.md) — §5 still lists the unused events; §6 now honestly parks recognition.
- [`apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs`](apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs) — allow-lists ManualPaymentRecorded.

### Tests
- Existing: `IntegrationEventSubscriptionTests.Every_Integration_Event_Has_A_Subscribe_Or_Is_Explicitly_Unused` (ManualPaymentRecorded must stay on the unused list); `PlatformSaasFeeHandlerTests` / `PlatformSaasInvoiceTests.StoreHandler_UploadsPdf_DoesNotPublishInvoiceIssued`; `MyInvoisLoopTests` constructs `InvoiceIssued` only to prove Lhdn ignores it. No test that production never `new`s `InvoiceIssuedIntegrationEvent`. No Billing test for `RevenueRecognitionJob`. No test for `ApiCreditPurchasedHandler`.
- Nothing fails while these writers stay dead. Architecture would fail if someone deleted the unused-list entry without adding a handler.
- First regression (honesty): assert no production `new InvoiceIssuedIntegrationEvent` / `new ManualPaymentRecordedIntegrationEvent` / `new ApiCreditPurchasedIntegrationEvent` (architecture or source grep test), and that `AddHostedService<RevenueRecognitionJob>` is absent. If recognition is ever enabled, a worker-tenant test must use `IgnoreQueryFilters` or an org scope.

### Reproduction today
Read `UseBillingSubscriptions`. Confirm `InvoiceIssued` and `ApiCreditPurchased` are subscribed. Grep production for `new InvoiceIssuedIntegrationEvent` (hits only tests) and `new ApiCreditPurchasedIntegrationEvent` (zero). Hit `GET /admin/billing/summary`: `deferred_revenue` and `recognized_revenue` are 0 with no schedules. Uncomment `AddHostedService<RevenueRecognitionJob>` in a worker with empty ambient tenant: `ProcessRecognitionsAsync` returns immediately even if another tenant has rows (fail-closed filter). Publish `ManualPaymentRecorded` on the in-process bus: architecture says that still throws (no handler).

### Blast radius
Editors, not customers, until someone “enables” recognition or publishes `ApiCreditPurchased` on the same gateway tx as `PlatformTopUpEventHandler` (wallet would double-credit; ledger unique key `(OrganizationId, ReferenceType, ReferenceId)` would throw on the second `SYSTEM_CREDIT_TOPUP`). Summary cards show 0 recognized/deferred — honesty, not a cash leak. Ops who trust README §5 will look for a ManualPaymentRecorded journal that does not exist (offline money is `ManualSubscriberEnrolled`).

### Suggested fix
Do not invent publishers. Smallest change: (1) drop or rewrite README §5 so it does not list unused events (pair with **244**); (2) move `ApiCreditPurchasedIntegrationEvent` onto the unused-list **or** delete the handler if the product path is `PlatformTopUpEventHandler` only; (3) leave `RevenueRecognitionJob` parked until a finance epic owns schedule writers + `IgnoreQueryFilters`/org scope — do not uncomment. Do not start Xero / Wave 5 recognition. Do not TypeSpec-regen.

### Evaluation notes
Honesty improved vs 009 (job XML + README §6). Registration of `ApiCreditPurchasedHandler` is a **161**-era “every event is subscribed or unused” side effect, not a product fix — and it is more dangerous than leaving it unregistered. Duplicate of **244** for the README lie. Still P2. Not blocked. Fail-closed 161–200 did not add `IgnoreQueryFilters` to the parked job (correct while unhosted).

