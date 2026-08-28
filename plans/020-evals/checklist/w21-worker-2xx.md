# W21 — Worker 2xx → succeeded

**Track:** W · **Depends:** W20, W17  
**Goal:** Round-trip HMAC matches inbound verifier.

**Why:** If Compute and TryVerify disagree, every sample will 401. Test rail start-to-paid is the cheapest fulfill.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` | Verify |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/FakePspHandler.cs` | Pattern for capturing HTTP |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PublicPay/PublicPayTests.cs` | Test start = paid |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` | HMAC vectors |

**Current (`6d730d15`):** N/A.

---

## W21.1

- [x] HTTP 2xx → `Status = succeeded`
- [x] Captured request: raw body equals `PayloadJson`
- [x] `OneWebhookSignature.TryVerify` succeeds with stored secret, body, headers

## W21.2 Tests

- [x] Test HttpMessageHandler 200
- [x] Fulfill Test rail → ProcessBatch → 1 POST
- [x] Money rows unchanged besides delivery status

## W21.3 Exit

- [x] Unblocked for W22, E14
