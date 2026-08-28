---
number: "020"
id: PAY-CHK-001
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 020 — Checkout idempotency is racy, body-blind, and always 201

- **Severity:** P1
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B8
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`CheckoutStore.CreateAsync`: lookup `(OrgId, Key)`; if checkout exists, return it; else insert checkout **and** key in one `SaveChanges`. No transaction around lookup+insert. Two concurrent `Idempotency-Key: k1` both miss, second hits PK unhandled → 500. If the key row exists but the checkout was deleted, the code falls through and tries a **duplicate key**. Replay returns **201** even when it returned the old row. The key is not hashed to `{amount, currency, provider, org}`. Same key with a different amount silently returns the first checkout.

Payment-link create has **no** idempotency header at all. The merchant SPA uses payment-links, not this door — kernel/README curl still does.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutStore.cs` **9–53**.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **76–99** — header + body key; always 201.
- `apps/lazuar-pay/src/Lazuar.Pay/Data/PayDbContext.cs` **59–63** — PK `(OrgId, Key)`.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` — no idempotency.
- Checkout tests: sequential `Create_idempotent_on_key`.

## Reproduction

Concurrent POST `/v1/checkouts` with the same `Idempotency-Key`. One 201; the other 500 on Postgres. Replay with a different `amount` returns the first checkout as 201.

## Blast radius

Double-click mint on the kernel door. Wrong amount if a client reuses a key. Merchant UI is on payment-links (orphan product is 038).

## Suggested fix

Catch duplicate key, re-read, return the original with **200** if a stored fingerprint matches, **409** if it does not. Store a hash of the canonical body. Add the same header to `POST /v1/payment-links`. Test concurrent replay against Postgres.

## Tests

- Existing: sequential idempotent create.
- Missing: concurrent same key; same key different body 409; payment-link idempotency; replay status 200.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B8
- `plans/019-evals/08-contracts-spec-honesty.md` idempotency only on checkouts
