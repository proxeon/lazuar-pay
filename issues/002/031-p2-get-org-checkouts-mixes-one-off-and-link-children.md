---
number: "031"
id: PAY-CHK-002
severity: P2
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 031 — GET org checkouts mixes one-off mints and occupancy children

- **Severity:** P2
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B13
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`GET /v1/orgs/{orgId}/checkouts` is `Where(x => x.OrgId == orgId)` with no `PaymentLinkId == null` filter. Every `MintOrResume` child appears as a checkout with its own `public_token`. Merchant Vite lists **payment-links**, not this door — a kernel client that sums both lists double-counts.

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **124–164**.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` **105–154** — list links separately with occupancy.

## Reproduction

Mint a link, start one slot. GET org checkouts includes the child. GET payment-links includes the parent. Two rows for one pay.

## Blast radius

Kernel / README clients. Merchant UI currently dodges it.

## Suggested fix

Filter children out (`PaymentLinkId == null`), **or** mark `{ kind: "one_off" | "link_child", payment_link_id }` and document it in pay-spec.

## Tests

- Missing: mint a link, start one slot, checkout list shape does not double-count (or includes `kind`).

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B13
- `plans/019-evals/08-contracts-spec-honesty.md` org checkouts door
