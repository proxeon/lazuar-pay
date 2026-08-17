---
number: "030"
id: B01-C04
severity: P1
status: resolved
source: plans/009-bugs/01-commerce-checkout-activation.md
head: "297ba98"
resolved_branch: fix/030-idempotency-replay-open-only
---

# 030 — B01-C04 — Idempotency replay returns EXPIRED URLs and empty-URL rows fall through to a second insert

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/01-commerce-checkout-activation.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/030-idempotency-replay-open-only`

Replay only OPEN unexpired URLs. OPEN rows without a URL resume mint. EXPIRED/COMPLETED rows release the key so a new session can be created.

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B01-C04 — Idempotency replay returns EXPIRED URLs and empty-URL rows fall through to a second insert

**Severity:** P1  
**One-sentence fault:** Replay is “same fingerprint + non-empty URL”, not “same fingerprint + still OPEN”; a first save without a URL is not treated as in-flight.

**Evidence.** See the block in §4.1 (`InitiateCheckoutCommandHandler.cs` 71–88). Unique index:

```218:220:apps/lazuar-api/Modules/Commerce/Infrastructure/CommerceDbContext.cs
            builder.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
```

Race catch (261–280) only returns if the other row **has** a URL; otherwise it rethrows the unique violation.

**Reproduction in words.**

1. **Expired replay.** Buyer initiates, gets a hop-2 URL, walks away 25 hours. Expiry job marks EXPIRED and releases the coupon. Same tab retries (portal reuses sessionStorage UUID). Handler finds the EXPIRED row, fingerprint matches, URL is set, returns that Stripe/Billplz link. Buyer pays. Webhook: session status is not OPEN → `HandleSubscriptionPaymentAsync` finds no subscription with that id → no-op. Processor has the money. Commerce has no Order / Subscription.
2. **Empty URL.** First request inserts the session + reserve, then `GenerateCheckoutSessionQuery` throws (CHIP missing brand id, Stripe key rejected). `GatewayCheckoutUrl` is still null. Retry: existing found, no URL, fall through, `AddCheckoutSession` another row with the same key, unique violation, catch sees no URL, rethrow → HTTP 400 with a database message. Coupon remains reserved on the orphan OPEN session until expiry.

**Blast radius.** Anyone using `Idempotency-Key` (the hosted portal always does). Abandoned hop-2 after 24h is the common case. Gateway-down first attempt is the support case.

**Why tests missed it.** `CommerceCheckoutIdempotencyTests` only test normalize and fingerprint change. No handler test for EXPIRED replay or missing URL.

**Fix direction.** Replay only `OPEN && ExpiresAt > now && URL present`. If OPEN and URL missing, resume mint on **that** row (do not insert). If EXPIRED / COMPLETED, mint a new session **or** reject with a typed error; do not hand back a dead processor URL. Catch unique violations and wait/re-read until the winner has a URL or has failed.

---

