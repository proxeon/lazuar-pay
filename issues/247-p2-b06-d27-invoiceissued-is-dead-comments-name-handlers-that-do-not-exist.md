---
number: "247"
id: B06-D27
severity: P2
status: open
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 247 — B06-D27 — InvoiceIssued is dead; comments name handlers that do not exist

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D27 — InvoiceIssued is dead; comments name handlers that do not exist (P2)

```8:25:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs
/// InvoiceIssued has no honest buyer identity. MyInvois submit is
/// <see cref="B2bSaleSubmitHandler"/>. This handler must never file stub TIN C1234567890.
...
            "Ignoring InvoiceIssued {Invoice} — MyInvois submit uses B2bSaleReadyForEinvoice only.",
```

`B2bSaleSubmitHandler` does not exist. `B2bSaleReadyForEinvoice` does not exist. The live type is `B2bTaxInvoiceRequestedIntegrationEventHandler`. Grep of `new InvoiceIssuedIntegrationEvent` in production: zero. `MyInvoisLoopTests.InvoiceIssuedHandler_DoesNotSubmitStubTin` only asserts the no-op does not throw.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`InvoiceIssuedIntegrationEvent` is a parked B2B “invoice opened / AR booked” event. Lhdn still subscribes a handler that **must not** file the old stub TIN `C1234567890`. That handler is a log-only no-op, which is the correct fail-closed posture. Its comments name `B2bSaleSubmitHandler` and `B2bSaleReadyForEinvoice`, neither of which exists. The live MyInvois hook is `B2bTaxInvoiceRequestedIntegrationEvent` → `B2bTaxInvoiceRequestedIntegrationEventHandler`. Billing still has `InvoiceIssuedHandler` subscribed (AR + deferred revenue) even though nothing in production constructs the event. The comments will send the next editor to the wrong type.

### Still present?
**STILL BROKEN**

Comments and log line are unchanged:

```8:26:apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs
/// InvoiceIssued has no honest buyer identity. MyInvois submit is
/// <see cref="B2bSaleSubmitHandler"/>. This handler must never file stub TIN C1234567890.
...
            "Ignoring InvoiceIssued {Invoice} — MyInvois submit uses B2bSaleReadyForEinvoice only.",
            @event.InvoiceNumber);
        return Task.CompletedTask;
```

Grep of `B2bSaleSubmitHandler` / `B2bSaleReadyForEinvoice` as types: only this comment, the audit, and `W2-LP-110-analysis.md`. Live consumer:

```101:101:apps/lazuar-api/Modules/Lhdn/Infrastructure/DependencyInjection.cs
        eventBus.Subscribe<B2bTaxInvoiceRequestedIntegrationEvent, B2bTaxInvoiceRequestedIntegrationEventHandler>();
```

`new InvoiceIssuedIntegrationEvent` in production `*.cs`: **zero**. The only construction is `MyInvoisLoopTests.cs:40`. Billing still subscribes `InvoiceIssuedHandler` (`DependencyInjection.cs:93`) which would book AR if anyone published (`InvoiceIssuedHandler.cs:19–42`). Lhdn still subscribes the no-op (`DependencyInjection.cs:98`).

### Related files
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/InvoiceIssuedIntegrationEventHandler.cs` — stale names + correct no-op.
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/EventHandlers/B2bTaxInvoiceRequestedIntegrationEventHandler.cs` — live type-01 submit.
- `apps/lazuar-api/Modules/Billing/Contracts/Events/InvoiceIssuedIntegrationEvent.cs` — unused event.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/InvoiceIssuedHandler.cs` — dead AR writer if published.
- `apps/lazuar-api/Modules/Billing/Infrastructure/DependencyInjection.cs` / `Modules/Lhdn/Infrastructure/DependencyInjection.cs` — both still subscribe InvoiceIssued.
- `apps/lazuar-api/Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` — publishes `B2bTaxInvoiceRequested`, not InvoiceIssued (`:160`).
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Lhdn/MyInvoisLoopTests.cs` — `InvoiceIssuedHandler_DoesNotSubmitStubTin`.
- `issues/234-p2-b05-l30-dead-parked-writers-that-will-confuse-the-next-editor.md` — same orphan event.

### Tests
- `MyInvoisLoopTests.InvoiceIssuedHandler_DoesNotSubmitStubTin` — constructs the event and asserts the no-op does not throw. Does **not** assert log text, cref names, or “no production publisher.”
- `MyInvoisLoopTests.B2bHandler_NoTin_DoesNotSubmit` / `B2bHandler_RealTinAndId_SubmitsBuyerFromCrm` and `B2bTaxInvoiceRequestedIntegrationEventHandlerTests.*` cover the live hook.
- `PlatformSaasInvoiceTests` / `PlatformSaasFeeHandlerTests` assert SaaS does **not** publish InvoiceIssued.
- No test would fail if the comments stayed stale. The no-op test would still pass if someone re-enabled a stub-TIN submit **as long as HandleAsync did not throw** — it does not inspect mediator/gateway.
- First regression: assert the handler source contains `B2bTaxInvoiceRequested` and does not contain `B2bSaleSubmitHandler` / `B2bSaleReadyForEinvoice`; keep a test that HandleAsync does not send `SubmitTaxDocumentCommand`.

### Reproduction today
Grep `new InvoiceIssuedIntegrationEvent` under `apps/lazuar-api` excluding tests: empty. Open the Lhdn handler and click the `B2bSaleSubmitHandler` cref (unresolved). Pay a B2B product and watch the outbox: `B2bTaxInvoiceRequestedIntegrationEvent` is what Lhdn consumes.

### Blast radius
Editor confusion and a landmine if someone “restores” InvoiceIssued publishing (Billing would book AR+deferred; Lhdn would still no-op — split books vs MyInvois). Stub TIN is **not** filed today. Frequency: every time someone reads the handler or Billing README §5 (also names InvoiceIssued; 244).

### Suggested fix
Rewrite the comment and log to `B2bTaxInvoiceRequestedIntegrationEventHandler`. Do not publish InvoiceIssued from Commerce or Hub SaaS. Do not revive stub TIN `C1234567890`. Optional later epic: delete the Lhdn subscription and/or the Billing handler together with 234/244. No TypeSpec regen.

### Evaluation notes
Overlaps **234** (dead InvoiceIssuedHandler) and Billing README §5 in **244**. The no-op itself is the 161–200-era fail-closed leftover (do not file stub TIN) and should stay. Still P2 (stale names, not a live submit). Do not change YAML status.

