---
number: "300"
id: B09-U32
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 300 — B09-U32 — Utility Ledger is a secret route

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U32 — Utility Ledger is a secret route (P2)

Mounted at `/workspace/ledger`. Not in the sidebar. Credits history is hidden next to a top-up form that does not link to it.

## Evaluation (current tree, 2026-08-18)

### What the bug is
Ops mounts a real Utility Ledger page at `/workspace/ledger`. It lists `GET /admin/billing/credits` `recent_transactions` (top-ups and LHDN deducts). The sidebar Workspace accordion has General, Team, Audit, Legal & Billing, Payment Gateways, Plan & billing, Email Provider — no Ledger. Plan & billing (`BillingSettingsPage`) shows the credit balance and the top-up form and does not link to the ledger. A merchant can buy credits and never see the history unless they guess the URL. 008 already filed this; it is still unlinked.

### Still present?
**STILL BROKEN**

Route exists:

```300:300:apps/lazuar-ops/src/App.tsx
        <Route path="/workspace/ledger" element={<UtilityLedgerPage />} />
```

Sidebar workspace links omit it:

```268:276:apps/lazuar-ops/src/components/Sidebar.tsx
                ] : [
                  { label: "General Settings", href: "/workspace/general" },
                  { label: "Team", href: "/workspace/team" },
                  { label: "Audit log", href: "/workspace/audit" },
                  { label: "Legal & Billing", href: "/workspace/billing-profile" },
                  { label: "Payment Gateways", href: "/workspace/payment-gateways" },
                  { label: "Plan & billing", href: "/workspace/billing" },
                  { label: "Email Provider", href: "/workspace/email" },
                ]
```

`BillingSettingsPage.tsx` (140–178) renders balance + package buttons + “Purchase Credits” and has no `Link`/`href` to `/workspace/ledger`. The page itself is implemented (`UtilityLedgerPage.tsx:9–87`).

### Related files
- `apps/lazuar-ops/src/App.tsx` — route mount.
- `apps/lazuar-ops/src/components/Sidebar.tsx` — missing nav item.
- `apps/lazuar-ops/src/modules/workspace/pages/BillingSettingsPage.tsx` — top-up with no history link.
- `apps/lazuar-ops/src/modules/workspace/pages/UtilityLedgerPage.tsx` — the hidden screen.

### Tests
- Existing: none in ops. No API test is required for a missing `<a>`.
- Would any test fail if the bug is still there? No.
- First regression: Sidebar workspace module includes a “Utility Ledger” (or “Credit history”) href `/workspace/ledger`; Plan & billing includes a link to the same path.

### Reproduction today
Sign in. Open Plan & billing. Buy or view credits. Assert: no control navigates to history. Open the sidebar Workspace section. Assert: no Ledger item. Manually visit `/workspace/ledger`. Assert: the table renders (empty or `recent_transactions`).

### Blast radius
Merchants who buy LHDN credits cannot audit deducts without a secret URL. Money-adjacent (credits), not buyer PII. Frequency: every credit-using tenant. OrgAdmin-only API; Viewer/Member will 403 if they guess the URL (page has no `isError` chrome).

### Suggested fix
Add `{ label: "Utility Ledger", href: "/workspace/ledger" }` to the Workspace sidebar list, and a “View credit history” link on `BillingSettingsPage` under the balance. Do not build a second ledger. Do not touch Stripe Billing.

### Evaluation notes
Still P2. Same as 008 “Utility Ledger secret route.” Not blocked. Mobile-nav outage (U05) would still hide the new item on phones until that ticket lands — still add the link.
