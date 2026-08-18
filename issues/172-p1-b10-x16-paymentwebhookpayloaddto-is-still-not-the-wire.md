---
number: "172"
id: B10-X16
severity: P1
status: resolved
resolved_branch: fix/172-payment-webhook-dto-honesty
source: plans/009-bugs/10-tenancy-workers-contracts-tests.md
head: "297ba98"
---

# 172 — B10-X16 — `PaymentWebhookPayloadDto` is still not the wire

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/009-bugs/10-tenancy-workers-contracts-tests.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)
- **Resolved on:** `fix/172-payment-webhook-dto-honesty`

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B10-X16 — P1 — `PaymentWebhookPayloadDto` is still not the wire

```50:66:packages/api-spec/modules/payments/models.tsp
/**
 * Outbound payment.* webhook envelope (snake_case JSON body).
 * Signed with X-Lazuar-Signature: t=…,v1=… (HMAC-SHA256 of "{t}.{rawBody}").
 */
model PaymentWebhookPayloadDto {
  event_id: string;
  event_type: "payment.completed" | "payment.failed";
  checkout_id: string;
  workspace_id: string;
  ...
}
```

Runtime wrap is `{ id, event_type, created_at, data: { ... } }`. The DTO is flat, claims to be the envelope, invents `workspace_id` / `occurred_at`, omits `provider_session_id` / `description` / `customer_email` / `gateway` as `data` fields. Generated `FromJson` against a live delivery will not bind `data.*`.

VitePress `events.md` line 31 warns. TypeSpec comment still lies. Sample `examples/hub-cashier-next/lib/types.ts` is the honest client. `@repo/api-types-ts` is not.

008 H3. Still present after `cbe17c2`.

