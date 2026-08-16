# 07 — Ops, portal, admin frontends (after Waves 1–4)

**Date:** 16 August 2026  
**Branch evaluated:** workspace `feat/007-waves-1-4-implement` (see [008 README](./README.md))  
**Apps:** `apps/lazuar-ops`, `apps/lazuar-portal`, `apps/lazuar-admin`  
**This file does not implement anything.** It is an uncondensed read of the UI as it is now, with `file:line` evidence. Historical `plans/007-feats` cells that still say “`[MVP-HIDE]` on quotes / TIN / `/pay/{id}` / tax-invoice download” are **stale**. Re-checked in this tree.

Honesty rule used here: a surface is **live** only if a human can reach a mounted route and click a control. Backend that exists behind a 403, a swallowed empty table, or an unrouted file is not “shipped UI.” Role chrome means the UI *knows* ADMIN / MEMBER / VIEWER and changes what it offers. After Waves 1–4 the APIs know roles. The consoles mostly do not.

---

## 1. How the three apps sit

| App | Stack | Who | Auth cookie | Entry |
|-----|--------|-----|-------------|-------|
| `lazuar-ops` | Vite + React Router | Merchant console | `lazuar_auth` via `GET /one/auth/me` | `apps/lazuar-ops/src/App.tsx` |
| `lazuar-portal` | Next.js App Router | Buyer checkout + portal | optional cookie *or* `?token=` magic link | `apps/lazuar-portal/src/app/` |
| `lazuar-admin` | Vite + React Router | Platform superadmin | `lazuar_admin_auth` via `GET /platform/auth/me` | `apps/lazuar-admin/src/App.tsx` |

Cookie split is host-owned, not UI-owned:

```54:61:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs
                    // Dual cookie realm: platform admin vs product console.
                    var isPlatformRoute = context.Request.Path.StartsWithSegments("/api/v1/platform");
                    var cookieName = isPlatformRoute ? "lazuar_admin_auth" : "lazuar_auth";

                    if (context.Request.Cookies.TryGetValue(cookieName, out var token))
                    {
                        context.Token = token;
                    }
```

Ops talks to `/one/*` and `/admin/*` with `credentials: "include"` (`apps/lazuar-ops/src/lib/api-client.ts`). Admin talks to `/platform/*` (`apps/lazuar-admin/src/lib/api-client.ts`). Portal uses a server `openapi-fetch` client plus a few raw `fetch` calls to `NEXT_PUBLIC_API_URL`.

Neither ops nor admin README is a product surface. Both files are the two-word stub `# Ops` (`apps/lazuar-ops/README.md:2`, `apps/lazuar-admin/README.md:2`).

---

## 2. `lazuar-ops` — every live route

`App.tsx` is the source of truth. Public (no `OpsLayout`):

| Path | Component | Notes |
|------|-----------|--------|
| `/` | `HomeRedirect` | Cookie check → `/commerce/dashboard` or `/pricing` (`176:203:apps/lazuar-ops/src/App.tsx`) |
| `/pricing` | `PricingPage` | Public Hub pricing; no session |
| `/signup` | `LoginPage` | Forced signup mode |
| `/login` | `LoginPage` | `/one/auth/login` |

Authenticated (`OpsLayout` + session + ≥1 entitlement):

| Path | Page file | Sidebar label |
|------|-----------|----------------|
| `/commerce/dashboard` | `modules/commerce/pages/DashboardPage.tsx` | Dashboard |
| `/commerce/products` | `ProductsPage.tsx` | Checkout Links |
| `/commerce/subscribers` | `SubscribersPage.tsx` | Subscribers |
| `/commerce/transactions` | `TransactionsPage.tsx` | Transaction Logs |
| `/commerce/disputes` | `DisputesPage.tsx` | Disputes |
| `/commerce/coupons` | `CouponsPage.tsx` | Promotions |
| `/commerce/dunning-campaigns` | `DunningCampaignsPage.tsx` | Dunning Campaigns |
| `/commerce/dunning-campaigns/new` | `CampaignBuilderPage.tsx` | (same module, no extra nav item) |
| `/commerce/dunning-campaigns/:id` | `CampaignBuilderPage.tsx` | (same) |
| `/commerce/templates` | `TemplatesPage.tsx` | Notification Templates |
| `/developer/api-keys` | `ApiKeysPage.tsx` | API Keys |
| `/developer/webhooks` | `DeveloperSettingsPage.tsx` | Outbound Webhooks |
| `/developer/logs` | `DeliveryLogsPage.tsx` | Delivery Logs |
| `/workspace/general` | `GeneralSettingsPage.tsx` | General Settings |
| `/workspace/team` | `TeamPage.tsx` | Team |
| `/workspace/audit` | `AuditLogPage.tsx` | Audit log |
| `/workspace/billing-profile` | `BillingProfilePage.tsx` | Legal & Billing |
| `/workspace/payment-gateways` | `PaymentSettingsPage.tsx` | Payment Gateways |
| `/workspace/email` | `EmailSettingsPage.tsx` | Email Provider |
| `/workspace/billing` | `BillingSettingsPage.tsx` | Plan & billing |
| `/workspace/ledger` | `UtilityLedgerPage.tsx` | **not in sidebar** |
| `/invoicing/quotes` | `QuotesPage.tsx` | Quotes |
| `/invoicing/tax-invoices` | `TaxInvoicesPage.tsx` | Sales documents |
| `/invoicing/credit-notes` | `CreditNotesPage.tsx` | Credit Notes |

Catch-all:

```247:248:apps/lazuar-ops/src/App.tsx
      <Route path="*" element={<Navigate to="/commerce/dashboard" replace />} />
    </Routes>
```

There is **no merchant 404 page**. Unknown paths silently become the dashboard. A Viewer who types a typo never sees “not found”; they just land on Sales Insights. The only remaining commented route is ops chat (`242:244:apps/lazuar-ops/src/App.tsx`).

`OpsLayout` (`41:164:apps/lazuar-ops/src/App.tsx`):

1. `GET /one/auth/me`. Failure → `/login?returnUrl=…` (or `/login` on throw).
2. `GET /one/me/entitlements`. Empty array → `EmptyWorkspaceState` (create workspace, no Superadmin).
3. Active workspace is `localStorage.ops_active_workspace_id`, repaired to the first entitlement if stale.
4. Workspace switch navigates to `/commerce/dashboard` (`110:114`).
5. Logout `POST /one/auth/logout` and clears the workspace key.

The layout **does not read `entitlement.role`**. It does not hide modules. It does not attach a role to `OpsOutletContext`:

```35:39:apps/lazuar-ops/src/App.tsx
export interface OpsOutletContext {
  activeWorkspaceId: string | null;
  entitlements: EntitlementDto[];
  onWorkspaceSelect: (id: string) => void;
}
```

`EntitlementDto` **does** include `role` (API: `WorkspaceEndpoints.cs` `151:157` and `162:163`). The console fetches it and then ignores it.

Zero entitlements is a real first-run path, not a 403:

```13:18:apps/lazuar-ops/src/components/EmptyWorkspaceState.tsx
      <div className="max-w-sm text-center space-y-2">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create your workspace</h1>
        <p className="text-[13px] text-[#71717a] leading-relaxed">
          You are signed in but have no workspace yet. Pick a name and slug — no Superadmin approval.
        </p>
      </div>
```

---

## 3. Sidebar (what a merchant is invited to click)

`MODULES` is four accordions (`26:31:apps/lazuar-ops/src/components/Sidebar.tsx`): Commerce, Invoicing, Developer, Workspace.

Hard-coded links (`249:276:apps/lazuar-ops/src/components/Sidebar.tsx`):

**Commerce:** Dashboard, Checkout Links, Subscribers, Transaction Logs, Disputes, Promotions, Dunning Campaigns, Notification Templates.

**Invoicing:** Quotes, Sales documents, Credit Notes.

**Developer:** API Keys, Outbound Webhooks, Delivery Logs.

**Workspace:** General Settings, Team, Audit log, Legal & Billing, Payment Gateways, Plan & billing, Email Provider.

Not linked, still mounted: `/workspace/ledger` (`UtilityLedgerPage`). A merchant can only reach credit history by typing the URL or bookmarking it. Plan & billing (`/workspace/billing`) shows the *balance* and a top-up form; the ledger page shows `recent_transactions` from the same `GET /admin/billing/credits`.

Not linked, still in the tree as files: `OpsChatWorkspace`, `ConversationsDirectory`, `components/forms/CreateProductForm` (chat registry), two unused `PaymentSettingsModal` copies (`apps/lazuar-ops/src/components/PaymentSettingsModal.tsx` and `modules/workspace/components/PaymentSettingsModal.tsx` — grep finds no importer).

Brand string in the rail is **“Lazuar Console”** (`212:213:apps/lazuar-ops/src/components/Sidebar.tsx`). User footer shows `user.name` + `user.email` and a Log out flyout. **No role chip.** An Admin, a Member, and a Viewer get the identical rail.

Sidebar chrome: 240px expanded / 48px collapsed; collapse persisted as `lazuar-ops-sidebar-collapsed` (note the invert: `localStorage.setItem(..., String(prev))` stores the *pre-toggle* value — `103:107:apps/lazuar-ops/src/App.tsx`). Section open-state is `lazuar-ops-sidebar-sections`. On `window.innerWidth < 768` the rail is `absolute`, translated `x: -240` when closed, and `isSidebarOpen` is forced false (`51:56`, `206:206`).

`PageLayout` (`apps/lazuar-ops/src/modules/core/components/PageLayout.tsx`) has breadcrumbs + workspace switcher. **It has no hamburger.** Combined with “close sidebar on every mobile resize,” a phone-width merchant cannot reopen navigation after the first paint. See §16.

---

## 4. Dashboard / MRR

`DashboardPage` is the only analytics surface. It fans five queries and **blocks the whole page** until all five settle (`65:71:apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx`):

| Query | Endpoint | Auth policy | Viewer/Member |
|-------|----------|-------------|----------------|
| `commerce-stats` | `GET /admin/commerce/stats` | group default `OrgRead` | 200 |
| `financial-summary` | `GET /admin/billing/summary` | entire `/admin/billing` is `OrgAdmin` (`10:10:apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs`) | **403** |
| `commerce-products` | `GET /admin/commerce/products` | `OrgRead` | 200 |
| `payment-config-status` | `GET /admin/commerce/payment-config` | `OrgAdmin` (`19:19:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/PaymentConfigEndpoints.cs`) | **403** (thrown; 404 is the only swallowed status) |
| `email-config-status` | `GET /admin/communications/email-config` | entire comms admin group `OrgAdmin` (`18:18:apps/lazuar-api/Modules/Communications/Infrastructure/Endpoints.cs`) | **403** |

`queryFn` throws on any non-404 error (`21:22`, `30:31`). React Query then sets `isLoading=false`, `isError=true`, `data=undefined`. The page never reads `isError`. After a 403 it **paints anyway** with zeros and a permanently incomplete checklist.

KPI strip (`75:83`, `171:188`):

1. **Net Cash in Bank** — `financials.net_revenue`. OrgAdmin-only. Viewer/Member see `RM 0.00`.
2. **MRR** — `stats.mrr`. Tooltip: “Committed monthly equivalent of active memberships. Not cash. Past-due is excluded.”
3. **ARR** — `stats.arr ?? mrr * 12`. Same tooltip copy pasted onto ARR (the `title` string is identical for both, `77:78`).
4. **Active Subscribers** — `stats.active_subscribers`.
5. **Past Due** — `stats.past_due_subscribers`, amber chrome if > 0.
6. **Cancellation Rate** — `stats.churn_rate_percentage` + `%`.
7. **Recovered (lifetime)** — `stats.recovered_revenue`.

Seven cards sit in `grid-cols-2 lg:grid-cols-3 xl:grid-cols-5` (`171`). On a 1440px desktop the last two wrap. Footnote (`190:196`) repeats the MRR/ARR definition and links to Dunning Campaigns. Recovered is “campaign-lifetime cash collected while PAST_DUE or SUSPENDED, not this month.”

Revenue Trend is a Recharts `BarChart` on `stats.cash_flow_trend` (`207:217`). Empty / all-zero → “No confirmed payments yet.” Up to three `payment_methods` rows under the chart.

Product Catalog is a read-only table of `GET /admin/commerce/products` (name, `RM {price}`, interval, Active/Archived). Rows are **not** clickable. The merchant must go to Checkout Links to edit.

Getting-started checklist (`10`, `85:102`, `111:169`) shows until `gatewayReady && emailReady && productReady && linkReady`, or until a 30-day dismiss in `localStorage`. For Viewer/Member, `gatewayReady` and `emailReady` are always false because those GETs 403. The checklist never completes. Copy still says “Email (Resend) — required for paid checkout.” Pay-link copy uses `VITE_PORTAL_URL` or `http://localhost:3004` plus `/{slug}/checkout/{product.slug}`.

No date picker. No comparison period. No per-product MRR. No cohort. This is a single-tenant snapshot, not Stripe-style analytics.

---

## 5. Subscribers

`SubscribersPage` is the densest merchant screen (~860 lines). List: `GET /admin/commerce/subscribers?page&limit=50&search` (`53:63`). **Status filter is client-side** on the current page (`299`). Selecting PAST_DUE does not ask the API for past-due; it hides other rows in the already-fetched 50. A workspace with 51 ACTIVE and 1 PAST_DUE on page 2 will show “No subscribers found” under PAST_DUE on page 1.

Columns: Customer (name, Reminder-only / Zap auto-debit, email + copy), Product + `RM` price, Status chips (TRIALING extra “Trial”, cancel-at-period-end “Cancels”, collection paused, pending plan, pending seats), Paid through / Next due.

Header actions always rendered (`306:322`):

- **Export CSV** → `GET /admin/commerce/subscribers/export` (`OrgRead`). Viewer can download PII.
- **Add Member** → `CreateSubscriberModal` → `POST /admin/commerce/subscribers` (`OrgMember`). Viewer gets a toast.

Side panel “Member Console” is the lifecycle cockpit. Mutations:

| UI | Call | Policy | Viewer | Member |
|----|------|--------|--------|--------|
| Schedule / revert plan | `POST .../subscribers/{id}/change-plan` | **inherits `OrgRead` only** (`157:188:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs`) | **writes** | writes |
| Set seats | `POST .../subscribers/{id}/quantity` | inherits `OrgRead` (`190:210`) | **writes** | writes |
| Pause / resume collection | `POST .../collection/pause\|resume` | inherits `OrgRead` (`212:243`) | **writes** | writes |
| Pause / resume dunning | `POST .../dunning/pause\|resume` | `OrgMember` (`253:262`) | 403 toast | writes |
| Log Payment | `POST .../record-payment` | `OrgMember` | 403 | writes |
| Cancel now / at period end | `POST .../cancel` | `OrgMember` | 403 | writes |
| Keep plan | `POST .../keep` | `OrgMember` | 403 | writes |
| Anonymize | `POST .../anonymize` | **`OrgAdmin`** (`279`) | 403 | **403** |
| Copy Portal Link | `POST .../subscribers/portal-link` | `OrgMember` | 403 | writes (Stripe Customer Portal URL) |
| Refund on a ledger row | `POST .../transactions/{id}/refund` | `OrgMember` | 403 | writes |
| Add Member | `POST /subscribers` | `OrgMember` | 403 | writes |

Plan & seats UI is shown for `ACTIVE` or `TRIALING` (`574:636`). Copy: “No charge today. Changes start on the next billing date.” Product dropdown is monthly/yearly active products other than current. Empty selection + Schedule is labeled “Revert.” Seats are `1..99`. Collection pause is ACTIVE-only.

Trial is visible: list chip (`397:401`), panel field `trial_ends_at` (`507:509`). Reminder-only vs vaulted token is first-class (`372:376`, `513`). Current renewal pay link is shown and copyable for reminder-only PAST_DUE/SUSPENDED (`641:656`).

WhatsApp deep-link on phone (`470:472`) is a `wa.me` URL, not a Communications send. It does not mean WhatsApp is billed or connected (Plan & billing copy still says WhatsApp is not connected — `BillingSettingsPage.tsx:149`).

Offline payment modal methods: BANK_TRANSFER, CASH, COMPED (`781:785`). Copy: “This grants one period from today.”

Create-subscriber modal (`CreateSubscriberModal.tsx`) enrolls a recurring product with amount, method, optional welcome email, start date, next billing date. Same `OrgMember` POST.

No pagination controls on the list despite `page` state. After 50 subscribers the merchant cannot go to page 2.

---

## 6. Refunds

Refunds are not a route. They are a modal opened from:

1. Subscriber Payment Ledger (`745:749:apps/lazuar-ops/src/modules/commerce/pages/SubscribersPage.tsx`).
2. `TransactionDetailPanel` (`RefundModal` import at `apps/lazuar-ops/src/modules/commerce/components/TransactionDetailPanel.tsx:6`).

`RefundModal` (`apps/lazuar-ops/src/modules/commerce/components/RefundModal.tsx`):

- Remaining = original − already refunded.
- Amount input, optional reason (255), optional gateway override if `gateway_name` is missing.
- API rails (`STRIPE`, `CHIP`, `RAZORPAY`, `XENDIT`, or `supports_api_refund`): “This sends money back at the processor.”
- Billplz: “Billplz has no bill-refund API. Refund the bill in the Billplz dashboard, then mark it here.” `mark_refunded: true`.
- Offline: “Mark only after you returned the money.”
- Explicit: “Cancel the subscription separately if access should stop. Refund does not cancel.” (`171:173`)

POST `/admin/commerce/transactions/{id}/refund` is `OrgMember` (`116:116:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints/TransactionEndpoints.cs`). Viewer sees the Refund button (`canRefund`) and gets a toast.

Transactions list (`TransactionsPage.tsx`) filters CONFIRMED / REFUND_PENDING / PARTIALLY_REFUNDED / REFUNDED / REFUND_FAILED, gateways including XENDIT and OFFLINE, search, CSV export of the last 31 days (`125:148`). Polls every 2s while any row is `REFUND_PENDING`. Amounts strike through when `REFUNDED`; partial shows `− RM`.

There is no “refunds inbox.” Credit notes appear separately under Invoicing after the ledger books a reversal.

---

## 7. Disputes

`DisputesPage` is a **read-only table**. No evidence upload, no accept/challenge, no “this is a duplicate,” no link into the subscriber.

```23:29:apps/lazuar-ops/src/modules/commerce/pages/DisputesPage.tsx
      const res = await fetch(`${API_URL}/admin/commerce/disputes?page=1&limit=50`, {
        credentials: "include",
        headers: { "X-Tenant-Id": activeWorkspaceId },
      });
      if (!res.ok) throw new Error("Failed to load disputes");
      return (await res.json()) as { data: CommerceDispute[] };
```

GET is `OrgRead` (`67:77:apps/lazuar-api/Modules/Commerce/Infrastructure/Endpoints.cs`). Columns: Date, Amount + currency, first 8 of `subscription_id`, Status (always amber). Empty: “No open disputes.” Description: “Open card chargebacks on Commerce payments. Access stays active until you cancel.” Page is hardcoded `page=1&limit=50`. A 403 throws (unhandled query error → blank/spinner, not a friendly “you cannot see disputes”).

A merchant can click Disputes. They cannot *do* a dispute.

---

## 8. Quotes

Wave 2 remounted invoicing. `QuotesPage` is live.

- List: `GET /admin/commerce/custom-checkouts` (`OrgRead`).
- Create: `CreateQuoteModal` → `POST /admin/commerce/custom-checkouts` (`OrgMember`).
- Detail: `QuoteDetailPanel`.

Create form (`CreateQuoteModal.tsx`): client name/email, optional expiry, terms default `due_on_receipt`, **B2B checkbox** `is_b2b_required`, line items (description, qty, unit price), reject total ≤ 0. No TIN collected at quote time. The B2B flag is a promise that checkout will collect TIN.

Detail (`QuoteDetailPanel.tsx`):

- Pay URL = `{VITE_PORTAL_URL}/{workspace_slug}/pay/{sessionId}` (`35:39`). After Waves 1–4 this URL is a **real portal route**, not `notFound()`.
- Copy Quote Link, open in new tab.
- “After payment” = “B2B tax invoice” vs “B2C official receipt” (`171:172`).
- **Mark as Paid (Bank Transfer)** → `POST /admin/commerce/checkouts/{id}/mark-paid` (`OrgMember`).
- Completed → jump to `/invoicing/tax-invoices?search={request.id}`.

Viewer can list and open the pay URL. Create / Mark paid 403. There is no “email this quote” button; the merchant copies a link.

---

## 9. Tax invoices, credit notes, legal profile

### Sales documents (`TaxInvoicesPage`)

`GET /admin/billing/ledger?type_filter=sales` — **entire billing group is `OrgAdmin`**. Viewer and Member clicking “Sales documents” fire a throwing query. React Query error is not rendered; they get a spinner then an empty “No tax invoices found” (or a stuck spinner depending on error phase). Same for the extra “last B2C consolidation” query (`search: "B2C-CONS-"`).

For an Admin the page is real: date, document number, B2C/B2B type, net, tax, LHDN badge (`VALID` / `SUBMITTED` / `PENDING` pulse / `B2C_RECEIPT` / `CONSOLIDATED_PENDING` / `REJECTED` / `CANCELLED`). Search is synced to `?search=`.

`TaxInvoiceDetailPanel`:

- Live LHDN poll `GET /lhdn/documents/{internalId}` every 5s while PENDING/SUBMITTED (`33:47`). That GET is `IntegrationLhdnDocumentsRead` = **ADMIN / SUPER_ADMIN / scoped API key only** (`70:70:apps/lazuar-api/Modules/Lhdn/Infrastructure/Endpoints/DocumentEndpoints.cs`, policy `107:117:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`). Member is not in that policy.
- Download PDF: `GET /admin/billing/ledger/{id}/document` (`OrgAdmin`) → `window.open(data.url)`.
- Cancel e-Invoice: `POST /lhdn/documents/{internalId}/cancel` (`IntegrationLhdnDocumentsWrite` = ADMIN only) if VALID and < 72h since `validated_at`. After 72h: “Cancel window closed — issue a credit note” (disabled). Copy: “Supplier cancel only… Buyer reject is not implemented.”
- MyInvois QR via `api.qrserver.com` (third-party, not self-hosted).

### Credit notes (`CreditNotesPage`)

Same ledger with `type_filter: reversals`. Banner: credit notes are automatic on refund / e-invoice cancel; “Manual creation of Credit Notes is restricted.” Trigger column is `GATEWAY_REFUND` → “Refund” else “Cancellation.” Amounts are red negatives. Same OrgAdmin wall. Same detail panel reused.

### Legal & Billing (`BillingProfilePage`)

Two cards.

**Stationery** — `GET/PUT /admin/billing/profile` (`OrgAdmin`): legal name, TIN, SSM, SST number, logo via `POST /one/storage/presigned-url` then PUT to the signed URL, MY state codes 01–17, country default `MYS`. Copy says checkout branding lives on General Settings (`292`).

**MyInvois** — `GET/PUT /lhdn/workspaces/{id}/lhdn-config` and optional `PUT .../lhdn-certificate` (`OrgAdmin` tenant-config group). Same-as-stationery checkbox. ID type BRN/NRIC/PASSPORT/ARMY. **Check TIN** → `POST /lhdn/taxpayer/validate`. Sandbox/Prod. MSIC. Intermediary mode. Client id + secret (never echoed). `.p12` + passphrase. Honesty line: unsigned v1.0 unless cert stored and `Lhdn:Signing=Auto` (`619:622`).

Viewer/Member opening this page: profile GET 403 (thrown unless 404), LHDN config 403. Forms still render empty. Save → 403 toast. Logo upload uses `/one/storage/presigned-url` (`RequireAuthorization()` only) — a Viewer might get an upload URL even if they cannot save the profile.

---

## 10. Team

`TeamPage` (`apps/lazuar-ops/src/modules/workspace/pages/TeamPage.tsx`):

- List: `GET /one/workspaces/{id}/members` — any authenticated member of the workspace (`91:91:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs`).
- Invite: `POST /one/workspaces/{id}/invites` — **`OrgAdmin`** (`97`).
- Remove: `DELETE /one/workspaces/{id}/members/{userId}` — **`OrgAdmin`** (`119`).

UI always shows the invite form (email + Admin/Member/Viewer) and a trash icon on every row including yourself. Description is honest: “Invite staff as Admin, Member, or Viewer. Members operate commerce; Viewers can only read.” (`62`). There is no “you are a Viewer, you cannot invite” chrome. Click Invite as Member/Viewer → toast of the API `detail`. There is no change-role control. There is no pending-invites list even though `GET .../invites` exists.

---

## 11. Audit

`AuditLogPage`: `GET /one/workspaces/{id}/audit?page&limit=50`. Backend is `RequireAuthorization()` + tenant access (`167:202:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs`). **Viewer can read the audit log.**

Frontend special-case:

```29:31:apps/lazuar-ops/src/modules/workspace/pages/AuditLogPage.tsx
      if (res.status === 403) return { data: [] as AuditEvent[], total_count: 0, total_pages: 1 };
      if (!res.ok) throw new Error("Failed to load audit log");
```

A real 403 is rendered as **“No audit events yet.”** That is the only page that swallows 403. Description: “Who changed money or identity in this workspace. Reads are not logged.” Columns: When, Actor, Action, Entity (type + first 8 of id). `metadata_json` is fetched and **never shown**. Prev/Next if `total_pages > 1`.

---

## 12. Payment settings — Xendit form is missing

`PaymentSettingsPage` is the BYOK vault. `GatewayType = "STRIPE" | "BILLPLZ" | "RAZORPAY" | "CHIP" | "XENDIT"` (`7`). The `<select>` includes Xendit (`211`):

```211:211:apps/lazuar-ops/src/modules/workspace/pages/PaymentSettingsPage.tsx
                      <option value="XENDIT">Xendit (SEA hosted invoice + wallets)</option>
```

Conditional field blocks exist for CHIP, BILLPLZ, STRIPE, RAZORPAY (`247:391`). **There is no `{gatewayType === "XENDIT" && (...)}` block.** Choosing Xendit shows Target Provider + environment + empty “Secure Credentials” + Save.

Client-side first-time validation also skips Xendit (`84:128`). Save still `PUT /admin/commerce/payment-config` with `gateway_type: "XENDIT"` and whatever happens to be in `apiKey` / `secretKey` / `webhookSecret` / `collectionId` (usually empty). `OrgAdmin` only. Viewer/Member: load 403 toast “Failed to load payment configuration,” form still interactive, Save 403.

Honesty that *is* present:

- Billplz: 128-char X-Signature, Collection ID required, amber “cannot vault / no silent auto-charge.”
- Stripe: secret + `whsec_`, Apple/Google Pay copy, test vs live.
- CHIP: Brand ID + secret, claims autonomous RSA + webhook setup.
- Environment select: “Hub hostname does not pick Billplz sandbox vs live.”

`has_api_key` / hints are never written back from the server after save (local optimistic `has_*` flags). Reloading is the only way to see stored hints.

Identical Xendit hole exists on the **platform** vault. See §15.

---

## 13. Role chrome (almost none)

Backend policy catalog (`76:94:apps/lazuar-api/src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs`):

- `OrgAdmin` = SUPER_ADMIN, ADMIN — keys, certs, payment/email config, member admin, **all of `/admin/billing`**, **all of `/admin/communications`**.
- `OrgMember` = those + MEMBER — operate commerce writes.
- `OrgRead` = those + VIEWER — GETs on `/admin/commerce`.

The ops UI never branches on `e.role`. Consequences:

1. Sidebar is identical for all three roles.
2. PageLayout workspace switcher shows name only (`75:76:apps/lazuar-ops/src/modules/core/components/PageLayout.tsx`).
3. User footer does not say “Viewer.”
4. Every destructive button (Refund, Anonymize, Invite, Save Credentials, Cancel e-Invoice, Create Quote) is enabled. Failure is a Sonner toast — if the query layer even surfaces `error.detail`.
5. Dashboard checklist lies to non-admins (gateway/email always “not done”).
6. Change-plan / quantity / collection pause are **Viewer-writable** because those four POSTs forgot `.RequireAuthorization("OrgMember")`. That is not just a UX hole; it is an authorization hole the UI happily exposes.
7. Anonymize is Admin-only; Member sees the same rose button.
8. Superadmin entitlements inject `Role = "SUPER_ADMIN"` without a `TenantMembership` (`145:159:apps/lazuar-api/Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs`). `UpdateWorkspaceCommand` then requires `membership.Role == "ADMIN"` **exactly** (`32:35:apps/lazuar-api/Modules/One/Application/Commands/UpdateWorkspaceCommand.cs`). A platform superadmin clicking Save on General Settings can get `InvalidOperationException("Unauthorized to update workspace.")` — typically a 500-shaped problem, not a 403. Member/Viewer get the same exception (not `OrgAdmin` policy; the route is `RequireAuthorization()` only, `50:64:WorkspaceEndpoints.cs`).

There is no “request access” empty state. There is no disabled-with-tooltip pattern.

---

## 14. Other live ops surfaces (needed to judge “what a merchant can click”)

**Checkout Links (`ProductsPage`)** — list + `CreateProductModal` / `ProductDetailPanel` / `ProductForm`. Warnings if no active gateway with a key, or no Resend. Those GETs are OrgAdmin, so Viewer/Member **always** see both red/amber banners. Create / update / archive are `OrgMember`.

`ProductForm` (the real one, not the chat leftover): interval one_time / mo / yr, optional yearly price on monthly, **trial days 0–90** (`150:154`), gateway select from configured vault, Active gated on email config, address / **TIN** / phone checkboxes, SST 06 vs 02 if the billing profile has an SST number, legacy fulfillment textarea. Help under TIN: “Collects buyer company + TIN. We do not validate the TIN at checkout.” (`221:222`). That sentence is **false after Wave 2** — portal `CheckoutForm` calls `validateTin` before submit (`96:110:apps/lazuar-portal/src/modules/checkout/components/CheckoutForm.tsx`).

Chat-era `modules/commerce/components/CreateProductForm.tsx` still has the TIN checkbox and no trial field. It is not on a live route.

**Promotions** — coupon table + create modal + detail. Writes `OrgMember`.

**Dunning** — list, deploy recommended strategy, builder at `/new` and `/:id`. Writes `OrgMember`. Empty state is a marketing block (“Revenue Recovery Engine”).

**Notification Templates** — `GET/POST/PUT /admin/communications/templates` is **OrgAdmin**. Member (who is supposed to “operate commerce”) cannot edit dunning email copy. Viewer neither. WhatsApp body fields still exist; Plan & billing says WhatsApp is not connected.

**API Keys** — closed scope catalog (LHDN, Payments checkouts, Commerce subscriptions, Webhooks). Presets for LHDN and “least-privilege Payments integrator.” Create reveals the secret once. `OrgAdmin`.

**Outbound Webhooks** — event catalog (`subscription.*`, `order.completed`, `payment_link.paid`, `payment.completed/failed`). Secret shown once. Manage requires ADMIN/SUPER_ADMIN (`299:302:WebhookEndpoints.cs`). GET list is any workspace member (`305:306`). Member can *see* endpoints, cannot create. UI does not hide the form.

**Delivery Logs** — list + expand + redeliver/resend. Redeliver is manage-required (`220`).

**General Settings** — name, checkout logo, accent hex, slug danger zone. `hasChanges` is hard-coded `|| true` (`110:113:apps/lazuar-ops/src/modules/workspace/pages/GeneralSettingsPage.tsx`) so Save is always enabled. Slug change confirm: “will break all existing public links.” Branding is **checkout** branding (`149`). Legal logo is a different upload on Billing Profile.

**Email Provider** — Resend key + sender + active. OrgAdmin. Dashboard treats this as a hard gate for paid checkout.

**Plan & billing** — Hub SaaS status + “Pay” / “Pay next period” → `POST /admin/billing/saas/checkout`. Utility credit balance + package picker + `POST /admin/billing/credits/top-up`. Copy is careful: software fee ≠ GMV take; credits only on live LHDN submit; WhatsApp not billed (`97:99`, `149`). OrgAdmin.

**Utility Ledger** — same credits GET, transaction table. Routed, not linked.

**Pricing (`/pricing`)** — public. Fallback if `GET /one/public/pricing` fails: 0% GMV, 50 starter credits, SST 0% note, `checkout_is_free: true`, `lhdn_credits_live: false`. Header “Lazuar Hub.”

**Login/signup** — `/one/auth/login` and signup with workspace name + slugify + terms checkbox pointing at `/portal/legal/terms` (Caddy path, not the portal origin). Signup creates the first ADMIN.

---

## 15. `lazuar-admin`

This app is a **single-page vault** with a lot of unused shadcn.

Routes (`86:96:apps/lazuar-admin/src/App.tsx`):

| Path | What happens |
|------|----------------|
| `/login` | `POST /platform/auth/login` |
| `/` | redirect → `/platform/gateways` |
| `/platform/gateways` | `PlatformPaymentSettingsPage` |
| `*` | redirect → `/platform/gateways` |

Auth: `GET /platform/auth/me`. Failure → `/login?returnUrl=`. Logout `POST /platform/auth/logout`. No entitlements, no workspace switcher. Sidebar brand: **“Platform Control.”** User subtitle is hard-coded **“Super Admin”** (`204:204:apps/lazuar-admin/src/components/Sidebar.tsx`), not `user.email`. One nav item: Payment Gateways.

`PlatformPaymentSettingsPage` is a near-clone of ops `PaymentSettingsPage` aimed at `GET/PUT /platform/payment-config` (Hub’s own processors for SaaS + credit top-ups). Same five-way `<select>` including Xendit (`206`). **Same missing Xendit field block.** No environment select (ops has test/live; admin does not). Description: “Configure the root payment processors for utility credit top-ups across the ecosystem.”

There is no tenant list, no impersonation, no credit grant, no feature flag, no dead-letter, no LHDN platform keys UI (those live under ops Legal & Billing / API keys). `prompt-library.ts` and `types/chat.ts` are leftover from the shared ops/admin scaffold. `use-mobile.ts` exists and is unused; `App.tsx` inlines the same `< 768` check. `PageLayout` has no workspace switcher and no hamburger — same mobile trap.

A non-superadmin with a product cookie hitting admin `/platform/auth/me` fails and is sent to login. There is no “wrong console” message.

---

## 16. `lazuar-portal`

### 16.1 Route map

| URL | File | Live? |
|-----|------|-------|
| `/` | `src/app/page.tsx` | Yes — lock icon + “use the magic links” |
| `/legal/terms`, `/privacy`, `/refund` | `src/app/legal/**` | Yes — Lazuar-as-platform terms, last updated June 2026 |
| `/{tenantSlug}/checkout/{productSlug}` | `checkout/[productSlug]/page.tsx` | Yes |
| `/{tenantSlug}/checkout/{productSlug}/success` | `success/page.tsx` | Yes |
| `/{tenantSlug}/checkout/custom/success` | exists | Yes (file present) |
| `/{tenantSlug}/pay/{sessionId}` | `pay/[sessionId]/page.tsx` | **Yes — remounted** |
| `/{tenantSlug}/portal` | `portal/page.tsx` | Yes |
| `/{tenantSlug}/update-payment/{subId}` | `update-payment/[subId]/page.tsx` | Yes |
| unknown | `not-found.tsx` | Localized 404 |

Tenant layout (`src/app/[tenantSlug]/layout.tsx`) fetches branding and sets CSS `--brand` from `primary_color`. Checkout layout wraps `CheckoutI18nProvider` + `CheckoutHeader`. Portal layout is a separate “Buyer Dashboard” header with optional Logout (cookie session only).

`CommunityPortalView` (`src/modules/community/components/CommunityPortalView.tsx`) is **not imported by any route**. Dead island. Cancel-at-period-end lives on the aggregated portal page instead.

### 16.2 Checkout EN / BM

Locales `en` | `ms` (`apps/lazuar-portal/src/modules/checkout/i18n/locales.ts`). Resolve order: `?lang=` → `?locale=` → cookie `lazuar_locale` → `Accept-Language` (only `ms` is sniffed; anything else falls through to `en`) → default `en`.

Header switcher (`CheckoutHeader`, `127:160:apps/lazuar-portal/src/modules/checkout/i18n/CheckoutI18n.tsx`): EN | BM, `aria-pressed`, sets cookie + `document.documentElement.lang` + `router.replace` with `?lang=`. Messages cover chrome, form, summary, promo, banners, errors, success, 404. **Not translated:** ID type / ID value labels (`"ID type"`, `"ID value"`, `"SSM / NRIC / passport no."` — `CheckoutForm.tsx:228:252`), monthly/yearly interval buttons (`"Yearly"` / `"Monthly"` — `CheckoutView.tsx:160`), QuoteView (English only), portal dashboard (English only), update-payment (English only), legal pages (English only).

Root layout footer uses the same dictionary (`Terms` / `Terma`, etc.) and `pb-[max(1rem,env(safe-area-inset-bottom))]`.

### 16.3 Quantity

`CheckoutView` (`41`, `82:95`, `174`):

```
quantityAdjustable = pricing_model === "FIXED" && interval ∈ {one_time, mo, yr}
```

`OrderSummaryCard` stepper `−` / number / `+`, min/max from `CHECKOUT_QUANTITY_MIN/MAX`, aria-labels translated. Coupon is dropped if quantity changes. PWYW has no quantity stepper. Submitted as `quantity` on `POST /public/commerce/checkout`. Unit × n and “Discount (per item × n)” are translated.

### 16.4 Trial

`product.trial_days` (`CheckoutView.tsx:42`). If `trialDays > 0` and interval is not `one_time`, **total due today is 0** (`117`). Summary line: `"{days}-day trial, then {amount} / {interval}. Cancel anytime during trial."` / BM equivalent (`messages.ts:52`, `154`). Ops `ProductForm` is where trial days are set (0–90). One-time products force trial 0.

### 16.5 TIN

Unhidden. If `checkout_configuration.requires_tax_id`:

- Company name, TIN, ID type (BRN/NRIC/PASSPORT/ARMY), ID value.
- Submit path **blocks** on `validateTin(tenantSlug, taxId, idType, idValue)` (`96:110:CheckoutForm.tsx`). Invalid → “This TIN / ID pair is not valid in MyInvois.”
- Payload sends `company_name`, `tax_id`, `id_type`, `id_value`.
- Hint “Matched: {taxpayer_name}” is state that is set but the success path immediately `window.location.assign`s, so the user rarely sees it.

Address block is separate (`requires_address`), 1-col on mobile / 2-col from `sm`. Country is a free-text ISO-ish box default `MY` (ops legal profile uses `MYS` — three letters). Product form help still claims TIN is not validated. It is.

B2B quotes collect TIN on `QuoteView` (company + TIN, required if `is_b2b_required`) without the MyInvois validate call (`37:40`, `163:180:QuoteView.tsx`). Different bar than hosted checkout.

### 16.6 Branding

`GET /public/one/{tenantSlug}/branding` (`branding.ts`, 60s revalidate). Fields: `name`, `slug`, `logo_url`, `primary_color`. Used as:

- Checkout header logo / name.
- `--brand` on the tenant wrapper; CTA buttons `style={{ backgroundColor: "var(--brand, var(--foreground))" }}`.
- Success/update-payment pages fetch again.
- QuoteView: B2B uses **legal profile** logo/name/TIN/SSM when `is_b2b_required`; else workspace branding.

Ops General Settings is the writer. Legal & Billing logo is stationery, not checkout, unless the quote is B2B.

### 16.7 `/pay/{id}`

```16:44:apps/lazuar-portal/src/app/[tenantSlug]/pay/[sessionId]/page.tsx
  const { data: checkout, error: checkoutError } = await serverClient.GET("/public/commerce/{tenantSlug}/custom-checkouts/{sessionId}", ...);
  if (checkoutError || !checkout) {
    notFound();
  }
  ...
  return ( <QuoteView ... /> );
```

This is a **200 + QuoteView**, not ADR-023 `notFound()`. Missing/expired session → portal 404 page (localized). `?cancelled=true` shows an English amber banner. Draft PDF via `checkout.draft_pdf_url`. Proceed → same `submitCheckout` as product checkout with `product_slug: "custom"` and `session_id`. Completed state links to `/{tenant}/portal` (no token — buyer then hits the magic-link gate unless they have a cookie).

### 16.8 Portal plan change

`PortalPlanChange` only mounts when `isHealthyActive && token` (`106:114:apps/lazuar-portal/src/app/[tenantSlug]/portal/page.tsx`). Cookie-only `/one/auth/me` buyers **do not** see plan change (token is `""` on the portal GET, and the component is not mounted).

Client fetch: `GET /public/commerce/{slug}/portal/plans?token=` then `POST .../portal/change-plan` with `{ subscription_id, product_id }`. Null product_id clears pending. Copy: “No charge today.” Pending product name shown. If plans array is empty and no pending, the control returns `null`.

Ops-side plan change (Member Console) does not need a buyer token.

### 16.9 Documents

After Wave 2/4 the portal page:

1. Still shows per-sub `document_url` + `document_label` (“Download receipt” default) (`123:127`).
2. **Also** `GET /public/commerce/{tenantSlug}/portal/documents?token=` (`48:52`, `200:237`). Table: date, number, type, amount, LHDN status, Download. Empty: “No receipts or invoices yet.”

The old `[MVP-HIDE]` tax-invoice `<a href={/api/billing/invoice?...}>` is gone. This is a real list. Token-less cookie sessions still call the documents endpoint with `token: ""` — behavior is whatever the API does with an empty token plus cookie.

### 16.10 Update payment

`GET /public/commerce/checkout/{subId}/arrears` then branches:

| State | UI |
|-------|----|
| ACTIVE + reminder-only | “Invoice each cycle… no card on file.” Link to portal |
| ACTIVE | RM 1 verification charge, date unchanged. POST `.../update-payment` → redirect to gateway URL |
| not PAST_DUE/SUSPENDED (and not the ACTIVE branches) | “Account in Good Standing” (this branch is hard to reach because ACTIVE is handled above) |
| PAST_DUE / SUSPENDED | Amount due + “Complete Payment” same POST |

Portal page hides “Update payment method” when `is_reminder_only` (`170`). The dedicated URL can still be opened. Branding logo at top. **No magic token** — identification is cookie / server client. A mailed `/update-payment/{id}` link may 404 if the arrears GET fails.

### 16.11 Portal lifecycle otherwise

- No token + no cookie → magic-link form (`POST /public/commerce/{slug}/portal/magic-link`). Always “If that email has a subscription…” (no enumeration).
- Token or cookie → `GET .../portal`. Failure → `notFound()` (404, not 403).
- ACTIVE: Cancel Plan (period end) + Cancel immediately (two server actions, **errors ignored**).
- Flagged cancel-at-period-end: Keep plan.
- PAST_DUE: cancel immediately only; no period-end.
- TRIALING chip with `trial_ends_at`.
- Header “Identity Verified” is shown whenever the GET succeeded, including token-in-query.

Portal is English. EN/BM switcher is checkout-header only.

---

## 17. Remaining `[MVP-HIDE]`

Ripgrep of `*.{ts,tsx}` after Waves 1–4 finds **one** marker:

```242:244:apps/lazuar-ops/src/App.tsx
        {/* [MVP-HIDE] ADR 023 — ops chat remains disconnected
        <Route path="/ops/chat" element={<OpsChatWorkspace />} />
        */}
```

Everything ADR 023 originally hid that this eval cares about is remounted:

- Invoicing routes + sidebar — live.
- Billing profile — live.
- Portal `/pay/{sessionId}` — live.
- Checkout TIN — live.
- Portal documents — live.

Still disconnected **without** the tag: `OpsChatWorkspace`, `ConversationsDirectory`, chat `FormRegistry` / `CreateProductForm`, `CommunityPortalView`, both `PaymentSettingsModal`s, `prompt-library.ts`. Floating islands, not product.

007-feats prose that still says “quotes are MVP-HIDE” is inventory drift. This file is the current count.

---

## 18. Mobile vs desktop

Breakpoint is **768px**, inlined in ops and admin `App.tsx` (not `use-mobile.ts`, which exists and is unused in both apps).

**Ops / admin desktop:** 240px rail, collapse to icon flyouts, `md:relative`. Page content `p-6 md:p-8`, `max-w-6xl`. Tables `min-w-[800px]`–`min-w-[950px]` inside `overflow-x-auto`. Side panels + modal `p-4` + `max-w-sm/2xl`.

**Ops / admin phone:**

- Sidebar `absolute`, `x: -240` when closed, overlay only when open (`158:160:App.tsx`).
- On mount and on every resize `< 768`, `setIsSidebarOpen(false)`.
- `PageLayout` has **no menu button**. Breadcrumbs + workspace name can overflow (`max-w-[120px]`).
- **There is no way to open the sidebar after it closes.** A merchant on an iPhone who loads `/commerce/dashboard` sees Sales Insights and cannot reach Subscribers without editing the URL.
- Product/email warning bars on Checkout Links are `flex justify-between` without a wrap — “Configure Now” can clip.
- Subscriber filters (`w-64` search + status select) do not stack.
- Transaction filters (search + two selects + CSV) overflow.

**Portal phone** is the only surface that looks designed for a hand:

- Checkout `flex-col-reverse` so the order summary sits **above** the form (`CheckoutLayout.tsx:11`).
- Quantity stepper 44px-class hit targets (`h-8` is a bit short of 44px but usable).
- Address/TIN `grid-cols-1 sm:grid-cols-2`.
- Safe-area footer padding.
- QuoteView actions `flex-col-reverse sm:flex-row`.
- Portal subscription cards `flex-col md:flex-row`; documents table still `overflow-x-auto`.
- Update-payment is a single centered card `p-8 sm:p-12`.

Portal EN/BM control is in a sticky header and survives small widths (logo truncates).

---

## 19. What a merchant can click vs 403 / 404

“Merchant” here means an **ADMIN** of a workspace (the person who signed up). 404 in ops does not exist (catch-all → dashboard). 404 in portal is the localized page. 403 is almost never a page; it is a toast, a zero, or an empty table.

### Admin merchant — click works (200 + UI)

| Click | Result |
|-------|--------|
| Any sidebar item except Utility Ledger | Mounted page |
| Getting started → Open gateway / email / product | Those pages |
| Copy pay link | Clipboard `{portal}/{slug}/checkout/{slug}` |
| Create / edit checkout link, set trial, require TIN | `OrgMember` writes |
| Add subscriber, cancel, keep, record payment, refund, dunning pause, portal link | 200 or domain error toast |
| Change plan / seats / collection pause | 200 (also works for Viewer — see §20) |
| Anonymize | 200 |
| Create quote, copy `/pay/{id}`, mark paid | 200; buyer opens QuoteView |
| Sales documents / credit notes / download PDF / cancel e-invoice < 72h | 200 if Admin |
| Legal stationery + MyInvois + Check TIN + .p12 | 200 |
| Invite Admin/Member/Viewer, remove member | 200 |
| Audit table | 200, metadata hidden |
| Save CHIP/Billplz/Stripe/Razorpay vault | 200 |
| Save Xendit | **200 with empty credential body** unless they typed into leftover fields. No form. Functionally a trap. |
| API keys, webhooks, delivery redeliver | 200 |
| Plan pay + credit top-up | Redirect to platform checkout |
| `/workspace/ledger` typed by hand | 200 |
| `/ops/chat` | Catch-all → dashboard (not 404) |
| Buyer `/pay/bad-id` | Portal 404 |
| Buyer expired quote | QuoteView “Quote Expired,” no pay |

### Admin merchant — click looks live, outcome is wrong

| Click | What they think | What happens |
|-------|-----------------|--------------|
| Xendit in gateway dropdown | “I can connect Xendit” | No fields. Save stores an empty/partial Xendit row. |
| Product form “we do not validate TIN” | Checkout will take any TIN | Checkout **does** validate via MyInvois. |
| Disputes row | Manage chargeback | Read-only. |
| WhatsApp on subscriber | Message the customer in-app | Opens `wa.me`. Communications WhatsApp is not live. |
| Templates WhatsApp body | WhatsApp dunning | Not connected; credits page says so. |
| Credit note “issue a credit note” after 72h | A create button exists | There is no create. Refund path books the note. |
| Dashboard ARR tooltip | ARR-specific definition | Same MRR sentence. |

### Member — same chrome, different HTTP

Member is sold as “operate commerce.” That matches **commerce writes** and **not** keys / vault / team / billing / templates / LHDN.

| Click | HTTP |
|-------|------|
| Dashboard | Page paints. Stats 200. Net Cash 403 → `RM 0.00`. Checklist never completes (gateway + email 403). |
| Checkout Links banners | Always “configure gateway/email” (those GETs 403) even if an Admin already did. Create link 200. |
| Subscribers operate (except Anonymize) | 200 |
| Anonymize | **403** |
| Refund | 200 |
| Quotes create / mark paid | 200 |
| Sales documents / credit notes / legal / plan / ledger / email / payment vault / API keys | **403** on every billing/comms/vault GET+PUT |
| Team invite / remove | **403** (list 200) |
| Audit | 200 |
| Templates | **403** |
| Webhooks GET | 200; POST/rotate **401/403** via `CanAccessWorkspaceWebhooksAsync(manageRequired: true)` |
| General Settings Save | `Unauthorized to update workspace.` (command-level, Role must be `ADMIN`) |

### Viewer — sold as “can only read”

| Click | HTTP |
|-------|------|
| All GET commerce lists (products, subs, txns, disputes, coupons, dunning, quotes, stats, export CSVs) | **200** — including PII exports |
| Create / refund / cancel / record payment / dunning pause / add member / create coupon / deploy dunning | **403** toast |
| **Change plan, set seats, pause collection** | **200 — Viewer can mutate money-adjacent state** |
| Anonymize, vault, billing, legal, templates, keys, invites | 403 |
| Audit | 200 |
| Dashboard Net Cash / checklist | same lie as Member |
| Payment / email / billing pages | 403 load |

### 404 specifically

- Ops: none. `*` → dashboard.
- Admin: none. `*` → gateways.
- Portal: `not-found.tsx` for missing product, missing pay session, failed portal GET.
- Portal `/` is not 404; it is a dead-end landing card with no tenant picker.

---

## 20. Viewer / Member UX holes (the list)

These are holes in the **console**, not missing backends.

1. **No role chrome.** Entitlements include `role`. Context drops it. Sidebar, headers, and buttons do not change. The Team page *describes* the model and then ignores it.

2. **Viewer can change plan, seats, and collection pause.** Four POSTs sit on the `OrgRead` group without a tighter policy (`SubscriberEndpoints.cs:157-243`). The UI invites the click. This is the worst hole in the file: the product copy says Viewers read; the Member Console gives them Schedule / Set seats / Pause collection.

3. **Member cannot do the jobs next to commerce.** Templates (dunning copy), billing summary, sales documents, legal/MyInvois, email, vault, API keys, anonymize, workspace rename/branding — all Admin. A Member who “operates commerce” cannot fix the red banner that says email is missing, because the GET that drives the banner 403s even when email is configured.

4. **Dashboard is an Admin page wearing a shared layout.** Three of five queries are OrgAdmin. Non-admins get a convincing Sales Insights with `RM 0.00` Net Cash and an immortal Getting started card.

5. **Checkout Links scare banners are Admin GETs.** Member/Viewer always see “Payment Gateway Not Configured” / “Configure Email Provider.”

6. **Anonymize is shown to Member.** Policy is OrgAdmin. Same for Save Credentials, Invite, Remove, Check TIN, Save MyInvois, Purchase Credits.

7. **Audit 403 → empty state.** If policy ever tightens, Admins will think nothing happened. Today Viewer *can* read, so the swallow is latent.

8. **Team invites have no pending list and no self-disable.** Viewer clicks Invite, toast, still looks like an admin.

9. **Superadmin ≠ ADMIN membership.** Platform operators get every workspace in the switcher as `SUPER_ADMIN` but `UpdateWorkspaceCommand` only accepts `Role == "ADMIN"`. Saving General Settings can fail for the most privileged human.

10. **Xendit is a decoy.** Dropdown in ops and admin; no fields; Save is enabled.

11. **Product form lies about TIN validation.** Portal validates. QuoteView does not. Three different bars.

12. **Status filter on subscribers is fake pagination.** Viewer/Member/Admin all hit it.

13. **No page 2 on subscribers.**

14. **Mobile nav is a trap.** Sidebar closes; nothing opens it. This is worse than a 403: the merchant cannot click at all.

15. **Utility Ledger is a secret route.** Credits exist on Plan & billing; history is hidden.

16. **Portal plan change requires `?token=`.** Cookie session buyers get cancel/keep/update-payment but not the plan `<select>`.

17. **Portal cancel server actions ignore API errors.** Button looks like it worked; `revalidatePath` runs anyway.

18. **`/pay/{id}` completed CTA goes to `/portal` without the token.** Buyer falls back to magic link.

19. **ID type fields and interval toggles skipped i18n.** BM checkout is partial.

20. **Disputes are a museum.** Clickable nav, no action, no 403 chrome, no “connect Stripe Radar” empty state beyond “No open disputes.”

21. **Catch-all erase 404.** Bad bookmarks become the dashboard. Ops chat URL does too.

22. **WhatsApp affordances** (subscriber `wa.me`, template WhatsApp body, billing “not connected”) disagree with each other.

23. **Export CSVs are OrgRead.** Viewer can take the whole subscriber file.

24. **LHDN document GET is Admin-only.** Even if billing were opened to Member later, the side panel’s live status/QR would 403.

---

## 21. File-level inventory (so the next person does not re-hide Wave 2)

### Ops pages that are mounted

`DashboardPage.tsx`, `ProductsPage.tsx`, `SubscribersPage.tsx`, `TransactionsPage.tsx`, `DisputesPage.tsx`, `CouponsPage.tsx`, `DunningCampaignsPage.tsx`, `CampaignBuilderPage.tsx`, `TemplatesPage.tsx`, `QuotesPage.tsx`, `TaxInvoicesPage.tsx`, `CreditNotesPage.tsx`, `ApiKeysPage.tsx`, `DeveloperSettingsPage.tsx`, `DeliveryLogsPage.tsx`, `GeneralSettingsPage.tsx`, `TeamPage.tsx`, `AuditLogPage.tsx`, `BillingProfilePage.tsx`, `PaymentSettingsPage.tsx`, `EmailSettingsPage.tsx`, `BillingSettingsPage.tsx`, `UtilityLedgerPage.tsx`, plus `LoginPage.tsx`, `PricingPage.tsx`, `EmptyWorkspaceState.tsx`.

### Portal modules that are mounted

`CheckoutView` / `CheckoutForm` / `CheckoutLayout` / `OrderSummaryCard` / `PromoCodeInput` / `IdentityBanner` / `CheckoutSuccessView` / `QuoteView` / `CheckoutI18n` / `PortalPlanChange` / `RequestMagicLinkForm`. Branding helper. Legal articles.

### Portal module that is not mounted

`CommunityPortalView`.

### Admin module that is mounted

`PlatformPaymentSettingsPage` only.

---

## 22. What Waves 1–4 changed on these surfaces (re-checked, not tracker folklore)

Live now, previously described as hidden in 007-feats:

- Invoicing nav + quotes + sales documents + credit notes.
- Legal & Billing + MyInvois card + TIN check + cert upload.
- Checkout company/TIN/ID + MyInvois validate.
- Checkout quantity stepper + trial-days copy + due-today 0.
- Checkout EN/BM + cookie + `Accept-Language`.
- Workspace checkout logo + accent color (`--brand`).
- `/pay/{sessionId}` QuoteView.
- Portal documents table + per-sub receipt link.
- Portal plan change (token path).
- Subscriber plan/seats/collection/trial chips + refund modal + disputes page + audit page + team page.
- Dashboard MRR/ARR/recovered + getting-started.

Still true after those waves:

- One `[MVP-HIDE]` (ops chat).
- No role-aware chrome.
- Xendit option without a form (ops **and** admin).
- Billing + communications admin APIs are Admin-only, so half the new nav is a 403 for Member/Viewer.
- Mobile ops/admin cannot open the rail.
- Viewer write hole on plan/quantity/collection.
- Portal i18n does not cover portal/pay/update-payment.
- Admin app is still one screen.

---

## 23. Stop lines for the next eval

Do not claim LHDN UI is hidden. It is on Legal & Billing and Sales documents.  
Do not claim `/pay/{id}` 404s. It renders `QuoteView`.  
Do not claim TIN is stripped. It is validated.  
Do not claim there is no quantity control. There is.  
Do not claim there is no BM. There is, on checkout chrome/form/summary only.  
Do not claim Viewers cannot change money-adjacent state. They can, on three POSTs.  
Do not claim Xendit is configurable in the console. The enum value is. The form is not.  
Do not treat `lazuar-admin` as a control plane. It is a vault page with a Super Admin label.
