# S45 — Webhook route + fulfill

**Track:** Sample app · **Analysis:** `../05`  
**Depends on:** S41, S42, S44  
**Goal:** Idempotent unlock on verified `payment.completed`.

---

## S45.1 Route wiring

- [x] Path matches provision webhook_url (chosen in S31)
- [x] `export const runtime = "nodejs"`
- [x] `export const dynamic = "force-dynamic"` (if needed)
- [x] `const rawBody = await request.text()` **first** — never `request.json()` before verify

## S45.2 Verify gate

- [x] Read `X-Lazuar-Signature` (case-insensitive header get)
- [x] Call verifySignature(secret, rawBody, header)
- [x] On fail → **401** (no domain side effects)

## S45.3 Parse envelope

- [x] `JSON.parse(rawBody)` after verify
- [x] Event type from header `X-Lazuar-Event` **or** body `event_type`
- [x] Payment fields from **`data`**, not top-level only
- [x] `order_id` from `data.metadata.order_id` (or map by `data.checkout_id`)
- [x] Missing checkout_id / mapping → **422** (not silent 200 if unprocessable)

## S45.4 Idempotency

- [x] Dedupe `X-Lazuar-Delivery-Id` (in-memory Set OK; note multi-instance limit)
- [x] Business dedupe: already paid / same gateway_transaction_id → 200 no-op
- [x] Replay test returns 200 without double transition

## S45.5 Fulfillment

- [x] `payment.completed` → mark order paid once; store delivery/event ids
- [x] `payment.failed` → mark failed if not paid; never unlock
- [x] Unknown events → 200 ignore (or documented policy)
- [x] Return **2xx** only after durable accept

## S45.6 Dev helper (optional same PR or S46)

- [x] Script to sign + POST fake webhook with local whsec (dev only)
- [x] Document: does not replace sandbox pay for full e2e

## S45.7 Exit

- [x] Fake signed completed → order paid
- [x] Bad signature → 401
- [x] Success page alone still does not pay
