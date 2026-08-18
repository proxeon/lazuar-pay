---
number: "320"
id: B09-U52
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 320 — B09-U52 — `CommunityPortalView` dead

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U52 — `CommunityPortalView` dead (P2)

Unimported. Cancel-at-period-end lives on the aggregated page.

## Evaluation (current tree, 2026-08-18)

### What the bug is
`CommunityPortalView` is a leftover buyer UI from the community/courses era (Telegram/Zoom “access links”, “lose access to the community”, cancel-at-period-end). It is not imported by any route. Cancel-at-period-end, keep-plan, and documents live on the aggregated `app/[tenantSlug]/portal/page.tsx`. The audit filed this so nobody treats the island as the live portal or remounts it thinking cancel is missing. Buyers are not blocked; the file is dead weight that still posts `at_period_end: true` with copy about a community that does not exist.

### Still present?
**STILL BROKEN**

`CommunityPortalView` still exists and is still unimported. Repo-wide `CommunityPortalView` hits are only the file’s own interface/export (`apps/lazuar-portal/src/modules/community/components/CommunityPortalView.tsx:11,22`). There is no `import` from `app/`, checkout, or portal modules.

The island still owns a cancel POST that is **not** what buyers click:

```27:39:apps/lazuar-portal/src/modules/community/components/CommunityPortalView.tsx
  const handleCancel = async () => {
    if (!window.confirm("Are you sure you want to cancel your subscription? You will lose access at the end of your billing cycle.")) {
      return;
    }
    ...
      const { error: apiError } = await browserClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
        params: { path: { tenantSlug }, query: { token } },
        body: { subscription_id: sub.id, at_period_end: true }
      });
```

Live cancel is on the aggregated page (server actions, period-end + immediate + keep):

```13:28:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
async function cancelPortalSubscription(
  tenantSlug: string,
  token: string,
  subscriptionId: string,
  atPeriodEnd: boolean,
) {
  "use server";
  const { error } = await serverClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
    params: { path: { tenantSlug }, query: { token } },
    body: { subscription_id: subscriptionId, at_period_end: atPeriodEnd },
  });
```

```165:184:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx
                    {isHealthyForCancel && (
                      <>
                        <form action={cancelPortalSubscription.bind(null, tenantSlug, accessToken, sub.id, true)}}>
                          <button ...>Cancel Plan</button>
                        </form>
                        <form action={cancelPortalSubscription.bind(null, tenantSlug, accessToken, sub.id, false)}}>
                          <button ...>Cancel immediately</button>
                        </form>
                      </>
                    )}
                    {isFlagged && (
                      <form action={keepPortalSubscription.bind(null, tenantSlug, accessToken, sub.id)}}>
```

Sibling leftover: `apps/lazuar-portal/src/modules/community/lib/api.ts` is a checkout-client clone (`validateCouponCode`, `submitCheckout`) and is also unimported. Landing copy no longer claims “courses, and downloads” (`app/page.tsx:14` is now “subscriptions and receipts”) — that honesty line from the audit was cleaned; the component was not deleted.

### Related files
- `apps/lazuar-portal/src/modules/community/components/CommunityPortalView.tsx` — dead view (Telegram/Zoom placeholders `href="#"`).
- `apps/lazuar-portal/src/modules/community/lib/api.ts` — unused client next to it.
- `apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx` — live cancel / keep / documents.
- `apps/lazuar-portal/src/app/page.tsx` — landing; courses/downloads sentence is gone.
- `docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` — floating islands are intentional unless remounted.
- `plans/009-bugs/09-frontends-ops-portal-admin.md` §8 — lists this file as “unread / not mounted.”

### Tests
- No portal test imports `CommunityPortalView`. Portal tests are `i18n.test.mjs` and `grossBreakdown.test.mjs` only.
- Nothing fails if the file stays or if someone remounts it (the danger).
- First regression test if you delete: `rg CommunityPortalView` is empty. If you keep it: a comment-only guard is not a test; add a lint/architecture grep that `app/` does not import `modules/community`.

### Reproduction today
Arrange: token portal URL `/{tenantSlug}/portal?token=…` for an ACTIVE sub. Act: load the page. Assert: you see “Active Subscriptions” / “Cancel Plan” / “Cancel immediately” from `portal/page.tsx`, not “Join Private Telegram Group” / “Weekly Zoom Access.” Act: `rg CommunityPortalView apps/lazuar-portal` — only the component file. Act: navigate to a path that would have been a community portal — there is no such route.

### Blast radius
Buyers: none, as long as nobody remounts it. Remount risk: fake Telegram/Zoom links (`href="#"`), cancel that sets status to `"CANCELED"` locally without `cancel_at_period_end` (line 43), copy about “community resources.” Ops: none. Money: a remounted broken cancel could confuse period-end vs immediate. Frequency: zero user hits today.

### Suggested fix
Delete `modules/community/components/CommunityPortalView.tsx` and `modules/community/lib/api.ts` (or leave them unimported and do not touch them — ADR 023 tree-shake). Do **not** remount. Do not add courses/downloads. Live cancel already lives on the aggregated page; LP-059 / next-renewal-only is a Billing concern, not this file. No TypeSpec. No WhatsApp.

### Evaluation notes
Inventory / leftover, still P2 only because remount would lie. Sister of 319 (ops community filter) and 321 (do not remount chat). Section 8 of the source audit says unimported islands are “not bugs unless someone remounts them”; U52 exists so the next person does not hunt cancel in this file. Landing “courses and downloads” was independently cleaned — partial honesty, component still dead.

