# G18 — PSP webhook route on 8081

**Track:** Rails · **Depends:** D23  
**Analysis:** [06](../06-money-rails.md) §5  
**IDs:** NP-API-002  
**Goal:** Plane B door. `NP-API-002`. Verify + insert, then fulfill (or log not wired).

---

## G18.1 Path

- [x] `POST /v1/webhooks/{provider}/{orgId}` on **8081**
- [x] `{provider}` = G10 rail only (`stripe` or `chip`)
- [x] **Not** Hub `/api/v1/webhooks/payments/{gateway}/{tenantId}`
- [x] **Not** `/one/*`. **Not** `/v1/one/webhooks` (Plane A)
- [x] No auth Bearer — PSP signature **is** the auth (G19)

## G18.2 Pipeline (no MediatR)

- [x] Unknown provider → **400**. Read **raw** body before model bind
- [x] After verify + D23 unique insert: if payment completed → call fulfill (F10)
- [x] F10 may be the **same commit** if small
- [x] If F10 is not done: persist the event and log `fulfill not wired` — **prefer G21 then F10 next**
- [x] Do **not** 200 and silent-drop (no row, no log)

## G18.3 Must not

- [x] No `IMediator.Send(ProcessGatewayWebhookCommand)`. No Payments outbox
- [x] Do not 401 this route. Do not wait for One to ACK money

## G18.4 Exit

- [x] `NP-API-002` may move when G19/G20/G21 exist (prefer G18–G21 same tip if small)
- [x] Unblocked for G19, G20, G21, G26
