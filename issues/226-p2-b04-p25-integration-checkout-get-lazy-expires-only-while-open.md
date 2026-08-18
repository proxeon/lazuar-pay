---
number: "226"
id: B04-P25
severity: P2
status: resolved
resolved_branch: fix/226-expired-then-completed
source: plans/009-bugs/04-payments-adapters-webhooks.md
head: "297ba98"
---

# 226 — B04-P25 — Integration checkout GET lazy-expires only while `open`

- **Severity:** P2
- **Status:** resolved
- **Source:** `plans/009-bugs/04-payments-adapters-webhooks.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/226-expired-then-completed`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B04-P25 — P2 — Integration checkout GET lazy-expires only while `open`

**Where.** `GetIntegrationCheckoutQueryHandler.cs:31-35`; `TryExpireIfPast` (`IntegrationCheckoutSession.cs:125-134`).

**What.** A `failed` session past TTL stays `failed` (good). An `open` session past 24h becomes `expired` on GET. Webhooks after expire: M2M handler still requires `open` — a late pay on an expired session is dropped. Buyer can pay a 25-hour-old bill; M2M outbound never fires. Related to B04-P02 (terminal states swallow completed).

## Evaluation (current tree, 2026-08-18)

### What the bug is
M2M sessions live 24h (`IntegrationCheckoutSession.DefaultTtl`). The only expire writer in this module is `TryExpireIfPast`, which flips `open → expired` when `utcNow >= ExpiresAt`. `failed` / `completed` / already-`expired` are left alone. GET `/integrations/payments/checkouts/{id}` and create-idempotency replay call that helper; there is no expire worker. The 17 Aug harm was: GET expires a stale `open` row, then a late processor webhook arrives, and `IntegrationCheckoutGatewayEventsHandler` required `Status == open` for completed — so a buyer who paid a 25-hour-old Billplz/CHIP bill never got outbound `payment.completed`. That session-layer drop was B04-P02. Issue 006 (`fix/006-m2m-fail-then-pay`) changed completed to **win** over `failed` / `expired`. The lazy-expire-only-while-open behaviour is still there (and is correct for `failed`). The money-drop consequence is not.

### Still present?
**PARTIAL**

Expire helper is unchanged (only `open`):

```124:134:apps/lazuar-api/Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs
    /// <summary>Lazy expire when still open and past TTL.</summary>
    public bool TryExpireIfPast(DateTime utcNow)
    {
        if (Status == StatusOpen && utcNow >= ExpiresAt)
        {
            MarkExpired();
            return true;
        }

        return false;
    }
```

GET still lazy-expires:

```31:35:apps/lazuar-api/Modules/Payments/Application/Queries/GetIntegrationCheckoutQueryHandler.cs
        if (session.TryExpireIfPast(DateTime.UtcNow))
        {
            await _sessions.SaveChangesAsync(cancellationToken);
        }
```

Create replay also calls `TryExpireIfPast` (`CreateIntegrationCheckoutCommandHandler.cs:183`). Completed no longer requires `open`:

```59:69:apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs
        // Fail-then-pay: CHIP/Billplz can publish FAILED then COMPLETED for the same object.
        // Completed money wins over failed/expired. Already-completed is idempotent.
        if (session.Status == IntegrationCheckoutSession.StatusCompleted)
        {
            // skip duplicate
            return;
        }

        session.MarkCompleted(@event.GatewayTransactionId);
```

Fail still requires `open` (`IntegrationCheckoutGatewayEventsHandler.cs:101-107`) — late fail after expire/complete is dropped (good; 065/006). There is still no background expire job. A session nobody GETs stays `open` past TTL and a late pay still completes (also good). Processor hosted pages can remain payable after our 24h.

### Related files
- `apps/lazuar-api/Modules/Payments/Domain/Aggregates/IntegrationCheckoutSession.cs` — TTL + `TryExpireIfPast`.
- `apps/lazuar-api/Modules/Payments/Application/Queries/GetIntegrationCheckoutQueryHandler.cs` — GET expire.
- `apps/lazuar-api/Modules/Payments/Application/Commands/CreateIntegrationCheckoutCommandHandler.cs` — idempotency replay expire.
- `apps/lazuar-api/Modules/Payments/Infrastructure/EventHandlers/IntegrationCheckoutGatewayEventsHandler.cs` — completed wins (006).
- `apps/lazuar-api/Modules/Payments/Infrastructure/IntegrationEndpoints.cs` — GET route.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/CreateIntegrationCheckoutTests.cs` — GET happy path, **no** TTL expire case.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Payments/IntegrationCheckoutOutboundWebhookTests.cs` — `Failed_ThenCompleted_MarksCompleted_AndPublishesPaymentCompleted`; no `Expired_ThenCompleted`.
- `issues/006-p0-b04-p02-m2m-fail-then-pay-session-stays-failed-outbound-payment-complete.md` — the P0 that removed the drop.

### Tests
- Existing tests: `CreateIntegrationCheckoutTests.Get_OwnCheckout_ReturnsMetadataAndAmount` (fresh session, still `open`). `IntegrationCheckoutOutboundWebhookTests.Failed_ThenCompleted_MarksCompleted_AndPublishesPaymentCompleted`; `Failed_AlreadyFailed_NoSecondPublish`; `Completed_DualEvents_SameSession_SingleOutbound`. I found **no** test named `TryExpireIfPast` and no GET that advances the clock past `ExpiresAt`.
- Whether any test would fail if the **remaining** expire-only-while-open behaviour is still there: **no**.
- Whether a test would fail if 006 were reverted (completed requires open again): **yes** — `Failed_ThenCompleted_*`.
- What a first regression test should assert: session `Status=expired` (or `TryExpireIfPast` after `ExpiresAt`) then `HandleAsync(GatewayPaymentCompleted)` → status `completed` and one outbound `payment.completed`. Second: GET after TTL on an `open` row returns `expired` and persists; GET on a `failed` row past TTL stays `failed`.

### Reproduction today
Arrange: M2M checkout, `ExpiresAt = now-25h`, status `open`. Act 1: GET `/integrations/payments/checkouts/{id}` → `status=expired`. Act 2: pay the still-live Billplz/CHIP page; inbound webhook publishes `GatewayPaymentCompleted`. Assert: session is `completed` and the workspace receives outbound `payment.completed` (006). Act 3: same GET on a `failed` session past TTL → still `failed`. The audit’s “outbound never fires” is **false** on this tree.

### Blast radius
M2M integrators. After 006 a late pay is fulfilled (correct). Remaining harm is cosmetic: GET is the only expire clock, so listing/idempotency can show `open` for a 3-day-old unused session until someone GETs it; processor may still accept money after our TTL (processor policy, not our drop). No PII. No double-cash. Frequency: any integrator who polls after 24h, or any Billplz bill whose hosted TTL > 24h.

### Suggested fix
Do not revert 006. Keep `TryExpireIfPast` open-only (failed/completed must not be rewritten to expired). Add `Expired_ThenCompleted` test so nobody re-introduces “completed requires open”. Optional: expire on webhook intake before resolve (still allow completed to win). Optional: a worker that marks `open` past TTL as `expired` so GET is not the only clock — not required to close the P2 money story. Do not invent Stripe `subscription.updated`. Do not change TypeSpec.

### Evaluation notes
Duplicates: B04-P02 / 006 (fixed the drop). Severity: the **drop** was the reason this was a money-adjacent P2; that is gone. Remaining lazy-expire semantics are still P2 hygiene. Not blocked. Residual after 161-200: 171 (M2M status filter) is Commerce subscribers, not this session.


