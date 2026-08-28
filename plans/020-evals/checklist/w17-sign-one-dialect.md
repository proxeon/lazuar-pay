# W17 — Sign with One dialect (reuse inbound helper)

**Track:** W · **Depends:** K00  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.3  
**Goal:** One HMAC algorithm in the binary.

**Why:** Pay already verifies One inbound `{unix}.{body}` hex `v1=` + timestamp. Official Standard Webhooks signs `msg_id.timestamp.payload` and base64-decodes `whsec_`. Using npm `standardwebhooks` against Pay’s inbound tests would fail. Hatch: compute next to existing verify.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` | `TryVerify` / `TryParse` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Identity/OneWebhookTests.cs` | Split headers + combined compat |
| Hub `OutboundWebhookSignature.cs` | Museum combined `t=,v1=` — do not copy types |

**Current (`6d730d15`):** Verify-only. No `Compute`.

---

## W17.1

- [x] Add `Compute` next to `OneWebhookSignature.TryVerify` (or shared helper)
- [x] Signed payload: `{unix}.{rawBody}`
- [x] Headers out: `X-Lazuar-Signature: v1={lowercase hex}`, `X-Lazuar-Timestamp: {unix}`
- [x] Also send `X-Lazuar-Event-Id`, `X-Lazuar-Event-Type`, `X-Lazuar-Tenant-Id` (org id)
- [x] Secret: full `whsec_…` UTF-8 bytes — **do not** strip prefix and base64-decode
- [x] Envelope JSON snake_case: `{ id, type, created_at, org_id, api_version, data }`
- [x] `data` for `payment.completed`: checkout_id, charge_id, amount, currency, provider, receipt number, payer_name optional

## W17.2 Tests

- [x] Compute then `TryVerify` round-trip
- [x] Tamper body fails
- [x] Stale timestamp fails inbound helper (300s)

## W17.3 Must not

- [x] Do not brand as Standard Webhooks
- [x] Do not emit Hub combined-only as the sole form (optional extra `t=,v1=` is P2, skip)

## W17.4 Exit

- [x] Unblocked for W18
