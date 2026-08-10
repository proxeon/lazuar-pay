# S45 — Webhook route + fulfill

**Track:** Sample app · **Analysis:** `../05`  
**Depends on:** S41, S42, S44  
**Goal:** Idempotent unlock on verified `payment.completed`.

---

## S45.1 Route wiring

- [ ] Path matches provision webhook_url (chosen in S31)
- [ ] `export const runtime = "nodejs"`
- [ ] `export const dynamic = "force-dynamic"` (if needed)
- [ ] `const rawBody = await request.text()` **first** — never `request.json()` before verify

## S45.2 Verify gate

- [ ] Read `X-Lazuar-Signature` (case-insensitive header get)
- [ ] Call verifySignature(secret, rawBody, header)
- [ ] On fail → **401** (no domain side effects)

## S45.3 Parse envelope

- [ ] `JSON.parse(rawBody)` after verify
- [ ] Event type from header `X-Lazuar-Event` **or** body `event_type`
- [ ] Payment fields from **`data`**, not top-level only
- [ ] `order_id` from `data.metadata.order_id` (or map by `data.checkout_id`)
- [ ] Missing checkout_id / mapping → **422** (not silent 200 if unprocessable)

## S45.4 Idempotency

- [ ] Dedupe `X-Lazuar-Delivery-Id` (in-memory Set OK; note multi-instance limit)
- [ ] Business dedupe: already paid / same gateway_transaction_id → 200 no-op
- [ ] Replay test returns 200 without double transition

## S45.5 Fulfillment

- [ ] `payment.completed` → mark order paid once; store delivery/event ids
- [ ] `payment.failed` → mark failed if not paid; never unlock
- [ ] Unknown events → 200 ignore (or documented policy)
- [ ] Return **2xx** only after durable accept

## S45.6 Dev helper (optional same PR or S46)

- [ ] Script to sign + POST fake webhook with local whsec (dev only)
- [ ] Document: does not replace sandbox pay for full e2e

## S45.7 Exit

- [ ] Fake signed completed → order paid
- [ ] Bad signature → 401
- [ ] Success page alone still does not pay
