# W22 — Worker 5xx schedules retry

**Track:** W · **Depends:** W20  
**Goal:** At-least-once. Money already paid.

**Why:** Merchant URL 500 must not roll back the receipt. Pending + backoff. Do not block the PSP thread.

**Related files**

| Path | Role today |
|------|------------|
| W11 deliveries | `NextAttemptAt`, `AttemptCount` |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Fulfillment.cs` | Money already committed |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Webhooks/PostgresTxTests.cs` | TX vs HTTP contrast |

**Current (`6d730d15`):** N/A.

---

## W22.1

- [x] 5xx or timeout → `AttemptCount++`, `NextAttemptAt` in the future, status stays `pending`
- [x] Backoff simple (e.g. 15s, 1m, 5m, cap) — document constants
- [x] Charge/receipt unchanged

## W22.2 Tests

- [x] Handler 500 → no succeeded; ProcessBatch again after clock advance retries
- [x] Do not infinite-loop in one ProcessBatch call

## W22.3 Exit

- [x] Unblocked for W23
