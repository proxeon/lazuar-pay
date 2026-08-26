---
number: "245"
id: B06-D07
severity: P2
status: resolved
resolved_branch: fix/245-productform-tin-subtitle
source: plans/009-bugs/06-lhdn-invoices-documents.md
head: "297ba98"
---

# 245 — B06-D07 — ProductForm subtitle: “We do not validate the TIN at checkout”

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/06-lhdn-invoices-documents.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

### B06-D07 — ProductForm subtitle: “We do not validate the TIN at checkout” (P2)

**Status:** open leftover lie.

```221:222:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. We do not validate the TIN at checkout.</span>
```

`CheckoutForm.tsx:96–110` calls `validateTin`. The subtitle is false. Quotes, which **don’t** validate, have no such warning.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops Product form, next to “Require Company Name & Tax ID (LHDN B2B)”, used to tell merchants “We do not validate the TIN at checkout.” Product checkout already called `POST /public/commerce/{slug}/validate-tin` before hop-1. The subtitle taught the opposite of the buyer path and would be repeated in demos. Quotes were a separate leftover: they collected TIN without `validate-tin` and had no warning.

### Still present?
**ALREADY FIXED**

The cited ProductForm subtitle was rewritten in `7548660c` (`fix(ops,portal): say TIN is validated at checkout when MyInvois is connected`) as part of **145** (`fix/145-tin-copy`):

```221:222:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. Checkout validates the TIN against MyInvois when LHDN is connected.</span>
```

Product checkout still validates (`CheckoutForm.tsx:96–110` calls `validateTin`; unavailable MyInvois is `TinValidationUnavailableError` and does not block). Checkout copy was aligned in the same 145 commit (`apps/lazuar-portal/src/modules/checkout/i18n/messages.ts:23` `form.taxIdHint`: “We validate this TIN / ID pair against MyInvois before pay.”). Repo-wide grep of `We do not validate the TIN at checkout` is empty.

The audit’s “quotes don’t validate” half is also gone: **014** (`e25d07d6`) made `QuoteView.tsx:87–101` call the same `validateTin` helper.

Residual copy: `apps/lazuar-ops/src/modules/commerce/components/CreateProductForm.tsx:86` and `apps/lazuar-ops/src/components/forms/CreateProductForm.tsx:133` still have the B2B checkbox **without** any subtitle (neither the old lie nor the new sentence). That is not the cited D07 string.

### Related files
- `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` — the cited subtitle (now honest).
- `apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx` — product `validateTin` before submit.
- `apps/lazuar-portal/src/modules/checkout/lib/api.ts` — `validateTin` + `TinValidationUnavailableError` (409).
- `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` — buyer-facing tax-id hint (145).
- `apps/lazuar-portal/src/modules/checkout/components/QuoteView.tsx` — quote path now validates (014).
- `apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/PublicTinValidationEndpoints.cs` — public validate-tin route.
- `issues/145-p1-b09-u16-product-form-and-checkout-disagree-about-tin.md` — the P1 that already rewrote this string.
- `issues/014-p0-b06-d04-quoteview-collects-tin-only-no-id-pair-no-validate-tin.md` — quotes now call validate-tin.

### Tests
- No frontend test under `apps/lazuar-ops` or `apps/lazuar-portal` asserts the ProductForm subtitle string (grep of the old/new copy in `*.test.*` / `*.spec.*` is empty).
- Backend TIN tests (`MyInvoisLoopTests.Submit_GeneralPublic_DoesNotValidateTin`, `LhdnRateLimitingTests`, public validate-tin endpoints) do not lock ops copy.
- No test would fail if the old lie were restored.
- First regression (if this issue is still “open” only as paper): a snapshot or string assert that `ProductForm.tsx` contains “Checkout validates the TIN” and does **not** contain “We do not validate the TIN at checkout.”

### Reproduction today
Open ops → product edit → “Require Company Name & Tax ID.” Assert the helper text says checkout validates when LHDN is connected. On the portal product checkout with that flag, submit a bad TIN/ID pair and assert hop-1 is blocked by `validateTin`. On a quote with B2B required, the same validate-tin call now runs.

### Blast radius
Was merchant-facing honesty (P2 leftover lie), not a money or PII defect. Demo risk is gone on the cited ProductForm. Create-product chat/forms still omit the explanation. Frequency was every B2B product configure.

### Suggested fix
No product code change required for D07. Optionally add the same one-line helper to the two `CreateProductForm` checkboxes so all three surfaces match 145. Do not turn off checkout `validateTin`. Do not TypeSpec-regen. Do not mark YAML resolved here (paper trail only).

### Evaluation notes
Superseded by **145** (P1, resolved) plus **014** for quotes. Severity as a live lie is no longer P2; leftover is the open YAML status and the two subtitle-less create forms. Not blocked. Not a 161–200 fail-closed residual.

## Resolution

ProductForm already honest (145). Both CreateProductForm checkboxes now use the same helper. `OpsTinCopyTests` locks the validate-TIN sentence and forbids the old lie.

