---
number: "319"
id: B09-U51
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 319 — B09-U51 — Community leftover fulfillment still filtered, labels still exist

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U51 — Community leftover fulfillment still filtered, labels still exist (P2)

`utils.ts` hides `internal:community`. Chat `CreateProductForm` still has WhatsApp. Not on a live route.

## Evaluation (current tree, 2026-08-18)

### What the bug is
ADR 023 / ADR 022 removed community (and vault) from the live CaaS product. The catalog still carries leftover `internal:community` / `internal:vault` fulfillment targets on old product rows, so ops hides those strings with `filterHiddenFulfillmentTargets`. That filter is still in the live product list, editor, and detail panel — a community-era shim. Separately, the **unrouted** chat `CreateProductForm` still labels the phone checkbox “Require WhatsApp Number.” The audit’s point: community is gone, but the UI still knows how to hide it and still talks about WhatsApp, which is Wave 5 and not connected. The chat form is not on a live route (`[MVP-HIDE]`). The live `ProductForm` *also* still says WhatsApp.

### Still present?
**STILL BROKEN**

The hide list is unchanged:

```8:12:apps/lazuar-ops/src/lib/utils.ts
const HIDDEN_INTERNAL_TARGETS = ["internal:community", "internal:vault"];

export function filterHiddenFulfillmentTargets(targets: string[] | undefined): string[] {
  return (targets ?? []).filter(t => !HIDDEN_INTERNAL_TARGETS.some(h => t.toLowerCase().startsWith(h)));
}
```

It is imported on live commerce surfaces:

- `ProductsPage.tsx:8,72` — badges skip hidden targets.
- `ProductForm.tsx:5,69,81` — editor textarea is pre-filtered and re-filtered on submit.
- `ProductDetailPanel.tsx:6,118` — detail list is filtered.

Chat form (only imported from `FormRegistry.ts` → `CreateProductCommand`) still has WhatsApp copy:

```135:138:apps/lazuar-ops/src/components/forms/CreateProductForm.tsx
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresPhone} onChange={e => setRequiresPhone(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require WhatsApp Number</span>
            </label>
```

`App.tsx:306–308` still comments out `/ops/chat`, so that form is not reachable. A *second* unused clone `modules/commerce/components/CreateProductForm.tsx:90` has the same label and has **no importers** (live create uses `CreateProductModal` → `ProductForm`). Live `ProductForm.tsx:227` and `ProductDetailPanel.tsx:240` still say “Require(s) WhatsApp Number” on the mounted checkout-link editor. Portal checkout `messages.ts` `form.phone` is still “WhatsApp Number.”

### Related files
- `apps/lazuar-ops/src/lib/utils.ts` — `HIDDEN_INTERNAL_TARGETS` / `filterHiddenFulfillmentTargets`.
- `apps/lazuar-ops/src/modules/commerce/pages/ProductsPage.tsx` — live badge filter.
- `apps/lazuar-ops/src/modules/commerce/components/ProductForm.tsx` — live editor filter + WhatsApp checkbox.
- `apps/lazuar-ops/src/modules/commerce/components/ProductDetailPanel.tsx` — live “Requires WhatsApp Number.”
- `apps/lazuar-ops/src/components/forms/CreateProductForm.tsx` — chat-only form (WhatsApp label).
- `apps/lazuar-ops/src/modules/commerce/components/CreateProductForm.tsx` — unused duplicate.
- `apps/lazuar-ops/src/components/chat/FormRegistry.ts` — only importer of the chat form.
- `apps/lazuar-ops/src/App.tsx` — chat route still `[MVP-HIDE]`.
- `apps/lazuar-portal/src/modules/checkout/i18n/messages.ts` — buyer-facing “WhatsApp Number.”
- `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` / `022-remove-community-vault-modules.md` — why community is leftover.

### Tests
- No frontend test of `filterHiddenFulfillmentTargets`. No test that product forms omit `internal:community` or the WhatsApp label.
- API: `OrderCompletedDigitalDeliveryHandlerTests` mentions `"internal:vault/abc"` as a fulfillment target in a comms fixture — not an ops UI test.
- Nothing fails today if the leftovers stay. A first regression test: unit-test the filter (`internal:community/foo` dropped, `https://hooks.example` kept). For honesty, snapshot or grep-test that the *live* `ProductForm` label is “phone” not “WhatsApp” if product agrees. Do not add a WhatsApp send path.

### Reproduction today
Arrange: any OrgAdmin in ops, Products. Act: open Create / edit a checkout link. Assert: checkbox copy is still “Require WhatsApp Number”; fulfillment textarea never shows `internal:community` even if the API product still has it (filter on load). Act: visit `/ops/chat` — 404 (`NotFoundPage` after 156), not the chat form. Grep `internal:community` — only `utils.ts`. Grep `Require WhatsApp Number` — three ops forms + portal `form.phone`.

### Blast radius
Not money. Honesty / Wave-5 leak: merchants think WhatsApp collection is a live channel; Billing Settings elsewhere says WhatsApp is not connected. Community filter is defensive and mostly harmless. Frequency: every product create/edit. PII: phone numbers collected under a WhatsApp label. Wrap-rail: do **not** implement WhatsApp delivery to “fix” the label.

### Suggested fix
1. Rename live `ProductForm` / `ProductDetailPanel` / portal `form.phone` to “phone number” (checkout already has a phone field). Do not wire Meta/WhatsApp.  
2. Keep `filterHiddenFulfillmentTargets` until a data backfill deletes `internal:community` / `internal:vault` from `fulfillment_targets`, then delete the helper.  
3. Do not remount chat (321). Optional: delete unused `modules/commerce/components/CreateProductForm.tsx`. No TypeSpec regen.

### Evaluation notes
Still P2 leftover / honesty, not a live community route. Overlaps portal phone copy (U-adjacent checkout i18n) and 321 (do not remount chat). 320 is the portal-side dead community island. Wave 5 / homemade e-mandate / Xero are out of scope.

