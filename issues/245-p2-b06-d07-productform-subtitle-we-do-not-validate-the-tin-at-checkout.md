---
number: "245"
id: B06-D07
severity: P2
status: open
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

