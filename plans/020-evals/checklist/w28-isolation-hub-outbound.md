# W28 — Isolation bans Hub outbound names

**Track:** W · **Depends:** W18  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) hole 11  
**Goal:** Copy-paste of Hub dispatcher fails CI even in a new namespace.

**Why:** IsolationTests ban `Modules.One` and `GatewayPaymentCompletedIntegrationEvent`. A paste into `Lazuar.Pay.Webhooks.Outbound` with Hub type names `OutboundWebhookDispatcherJob` would pass today.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs` | `BannedSrc` |
| Hub `apps/lazuar-api/.../OutboundWebhookDispatcherJob.cs` | Museum tokens to ban |

**Current (`6d730d15`):** Those Hub job names are not in `BannedSrc`.

---

## W28.1

- [x] Add `BannedSrc` tokens: `OutboundWebhookDispatcherJob`, `WebhookDeliveryOutbox`, `IEventBus`, `OutboundWebhookRequested`, `GatewayPaymentCompletedIntegrationEvent` (already present)
- [x] Still ban `Modules.One`, `MediatR`

## W28.2 Must not

- [x] Do not ban the word `webhook` (Plane A/B live)

## W28.3 Exit

- [x] Unblocked for W29
