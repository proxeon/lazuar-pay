---
number: "023"
id: PAY-CAT-001
severity: P1
status: resolved
source: plans/019-evals/01-pay-host-seams.md
head: "9f04ad58"
---

# 023 — Catalog `product_id` is not money; amount is typed at mint

- **Severity:** P1 (product / honesty)
- **Status:** open
- **Source:** `plans/019-evals/01-pay-host-seams.md` B11 (also `10-honesty-bugs-gaps.md` P1-9)
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

Merchant create-pay-link POSTs a product `{ name, amount, currency }` then a payment-link with `product_id` **and a second copy of amount**. Host mint assigns `ProductId = body.ProductId.Trim()` with **no** `Products` lookup. Amount is `body.Amount`. Interval on the child is always `"one_off"` even if the price row said otherwise.

List label joins product names **without** `p.OrgId == orgId`. A guessed/leaked product id from another org can print that name on this org’s list. Catalog create rejects non-MYR; payment-link mint accepts any currency string (`ToUpperInvariant` only).

016 called catalog decorative. 018 sends `product_id` as a **label sidecar**. Staff who think “Dogfood RM10” is the catalog price are wrong if they edit the amount box independently (same dialog today, but API allows drift).

## Related files

- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` **16–62** — creates product **and** price.
- `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` **87–99** — amount from body; product_id stored raw.
- `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` **87–92**, **146–150** — same; names join without org filter.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **152–186** — product then link; same amount twice.
- `packages/pay-spec/main.tsp` — catalog create has **no body** (069).

## Reproduction

POST a product RM 99. POST a payment-link with that `product_id` and `amount: 10`. Link charges 10. List label still “whatever the product was named.”

## Blast radius

Wrong charge vs label. Cross-org name leak requires knowing a GUID (P2 tenancy). SST/Bar B MYR is only a catalog-create check.

## Suggested fix

If catalog is real: load `(orgId, productId)`, copy amount/currency/interval, 404 if missing. If catalog is a label: stop taking `product_id` as a money input, still filter names by `OrgId`, and say so in README. Either way, close the honesty gap. Prefer one money field.

038 (orphan products on link 400) gets easier if you stop creating a product per link.

## Tests

- Existing: `Create_product_as_owner` / `Member_cannot_create_product`.
- Missing: mint with other-org `product_id` does not leak the name; mint amount ≠ price amount is either 400 or documented label-only.

## Source reports

- `plans/019-evals/01-pay-host-seams.md` §B11 §G1
- `plans/019-evals/10-honesty-bugs-gaps.md` §P1-9
- `plans/019-evals/02-merchant-frontend.md` §G4
