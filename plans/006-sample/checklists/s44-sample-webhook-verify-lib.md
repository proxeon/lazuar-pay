# S44 — Webhook signature verify library

**Track:** Sample app · **Analysis:** `../05-webhook-verify-nextjs.md`  
**Depends on:** S31  
**Goal:** Pure helper matching `OutboundWebhookSignature` before route wiring.

---

## S44.1 Implement `verifySignature`

- [ ] File e.g. `lib/webhook-verify.ts`
- [ ] Parse `X-Lazuar-Signature` parts `t` and `v1` (case-insensitive keys)
- [ ] Reject missing secret/header/t/v1
- [ ] Skew: `|now - t| > tolerance` (default 300s) reject
- [ ] `signedPayload = `${t}.${rawBody}``
- [ ] HMAC-SHA256 with **full** secret string as UTF-8 key (keep `whsec_` prefix)
- [ ] Digest hex lowercase
- [ ] Constant-time compare equal-length buffers
- [ ] Node `crypto` (not browser)

## S44.2 Types

- [ ] Envelope type: `{ id, event_type, created_at, data }`
- [ ] Payment data type under `data` (checkout_id, metadata, amount, status, …)

## S44.3 Unit vector test

- [ ] Fixed secret, fixed t, fixed body → known v1 (generate with python snippet from analysis 05)
- [ ] Test fails if body one character changes
- [ ] Test fails if secret wrong
- [ ] Optional: stale t fails

## S44.4 Exit

- [ ] Tests green without Next server
- [ ] Comment cites monorepo `OutboundWebhookSignature.cs` as SSoT
