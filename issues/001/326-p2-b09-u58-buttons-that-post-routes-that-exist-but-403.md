---
number: "326"
id: B09-U58
severity: P2
status: open
source: plans/009-bugs/09-frontends-ops-portal-admin.md
head: "297ba98"
---

# 326 — B09-U58 — Buttons that POST routes that exist but 403

- **Severity:** P2
- **Status:** open
- **Source:** `plans/009-bugs/09-frontends-ops-portal-admin.md`
- **HEAD:** `297ba98` (`feat/007-waves-1-4-implement`)

Extracted from the 17 August 2026 bug audit. Resolve this issue on its own. Do not edit other issue files while fixing this one.

## Audit write-up

#### B09-U58 — Buttons that POST routes that exist but 403 (P2 inventory)

Not missing routes. Catalog of painted writes that are not Viewer-legal: refund, cancel, keep, record-payment, anonymize, invite, remove, save vault, save email, save legal, Check TIN, create quote, mark paid, create coupon, deploy dunning, create template (WhatsApp required), create API key, create webhook, rotate secret, redeliver, SaaS pay, credit top-up, create product. Failure = toast. This is U14’s inventory.

No live button in the three apps POSTs a path that 404s at the API, except the unrouted chat island (`/ops/execute-action`, `/ops/chat/conversations/...`) which is not mounted.

---

## Evaluation (current tree, 2026-08-18)

### What the bug is
This is U14’s inventory, not a missing-route bug. Ops paints write buttons that POST real `/admin/*` and `/one/*` paths; the API then 403s anyone who is not `MEMBER`/`ADMIN` (or, for vault/keys/members/email/billing, not `ADMIN`). Failure is a Sonner toast. After 024/046 the money-adjacent subscriber writes (change-plan, seats, collection pause/resume, CSV export) are `OrgMember` and hidden from Viewer. After 143/154 the chrome knows `role`, Invite/Save vault/Anonymize are gated, and a role chip is painted. The leftover is every other mutation that is still always-on: refund, cancel, keep, record-payment, pause/resume dunning, remove member, save email, save legal, Check TIN, create quote, mark paid, create coupon, deploy dunning, create template, create API key, create webhook, rotate secret, redeliver, SaaS pay, credit top-up, create product, General Settings save. Chat (`/ops/execute-action`) is still `[MVP-HIDE]` and is not a live 404.

### Still present?
**PARTIAL**

Role is now on the ops outlet and the rail:

```183:204:apps/lazuar-ops/src/App.tsx
  const workspaceRole = workspaceRoleOf(entitlements || [], activeWorkspaceId);
  // ...
        role={workspaceRole}
  // ...
          role: workspaceRole,
```

```87:90:apps/lazuar-ops/src/modules/core/components/PageLayout.tsx
              {role && (
                <span className="hidden sm:inline text-[9px] font-bold uppercase tracking-widest text-[#71717a] border border-[#e5e5e5] px-1.5 py-0.5">
                  {role.replace("_", " ")}
```

Closed slices (do not re-fix): 143 (`fix/143-ops-role-chrome`), 154 (`fix/154-role-gated-buttons`), 024 (`fix/024-viewer-cannot-change-plan`), 046 (`fix/046-orgread-subscriber-writes`). Viewer no longer sees Export/Add Member/plan/seats/collection/anonymize; Invite and Save Credentials are Admin-only. Subscriber write maps are `OrgMember`; anonymize is `OrgAdmin` (`SubscriberEndpoints.cs` 48–280).

Still painted for Viewer (click → 403/401 toast):

- Refund on the subscriber ledger (`SubscribersPage.tsx` 789–792) and `TransactionDetailPanel` — API `OrgMember` (`CommerceEndpointsAuthorizationTests.MapCommerceEndpoints_Refund_Requires_OrgMember`).
- Log Payment / Cancel Sub / Cancel at period end / Keep plan (`SubscribersPage.tsx` 699–716) — `OrgMember` (lines 99–156 of `SubscriberEndpoints.cs`).
- Pause/resume recovery (`SubscribersPage.tsx` 588–608) — `OrgMember` (246–263).
- Remove member trash (`TeamPage.tsx` 122–131) — `DELETE .../members/{userId}` is `OrgAdmin` (`WorkspaceEndpoints.cs` 125–132). Invite form is gated (`canInvite`, 68).
- Create Quote (`QuotesPage.tsx` 46–51) and Mark paid (`QuoteDetailPanel.tsx` 51–56) — `OrgMember` (`Endpoints.cs` 35–65).
- Create Link (`ProductsPage.tsx` 100), Create Coupon (`CouponsPage.tsx` 51), Deploy/Create campaign (`DunningCampaignsPage.tsx` 44, 69), Create Template (`TemplatesPage.tsx` 134) — product/coupon/dunning writes are `OrgMember`; templates sit on the Communications `OrgAdmin` group (`Endpoints.cs` 18).
- Create API key (`ApiKeysPage.tsx` 175), Create/rotate webhook (`DeveloperSettingsPage.tsx` 109–425), Redeliver (`DeliveryLogsPage.tsx` 64–86) — keys are `OrgAdmin`; webhook writes require `manageRequired: true` (`WebhookEndpoints.cs` 47–55, 211–220).
- Save email (`EmailSettingsPage.tsx` 151) — `PUT /admin/communications/email-config` is `OrgAdmin`.
- Save stationery / Save MyInvois / Check TIN (`BillingProfilePage.tsx` 432, 506, 653) — billing admin group is `OrgAdmin` (`Billing/Infrastructure/Endpoints.cs` 10); TIN is `IntegrationLhdnDocumentsRead`, which is ADMIN/SUPER_ADMIN (or scoped API key), not Viewer (`AuthAndCorsExtensions.cs` 107–117, `DocumentEndpoints.cs` 70–99).
- SaaS pay + credit top-up (`BillingSettingsPage.tsx` 61–77, 128–131) — same `OrgAdmin` billing group.
- General Settings Save (`GeneralSettingsPage.tsx` 197) — handler still requires role `ADMIN` (issue 120/143 residue for Superadmin; Viewer/Member 403/500-shaped).

Chat is still commented (`App.tsx` 306–308). Invoicing/BillingProfile **are** mounted (`App.tsx` 296–304) — the stale “unrouted” sentence lives in issue 330, not here.

### Related files
- `apps/lazuar-ops/src/App.tsx` — outlet now carries `role`; chat still hidden.
- `apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx` — `canWrite`/`canAnonymize` cover only part of the panel.
- `apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx` — Invite gated; remove not.
- `apps/lazuar-ops/src/modules/workspace/pages/{PaymentSettingsPage,EmailSettingsPage,BillingProfilePage,BillingSettingsPage,GeneralSettingsPage,ApiKeysPage,DeveloperSettingsPage,DeliveryLogsPage}.tsx` — remaining writes.
- `apps/lazuar-ops/src/modules/commerce/pages/{ProductsPage,CouponsPage,DunningCampaignsPage,TemplatesPage}.tsx` and `apps/lazuar-ops/src/modules/invoicing/{pages/QuotesPage.tsx,components/QuoteDetailPanel.tsx}` — create/deploy/mark-paid always on.
- `apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` and `Endpoints.cs` — current policies for the POSTs the buttons hit.
- `apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` — `OrgRead` includes VIEWER; `OrgMember`/`OrgAdmin` do not.
- `apps/lazuar-api/tests/Lazuar.ModuleTests/Commerce/CommerceEndpointsAuthorizationTests.cs` — pins API policies, not the painted buttons.
- Issues 143, 154, 024, 046 (resolved), 325 (no ops tests), 317 (create-workspace switcher), 321 (chat hide).

### Tests
- Existing tests that touch this path: `CommerceEndpointsAuthorizationTests.MapCommerceEndpoints_SubscriberWrites_Require_OrgMember`, `MapCommerceEndpoints_Refund_Requires_OrgMember`, `MapCommerceEndpoints_ProductPost_Requires_OrgMember`. Those prove the API 403s Viewer; they do **not** fail if the button is painted.
- No test in `apps/lazuar-ops` or `apps/lazuar-admin` (issue 325). Portal has `i18n.test.mjs` and `grossBreakdown.test.mjs` only.
- Whether any test would fail if the bug is still there: **no** for the leftover inventory. The authorization tests would fail only if someone stripped `OrgMember` off the maps (the 024/046 hole).
- First regression test: render `SubscribersPage` / `QuotesPage` / `TeamPage` with `role: "VIEWER"` and assert Refund, Cancel, Log Payment, Create Quote, and the remove icon are absent; render with `role: "MEMBER"` and assert those commerce writes are present but Anonymize / Invite / Save Credentials / Check TIN / Create Key are not.

### Reproduction today
Arrange: workspace with one ADMIN, one VIEWER cookie, one ACTIVE subscriber with a refundable `CommerceTransactionLog`, one OPEN custom quote, CHIP configured. Act: sign in as Viewer; open Subscribers → Refund and Cancel Sub; open Quotes → Create Quote; open Team → trash icon; open Legal & Billing → Check TIN / Save; open Plan & billing → Pay / top-up. Assert: each POST returns 403 (TIN/billing/members) or 401 on webhook manage, UI shows a toast, no row changes. Repeat as MEMBER: commerce writes 200, vault/TIN/keys/members still 403.

### Blast radius
Viewers and Members who treat the console as the access-control SSoT. Not a silent money hole anymore — the four OrgRead POSTs are closed — but refund/cancel/mark-paid are one mis-click + a confusing toast away from a support ticket. PII export is gated. Frequency: every Viewer session on those pages. Ops/admin have the same “button exists, policy disagrees” shape; portal is not in this inventory.

### Suggested fix
One `canWrite` / `canAdmin` helper from `OpsOutletContext.role` (already on the outlet). Hide remaining Viewer-illegal buttons; hide Admin-only (vault, email, legal, TIN, keys, webhooks, SaaS/credits, invite/remove, anonymize) from Member. Leave API policies as they are. Do not remount `/ops/chat`. Do not emit `subscription.updated`. Do not TypeSpec-regen. Do not open Wave 5 / WhatsApp / Xero / e-mandate.

### Evaluation notes
This **is** U14’s inventory (143 resolved the chrome, not the leftover buttons). Severity stays P2: leftover is toast-on-403, not Viewer-200. 317 (create workspace in the switcher) is a sibling painted write. 325 blocks a real UI regression. Chat 404 is 321, still unmounted. Do not mark resolved until the inventory buttons are hidden or the pages are Viewer-read-only.


