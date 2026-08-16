# W1-LP-142 — Idempotency-Key on POST

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-142`. Tracker: *Idempotency-Key on POST* — Lazuar **P**.  
**Not this ID:** inbound gateway webhook event-id / business-key (`LP-090` **Y**). Credit-deduction logs. Stripe off-session keys. Do not wrap every human Ops POST (create product, save email).

**Invariant:** Retrying a **money or document create** with the same `Idempotency-Key` and same fingerprint returns the **same** result and does not double-charge / double-submit. Same key + different body → **409**.

---

## 0. Scope lock

In scope (Wave 1 money/integrator POSTs that still lack it):

- Public Commerce `POST /public/commerce/checkout` (hosted buy)
- Public `POST /public/commerce/checkout/{subId}/update-payment`
- Admin `POST /admin/commerce/transactions/{id}/refund` (optional but same ticket if cheap)

Already done — **do not rewrite**, only document:

- `POST /integrations/payments/checkouts` (header **or** body; optional)
- `POST /lhdn/documents` (header **required**)
- Internal credit deduct / webhook logs

Out of scope:

- Global middleware on **all** POST (register, login, create workspace)
- Making Payments cashier key **required** (optional is Stripe-like)
- Portal cancel / admin cancel (safe to retry today; not a charge)

---

## 1. Verdict

Two integrator surfaces already implement Stripe-shaped keys. **Hosted checkout does not.** Double-click “Pay” or a flaky mobile retry can open **two** Billplz bills.

| POST | Header | Behavior |
|------|--------|----------|
| `/integrations/payments/checkouts` | Optional `Idempotency-Key` | Replay same fingerprint; 409 on mismatch; unique (org, key) |
| `/lhdn/documents` | **Required** | Return existing doc id |
| `/public/commerce/checkout` | **None** | New `CheckoutSession` every time |
| `/checkout/{id}/update-payment` | **None** | May mint another recovery checkout (partially cached by `CurrentRenewalCheckoutUrl`) |
| `/admin/commerce/transactions/{id}/refund` | **None** | Gateway refund risk on double click |
| Credit deduct | Internal key | Already unique |

Tracker **P** is exact.

---

## 2. Current files

### 2.1 Payments (template to copy)

| Path | Role |
|------|------|
| `IntegrationEndpoints.cs` `ResolveIdempotencyKey` | Header wins over body |
| `CreateIntegrationCheckoutCommandHandler.cs` | Fingerprint + unique index |
| `IntegrationCheckoutSession.IdempotencyKey` | max 200 |
| `packages/api-spec/modules/payments/routes.tsp` | Optional header |

### 2.2 LHDN

| Path | Role |
|------|------|
| `DocumentEndpoints.cs` | 400 if header missing |
| `SubmitTaxDocumentCommandHandler` | Log table unique (org, key) |
| `lhdn-sdk-dotnet` | Auto-adds header if absent |

### 2.3 Commerce public (the hole)

| Path | Role |
|------|------|
| `PublicCheckoutEndpoints.cs` | `InitiateCheckoutCommand` — new OPEN session |
| `InitiateCheckoutCommandHandler.cs` | Always insert |
| `PublicArrearsEndpoints.cs` | Reuses stored URL if same billing date; else new gateway session |

---

## 3. Gaps

### G1 — Hosted checkout double-create (P0)

Buyer retries POST → two OPEN sessions → two bills → two possible captures (second may fail or double-charge depending on rail).

### G2 — Update-payment race

Cache on `CurrentRenewalCheckoutUrl` helps same-day retries **without** a client key. Concurrent first clicks can still dual-mint. Header makes it explicit.

### G3 — Refund double-click

Ops UI has no disable-on-submit guarantee across tabs.

### G4 — No shared host primitive

Each module rolled its own table. Wave 1 should **not** invent a platform-wide idempotency middleware unless it is thin (header parse + hash body). Prefer **per-command** like Payments.

**Not gaps**

- Webhook inbound idempotency (LP-090).
- Optional cashier key (leave optional).

---

## 4. Minimal changes

### 4.1 Must — public checkout

| File | Change |
|------|--------|
| `public-routes.tsp` | Optional `@header("Idempotency-Key")` on `POST /checkout` |
| `PublicCheckoutEndpoints.cs` | Read header; pass into `InitiateCheckoutCommand` |
| `InitiateCheckoutCommand` + handler | If key present: lookup existing OPEN/COMPLETED session for (org, key); same fingerprint (tenant, product, email, coupon, amount) → return same `CheckoutResultDto`; mismatch → 409; insert key on new session |
| Migration | `commerce.CheckoutSessions.IdempotencyKey` nullable + unique (OrganizationId, IdempotencyKey) WHERE key IS NOT NULL |

Fingerprint: `productId + email + coupon + quantity + sessionId?`. Do not include timestamps.

Missing header: keep today’s behavior (new session). Portal should **send** a key (uuid in `sessionStorage` per form mount) so retries are safe.

| `CheckoutForm.tsx` | `Idempotency-Key: crypto.randomUUID()` stored in `sessionStorage` for that product slug until success |

### 4.2 Should — update-payment + refund

- Arrears POST: accept header; if key matches stored issuance, return same URL.  
- Refund: persist `(org, idempotencyKey) → refund request id` or rely on gateway + existing “already refunded” — if `RecordRefundCommand` is not idempotent, add a key column.

### 4.3 Do not

- Require the header on public checkout (browsers without the new bundle).  
- One global table for login POST.  
- Change LHDN required-header.

---

## 5. Tests

Extend Commerce initiate tests + new cases:

| Case | Expect |
|------|--------|
| Two POST same key + same body | One `CheckoutSessions` row; same URL / session id |
| Same key + different product | 409 |
| No header | Two sessions (legacy) |
| Key > 200 chars | 400 |
| Update-payment same key | Same checkout URL |

Payments cashier tests already cover the pattern — do not duplicate.

Portal: no runner; manual double-submit.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Unique index race | Same catch-and-replay as `CreateIntegrationCheckoutCommandHandler` |
| Fingerprint too strict (email typo retry) | Email is part of identity; OK |
| Fingerprint too loose | Include product + coupon |

---

## 7. Acceptance

1. Public checkout with `Idempotency-Key` is replay-safe.  
2. Same key + different payload → 409, no second charge session.  
3. Without header, behavior unchanged.  
4. Portal form sends a key.  
5. Cashier + LHDN behavior unchanged.  
6. Tests §5 pass.  
7. Tracker **P → Y** if checkout + (update-payment **or** documented residual) ship. Prefer update-payment in the same PR.

---

## 8. Implement order

1. Session column + initiate handler  
2. Public endpoint + TypeSpec + portal header  
3. Update-payment / refund if time  
4. Tests  
