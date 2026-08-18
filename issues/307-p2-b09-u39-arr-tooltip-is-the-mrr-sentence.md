---
number: "307"
id: B09-U39
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 307 — B09-U39 — ARR tooltip is the MRR sentence

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U39 — ARR tooltip is the MRR sentence (P2)

`DashboardPage.tsx` 77–78.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Sales Insights paints two KPI tiles, MRR and ARR, that share the exact same `title` tooltip: “Committed monthly equivalent of active memberships. Not cash. Past-due is excluded.” That sentence is a correct (post-041/042) description of *MRR*. ARR on this API is `mrr * 12` — an annualization of that monthly figure — but the tooltip never says “times twelve” or “annual.” The footnote under the grid repeats the merge: “MRR / ARR is the committed monthly equivalent…”. A merchant hovering ARR is taught the MRR definition. The *number* is not the monthly figure (unless they ignore `stats.arr` and the page falls back to `(mrr || 0) * 12`, which is the same math).

### Still present?
**STILL BROKEN**

Line numbers moved slightly after 144’s dashboard 403 work; the duplicate tip is still there:

```78:81:apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx
  const topMetrics = [
    { label: "Net Cash in Bank", value: financials?.forbidden ? "—" : formatMYR(financials?.data?.net_revenue || 0), icon: DollarSign },
    { label: "MRR", value: formatMYR(stats?.mrr || 0), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
    { label: "ARR", value: formatMYR(stats?.arr ?? ((stats?.mrr || 0) * 12)), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
```

```195:197:apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx
        <p className="text-[10px] text-[#71717a] -mt-2">
          MRR / ARR is the committed monthly equivalent of active memberships. Not cash. Past-due is excluded.{" "}
```

Backend confirms ARR is just 12× MRR:

```124:127:apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs
        return new CommerceStatsDto
        {
            Mrr = (double)mrr,
            Arr = (double)(mrr * 12m),
```

### Related files
- `apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` — duplicate tooltip + merged footnote.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs` — `Arr = mrr * 12`.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceMrrTests.cs` — MRR math; does not mention ARR copy.

### Tests
- Existing tests that touch this path: `CommerceMrrTests` (monthly equivalent, past-due excluded, ARPU). No ops dashboard test. No assertion that ARR tooltip ≠ MRR tooltip.
- Whether any test would fail if the bug is still there: **No.**
- What a first regression test should assert: ARR tip contains “× 12” / “annual” and does not equal the MRR tip string; footnote does not call ARR a monthly equivalent.

### Reproduction today
Sign in as a merchant with at least one ACTIVE monthly subscription. Open `/commerce/dashboard`. Hover the ARR tile (native `title` attribute). Assert: tooltip is the MRR sentence. Compare the ARR number to 12 × MRR — they match. Read the grey footnote: it names both KPIs as the monthly equivalent.

### Blast radius
Every merchant who uses Sales Insights. Honesty only — the ARR *value* is the annualized MRR, not a second cash figure. Risk is a merchant quoting “ARR” to a board or investor with the monthly definition in their head, or thinking ARR is independently computed. No PII, no charge path.

### Suggested fix
Give ARR its own tip: “MRR × 12. Same committed-membership rules as MRR; not cash collected this year.” Split the footnote the same way. Do not invent a second ARR formula, do not pull Stripe Billing ARR, do not change `CommerceQueryService.Stats`. No TypeSpec regen.

### Evaluation notes
009 table still listed “ARR tooltip = MRR” as OPEN. Adjacent money-definition work (041 interval, 042 ARPU/past-due) already landed; this is leftover copy. Severity still P2. Not blocked. Could be closed in a one-line string change.

