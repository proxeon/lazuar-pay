---
number: "308"
id: B09-U40
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 308 — B09-U40 — Draft vs Archived

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U40 — Draft vs Archived (P2)

`ProductsPage.tsx` 211 vs `DashboardPage.tsx` 265.

## Evaluation (current tree, 2026-08-18)

### What the bug is
The same `product.is_active === false` row is labeled **Draft** on Checkout Links (`ProductsPage`) and **Archived** on the Sales Insights product catalog (`DashboardPage`). Product Form copy says the flag is “Active (Visible at Checkout)” — inactive means not buyable, not that the SKU was archived/deleted. There is no separate archive field on the product DTO. A merchant who unchecks Active (or never finished Resend and could not activate) sees “Draft” in one list and “Archived” in the other and will think they have two different product states. Dashboard rows are still not links, so they cannot click through to reconcile.

### Still present?
**STILL BROKEN**

```210:216:apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx
                      <td className="px-5 py-4">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
                          product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {product.is_active ? "Active" : "Draft"}
```

```265:271:apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx
                      <td className="px-5 py-3.5 text-right">
                         <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
                          product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {product.is_active ? "Active" : "Archived"}
```

Form source of truth:

```193:201:apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx
            <label className={cn("flex items-center gap-2 w-fit", hasValidEmailConfig ? "cursor-pointer" : "cursor-not-allowed opacity-60")}>
              <input 
                type="checkbox" 
                checked={isActive} 
                onChange={e => setIsActive(e.target.checked)} 
                disabled={isPending || !hasValidEmailConfig} 
                className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b] disabled:opacity-50" 
              />
              <span className="text-[12px] font-medium text-[#09090b]">Active (Visible at Checkout)</span>
```

Coupons and dunning campaigns also use “Archived” for `!is_active` (`CouponsPage.tsx` 125, `DunningCampaignsPage.tsx` 128) — different objects, same word, which makes the product-catalog mismatch worse.

### Related files
- `apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx` — Checkout Links badge “Draft”.
- `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` — catalog badge “Archived”; rows not clickable.
- `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` — “Active (Visible at Checkout)”.
- `apps/lazuar-ops/src/modules/commerce/pages/CouponsPage.tsx` / `DunningCampaignsPage.tsx` — same “Archived” word on other entities.

### Tests
- Existing tests that touch this path: none in ops. No product-list badge test in API either (activation is a boolean on the product write).
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: both surfaces render the same label for `is_active === false` (pick one word; “Inactive” or “Draft” matches the form). A fixture with one inactive product must not produce both “Draft” and “Archived” in the two pages.

### Reproduction today
Create a product, leave “Active (Visible at Checkout)” off (or turn it off). Open `/commerce/products`: badge is Draft. Open `/commerce/dashboard` Product Catalog: same name, badge is Archived. Assert they disagree.

### Blast radius
Every merchant with an unpublished or deactivated checkout link. Honesty only — checkout availability is still `is_active`. Confusion can cause a merchant to recreate a “lost” product or think archive (119) ran. No money, no PII.

### Suggested fix
Use one word on both pages. Prefer “Inactive” or “Draft” to match Product Form; reserve “Archived” for workspace/product archive (119) if that is a distinct action. Optional: make dashboard catalog rows link to `/commerce/products`. Do not add a second status column or a TypeSpec field.

### Evaluation notes
Called out in 009 §4.3 and U40. Not fixed by 141/144. Severity still P2. Not blocked. One-line label change.

