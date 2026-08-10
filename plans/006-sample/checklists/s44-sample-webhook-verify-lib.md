# S44 — Webhook signature verify library

**Track:** Sample app · **Analysis:** `../05-webhook-verify-nextjs.md`  
**Depends on:** S31  
**Goal:** Pure helper matching `OutboundWebhookSignature` before route wiring.

---

## S44.1 Implement `verifySignature`

- [x] File e.g. `lib/webhook-verify.ts`
- [x] Parse `X-Lazuar-Signature` parts `t` and `v1` (case-insensitive keys)
- [x] Reject missing secret/header/t/v1
- [x] Skew: `|now - t| > tolerance` (default 300s) reject
- [x] `signedPayload = `${t}.${rawBody}``
- [x] HMAC-SHA256 with **full** secret string as UTF-8 key (keep `whsec_` prefix)
- [x] Digest hex lowercase
- [x] Constant-time compare equal-length buffers
- [x] Node `crypto` (not browser)

## S44.2 Types

- [x] Envelope type: `{ id, event_type, created_at, data }`
- [x] Payment data type under `data` (checkout_id, metadata, amount, status, …)

## S44.3 Unit vector test

- [x] Fixed secret, fixed t, fixed body → known v1 (generate with python snippet from analysis 05)
- [x] Test fails if body one character changes
- [x] Test fails if secret wrong
- [x] Optional: stale t fails

## S44.4 Exit

- [x] Tests green without Next server
- [x] Comment cites monorepo `OutboundWebhookSignature.cs` as SSoT
