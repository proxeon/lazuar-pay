# W19 — Unique delivery event id

**Track:** W · **Depends:** W18  
**Goal:** Replay fulfill / second worker pass does not double-notify.

**Why:** Unique `charges.CheckoutId` already stops a second receipt. Enqueue must use the same grain (charge id) so a retried fulfill or a worker restart cannot insert two `payment.completed` rows.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` | Unique `charges.CheckoutId` |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Detach on unique conflict |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/FillTests.cs` | Concurrent fulfill one receipt |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/PostgresTxTests.cs` | TX proof |

**Current (`6d730d15`):** Unique fulfill; no delivery unique.

---

## W19.1

- [x] Unique `(EndpointId, EventId)`
- [x] Second fulfill of same checkout (unique charge already) never reaches a second completed; if enqueue called twice, catch unique and continue
- [x] Worker sending twice is the **app’s** idempotency (document); Pay still only has one row

## W19.2 Tests

- [x] Two fulfill attempts → one delivery row
- [x] Postgres unique if you have Testcontainers; InMemory unique index if configured

## W19.3 Exit

- [x] Unblocked for W20
