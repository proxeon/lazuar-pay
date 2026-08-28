---
number: "038"
id: PAY-MERCH-004
severity: P1
status: resolved
source: plans/019-evals/02-merchant-frontend.md
head: "9f04ad58"
---

# 038 — Writer busy flags have no `try/finally`; catalog orphans

- **Severity:** P1
- **Status:** resolved
- **Source:** `plans/019-evals/02-merchant-frontend.md` B4
- **HEAD:** `9f04ad58` (`feat/018-merchant-shell`)

Extracted from the 26 August 2026 Pay evaluation. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## What the bug is

`GatewayPage.pasteKey` sets `saving` true, awaits `payFetch`, then `setSaving(false)`. If fetch throws, Save stays disabled.

`createProductAndLink` is worse: product POST 201 then payment-link POST. Product `!ok` resets busy. A **throw** on either fetch, or a 400 on the **link** after product 201, leaves an orphan `products` + `prices` row. There is no catalog UI to see or delete it. Combined with 042, Production Test mint 400s after a product 201.

## Related files

- `apps/lazuar-pay-merchant/src/pages/org/GatewayPage.tsx` **83–116**.
- `apps/lazuar-pay-merchant/src/pages/org/CheckoutsPage.tsx` **152–194**.
- `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` — SaveChanges on product before the link exists.

## Reproduction

Create pay link; kill the network after product 201. Dialog stuck busy. GET products has a row the table never shows.

## Blast radius

Stuck dialogs; orphan catalog rows; retries create another product. Production Test (042) hits this every time.

## Suggested fix

`try/finally` for `saving`/`busy`. If link fails after product 201, show host `detail` **and** that a product was created. Better: stop creating a product per link (023). Do not close the dialog on link failure.

## Tests

- Missing: component/test that busy clears on throw. Host: no requirement to delete orphans if mint stops creating products.

## Source reports

- `plans/019-evals/02-merchant-frontend.md` §B4
- `plans/019-evals/04-processors-vault-test.md` Production Test + product 201
