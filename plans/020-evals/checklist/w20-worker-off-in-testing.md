# W20 — Worker off in Testing; ProcessBatch testable

**Track:** W · **Depends:** W18  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.2.7  
**Goal:** CI does not background-POST. Tests call a method.

**Why:** `PayApiFactory` boots the host. A hosted loop would POST to random URLs in CI. Mirror: Testing skips Npgsql in `Program.cs`; worker skips in Testing.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Program.cs` | `AddHttpClient` names; Testing skips Npgsql |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayApiFactory.cs` | `UseEnvironment("Testing")` |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Chip/ChipHosted.cs` | Named client `"chip"` pattern |

**Current (`6d730d15`):** No worker.

---

## W20.1

- [x] Hosted service runs outside Testing (or when `Pay:OutboundWebhooks:Worker` true)
- [x] `PayApiFactory` does **not** start the loop
- [x] `ProcessBatch(ct)` is public/internal on a small type, injectable
- [x] Named HttpClient `pay-webhooks`: 10s timeout, **no auto redirect**, User-Agent `Lazuar-Pay-Webhooks/1.0`
- [x] Unprotect secret per row; sign; POST raw `PayloadJson`
- [x] Do not log Authorization / `whsec_` / body

## W20.2 Must not

- [x] Do not use Hub job names
- [x] Do not call One dispatcher

## W20.3 Exit

- [x] Unblocked for W21
