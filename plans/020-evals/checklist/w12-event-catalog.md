# W12 — Closed event catalog

**Track:** W · **Depends:** K00  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §5 / §9.1  
**Goal:** Pay emits only types it writes.

**Why:** Hub catalog listed events with no writer. Aura woke on noise. Hatch is `payment.completed` when `FulfillPaidAsync` actually pays. Rails ignore failure events → no `payment.failed` (P15).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Only paid writer |
| `apps/lazuar-pay/src/Lazuar.Pay/Webhooks/WebhookEndpoints.cs` | `{ ignored }` on unknown PSP events |
| `packages/pay-spec/main.tsp` | Inbound Webhooks tag only |

**Current (`6d730d15`):** No catalog class.

---

## W12.1

- [ ] `PayWebhookEventCatalog` (name may vary) contains `payment.completed`
- [ ] Optional same program: `webhook.test`
- [ ] Register door 400s unknown `enabled_events` strings
- [ ] Empty `enabled_events` means the closed default list (completed ± test)

## W12.2 Must not

- [ ] No `payment.failed` until P15 (rails do not write failed)
- [ ] No `refund.created` until P10
- [ ] No `subscription.activated` because a stub table exists
- [ ] No Hub `GatewayPaymentCompletedIntegrationEvent`

## W12.3 Exit

- [ ] Unblocked for W14
