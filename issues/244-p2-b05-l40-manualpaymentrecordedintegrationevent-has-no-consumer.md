---
number: "244"
id: B05-L40
severity: P2
status: open
source: plans/009-bugs/05-billing-ledger-refunds-disputes.md
head: "297ba98"
---

# 244 — B05-L40 — `ManualPaymentRecordedIntegrationEvent` has no consumer

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/05-billing-ledger-refunds-disputes.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B05-L40 — P2 — `ManualPaymentRecordedIntegrationEvent` has no consumer

Contract exists. Billing README §5 still lists “From B2B/Invoicing: `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.” Manual **enrollment** is a different event and **is** consumed. The recorded-payment event is a lie in the README.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
`ManualPaymentRecordedIntegrationEvent` is a Billing contracts record that looks like the settlement hook for a B2B invoice being marked paid by hand. Nothing in production publishes it, and no `IIntegrationEventHandler<ManualPaymentRecordedIntegrationEvent>` exists. The live offline/manual cash path is a different type, `ManualSubscriberEnrolledIntegrationEvent`, which Billing **does** subscribe and journal. The Billing module README §5 still tells the next engineer that Billing consumes `ManualPaymentRecordedIntegrationEvent` (and `InvoiceIssuedIntegrationEvent`) “From B2B/Invoicing.” That is false: the recorded-payment event is contract-only. After issue 161 the in-process bus must not treat “no handlers” as success, so anyone who wires a publisher without a handler will fail closed rather than silently drop cash.

### Still present?
**STILL BROKEN**

The contract is still the unused record:

```6:16:apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs
public record ManualPaymentRecordedIntegrationEvent(
    Guid OrganizationId,
    string InvoiceNumber,
    decimal AmountPaid,
    string Currency,
    string PaymentMethod,
    string? ReferenceNumber) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
```

Billing subscriptions register enrollment, not recorded-payment (`UseBillingSubscriptions` at `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs:99` has `Subscribe<ManualSubscriberEnrolledIntegrationEvent, ManualSubscriberEnrolledIntegrationEventHandler>` and no `ManualPaymentRecorded` line). Grep of `IIntegrationEventHandler<ManualPaymentRecorded` under `*.cs` is empty. Architecture tests **lock the orphan in**:

```19:23:apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs
    private static readonly HashSet<string> EventsWithoutInProcessHandlers = new(StringComparer.Ordinal)
    {
        "Modules.Billing.Contracts.Events.ManualPaymentRecordedIntegrationEvent",
        "Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent",
    };
```

README §5 is unchanged:

```39:39:apps/lazuar-api/Modules/Billing/README.md
* **From B2B/Invoicing:** `InvoiceIssuedIntegrationEvent`, `ManualPaymentRecordedIntegrationEvent`.
```

Enrollment **is** consumed and books cash (`ManualSubscriberEnrolledIntegrationEventHandler.cs:32–78`).

### Related files
- `apps/lazuar-api/Modules/Billing/Contracts/Events/ManualPaymentRecordedIntegrationEvent.cs` — dead contract the README names.
- `apps/lazuar-api/Modules/Billing/README.md` — §5 overclaims consumption.
- `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` — live subscribe list (enrollment yes, recorded-payment no).
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/ManualSubscriberEnrolledIntegrationEventHandler.cs` — the real manual-cash journal.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs` — sibling orphan consumer (no production `new InvoiceIssuedIntegrationEvent`; see 234 / 247).
- `apps/lazuar-api/tests/Lazuar.ArchitectureTests/IntegrationEventSubscriptionTests.cs` — allowlists the unused event.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs` — covers enrollment, not this event.
- `issues/234-p2-b05-l30-dead-parked-writers-that-will-confuse-the-next-editor.md` — same dead-event bullet.

### Tests
- Existing: `IntegrationEventSubscriptionTests.Every_Integration_Event_Has_A_Subscribe_Or_Is_Explicitly_Unused` (passes **because** this event is on the unused allowlist); `Every_Integration_Event_Handler_Is_Subscribed`; `ManualSubscriberEnrolledHandlerTests` (enrollment only); `CreateManualSubscriberCommandHandlerTests` / `RecordSubscriberPaymentCommandHandlerTests` / `CommerceProductCompletenessTests` publish enrollment.
- None of those fail while the README lie and missing consumer remain. The architecture test would fail only if someone **removed** the allowlist entry without adding a handler, or added a handler without `Subscribe<>`.
- First regression: either delete the event + README line + allowlist entry and assert `Every_Integration_Event_Has_A_Subscribe_Or_Is_Explicitly_Unused` still passes, **or** add a real handler + `Subscribe<>`, drop the allowlist row, and assert a recorded-payment journal. Do not add a handler that books cash a second time on top of `ManualSubscriberEnrolled`.

### Reproduction today
Arrange a tenant with a B2B invoice number and no subscription enrollment. Act: grep production `new ManualPaymentRecordedIntegrationEvent` (zero hits) and read Billing README §5. Assert: no handler type, no subscribe line, README still names the event. Separately mark a subscriber paid offline and confirm the ledger row is `ManualSubscriberEnrolled`, not this event.

### Blast radius
Docs / next-editor trap, not a live money leak today (nothing publishes it). If a future invoicing slice publishes it expecting AR settlement, issue 161 means the in-process bus should throw rather than drop; dual-booking vs enrollment is the money risk if someone “just adds a handler.” Demo/README readers will think B2B invoicing already settles cash. Frequency: every time someone reads Billing README §5 or copies the event.

### Suggested fix
Smallest honest change: delete or park the contract, remove it from README §5 (keep enrollment as the named Commerce→Billing cash event), and keep or drop the architecture allowlist to match. Do **not** invent a Stripe Billing `subscription.updated` consumer. Do not book a second cash journal on recorded-payment if enrollment already wrote `ASSET_CASH`. No TypeSpec regen. No Wave 5 / WhatsApp / Xero / homemade e-mandate.

### Evaluation notes
Duplicates the `ManualPaymentRecordedIntegrationEvent: contract only, no handler` bullet on **234** (B05-L30). Sibling orphan is `InvoiceIssuedIntegrationEvent` (247 + 234). Residual after 161: unused events are explicit, not silent success. Still P2 (honesty / dead contract, no live path). Do not mark YAML resolved.

