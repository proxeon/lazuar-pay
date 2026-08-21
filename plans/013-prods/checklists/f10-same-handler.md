# F10 — Same request, same process `fulfill()`

**Track:** Fulfillment · **Depends:** G21  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-FUL-001  
**Goal:** After PSP verify + idempotency, call `fulfill()` in the same HTTP request and process. Do not wait on One.

---

## F10.1 Call site

- [x] Webhook `POST /v1/webhooks/{provider}/{orgId}`: after signature verify and `(org_id, provider, event_id)` idempotency, call `fulfill()` (name illustrative)
- [x] Same process as the PSP POST. Not a worker. Not a Billing inbox
- [x] Return 200 to the PSP only after fulfill commits, or after an idempotent no-op

## F10.2 Must not

- [x] No `PublishAsync` of `GatewayPaymentCompletedIntegrationEvent` (Pay must not talk to Pay via a bus)
- [x] No MediatR `IRequestHandler` for this write
- [x] No wait on One (`/me`, members, `authz/write`, SCIM) before access/journal exist
- [x] No `AddBillingModule` / `Modules/` under `apps/lazuar-pay` / project reference to `apps/lazuar-api`

## F10.3 Isolation

- [x] IsolationTests still **fail** on substring `MediatR` in host/test csproj and `apps/lazuar-pay/src/**/*.cs`
- [x] Do not weaken IsolationTests to allow MediatR “just for fulfillment”

## F10.4 Exit

- [x] Happy-path call exists in the webhook request
- [x] Unblocked for F11 and F17
