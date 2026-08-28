# E14 — Sample verifies One-dialect HMAC

**Track:** E · **Depends:** E13, W21  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.3  
**Goal:** Raw body + `v1=` + timestamp. Not Stripe, not Standard Webhooks npm.

**Why:** Hub sample verifies Hub HMAC. One `examples/node-webhook-verify` is the judgment. Pay outbound (W17) emits One dialect. npm `standardwebhooks` would **mis-verify**.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookSignature.cs` | Algorithm |
| Sibling `examples/node-webhook-verify` | Copy **judgment** |
| `examples/hub-cashier-next` webhook route | Museum — do not copy Stripe/Hub verify |
| W21 | Pay sends these headers |

**Current (`6d730d15`):** No Pay sample verify.

---

## E14.1

- [ ] HTTP listener reads **raw** body
- [ ] Verify with HMAC-SHA256 hex of `{timestamp}.{body}` using full `PAY_WEBHOOK_SECRET`
- [ ] Headers: `X-Lazuar-Signature`, `X-Lazuar-Timestamp`
- [ ] 401 on fail; 200 `{ ok: true }` on pass
- [ ] Ignore unknown `type` except `payment.completed` (and `webhook.test` if W30)

## E14.2 Must not

- [ ] Do not `JSON.parse` before HMAC
- [ ] Do not use Hub combined-only as the only parser unless you also accept split

## E14.3 Exit

- [ ] Unblocked for E15
