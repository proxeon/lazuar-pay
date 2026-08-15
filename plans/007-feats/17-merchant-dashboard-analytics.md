# 17 — Merchant dashboard, analytics, CRM

**Program:** Lazuar Pay competitor-feature analysis (subagent 17 of 20).  
**Product under review:** Lazuar Pay / Lazuar Console (`lazuar-ops`), not Aura salon OS.  
**Code root:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay`  
**As of:** 16 August 2026.  
**Nature:** Full uncondensed analysis. Do not summarize this file into a README. Tracker rows belong in the program tracker after all 20 chapters exist.  
**This file is not:** a commitment to ship ChartMogul, Sigma, or a Salesforce clone. It is a map of what merchants are trained to expect, what our console actually shows, and which numbers are honest.

---

## Method

### What this chapter is for

Lazuar Pay sells **Checkout-as-a-Service** to a merchant: hosted checkout links, recurring subscriptions, dunning, BYOK gateways (Billplz / Stripe / CHIP / Razorpay), a double-entry billing ledger, and a thin PII registry. The merchant-facing home is **Lazuar Console** (`apps/lazuar-ops`), titled “Sales Insights” on `/commerce/dashboard`.

The competitors that set the bar for *this* surface are not Fresha or Booksy. They are:

| Stack | Why it is in the set |
|-------|----------------------|
| **Stripe Dashboard** | Default mental model for “my payments console.” Billing analytics, payouts, team roles, Sigma, security history. Merchants who already have a Stripe account will open ours and look for the same jobs. |
| **Paddle + ProfitWell Metrics + Retain** | MoR + free MRR dashboard + recovered-MRR product. Closest Western analogue for “subscription metrics that just appear.” |
| **Chargebee RevenueStory** | Billing-native analytics with 150+ reports, collections, LTV, cohorts, CFO dashboard. What a serious subscription ops team expects once they outgrow a four-KPI strip. |
| **ChartMogul / Baremetrics** | Adjacent specialists. They exist *because* billing dashboards under-serve SaaS metrics. If we never ship MRR waterfalls, this is where merchants export. |
| **HitPay** | SEA / MY merchant dashboard that is *not* SaaS-MRR. Transactions, fees, net, payouts, roles, CSV. The local cash-ops analogue. |

Aura’s salon EOD / mix / staff-performance chapter is not this chapter. Guest money in Aura is System B through Lazuar Pay; this file is about **the Pay console the salon owner never sees unless they are also a Pay merchant**, and about Pay merchants who are not salons.

### How evidence was gathered

1. **Ops surfaces, live routes only.** `apps/lazuar-ops/src/App.tsx`, `Sidebar.tsx`, `PageLayout.tsx`, and every commerce / workspace page that is actually mounted. ADR 023 `[MVP-HIDE]` islands (invoicing, billing profile, ops chat) are noted as **unrouted**, not as shipped product.
2. **HTTP contracts.** TypeSpec `packages/api-spec/modules/{commerce,billing,crm,one}` plus the Minimal API implementations under `apps/lazuar-api/Modules/*/Infrastructure/Endpoints`.
3. **Read models that feed the dashboard.** `CommerceQueryService.Stats.cs`, `CommerceQueryService.Transactions.cs`, `CommerceQueryService.Subscribers.cs`, `CommerceQueryService.Dunning.cs`, `BillingQueryService.cs`.
4. **Write paths that create the numbers.** Gateway completion / failure handlers, `RecordSubscriberPaymentCommandHandler`, `DunningCampaign.RecordRecovery` / `RecordChurn`, `GatewayPaymentCompletedHandler` (Billing ledger), Stripe/Billplz adapters, `ProcessGatewayWebhookCommandHandler` fee arguments.
5. **Identity and tenancy.** `AuthEndpoints.IssueCookie`, `TenantSecurityMiddleware`, `WorkspaceEndpoints`, `InviteUserToWorkspaceCommand`, `OrgAdmin` policy in `AuthAndCorsExtensions.cs`.
6. **CRM as implemented, not as marketed.** `Modules/CRM/README.md` vs `ClientProfileEntity`, resolve/create/anonymize, `ICrmQueryService`, **zero HTTP routes**, **zero ops page**.
7. **Competitor primary docs (fetched 16 August 2026).** Stripe Billing Analytics, Stripe team roles, Stripe payout/reconciliation reports, Paddle ProfitWell Metrics, Paddle Retain recovery-rate help, Chargebee RevenueStory / metric taxonomy / users & roles, ChartMogul MRR chart + CRM positioning, Baremetrics comparison pages, HitPay payment reports + user roles.

### Honesty rules used below

A cell is **shipped** only if a merchant can perform the job in `lazuar-ops` against a live endpoint, and the number is computed from a named definition.  

- TypeSpec without UI is **backend-only**.  
- UI without a matching filter/field is **theater**.  
- A ledger that exists but is not linked in the sidebar is **dark matter**.  
- A KPI whose formula includes PAST_DUE as “active revenue,” or fees that are always `0`, is **partial** even if the card renders.

### What this chapter refuses

- Do not become ChartMogul. We are the billing system of record, not a warehouse overlay.
- Do not become Stripe Sigma. SQL-over-payments is a platform add-on, not a MY CaaS MVP.
- Do not become Salesforce. CRM README already forbids leads, deals, tickets.
- Do not show “Net Cash in Bank” unless it is cash after **actual** gateway fees and **actual** tax, reconcilable to a payout or a bank line. A signed double-entry *approximation* is allowed only if labeled as such.
- Do not copy HitPay’s payout product unless we become an acquirer. We are BYOK. Settlement lives on Billplz / Stripe / CHIP / Razorpay. Our job is **to attribute and explain**, not to pay out.

---

## Competitor dashboards

### How to read a competitor dashboard

Every mature payments / billing console is three products glued together:

1. **Cash register** — did money move, what fee, when does it land, can I refund, can I export.  
2. **Subscription intelligence** — MRR/ARR, movements, churn, LTV, cohorts, failed → recovered.  
3. **Control plane** — who on the team can see money, who can refund, audit log, multi-account / multi-brand.

Stripe and HitPay are strongest at (1) and (3). Paddle, Chargebee, ChartMogul, Baremetrics are strongest at (2). Chargebee and Stripe try to do all three. We currently attempt (1) with a four-card strip and a transaction table, attempt (2) in SQL that the dashboard mostly **does not render**, and barely attempt (3).

### Stripe Dashboard

**Primary docs:** [Billing Analytics](https://docs.stripe.com/billing/subscriptions/analytics), [user roles](https://docs.stripe.com/get-started/account/teams/roles), [payout reconciliation](https://docs.stripe.com/connect/supported-embedded-components/payout-reconciliation-report), [export / balance reports](https://support.stripe.com/questions/exporting-payment-data), Sigma / Data Pipeline under `/data`.

**Home / Billing overview.** Stripe’s Billing analytics are a first-class dashboard, not a side table. Documented metric families:

| Family | What the merchant sees |
|--------|------------------------|
| Revenue | MRR, MRR growth; usage MRR in preview |
| Usage | Aggregate usage, usage revenue |
| Subscribers | Active, active growth, new, ARPU, LTV |
| Trials | New trials, trial conversion |
| Churn | Subscriber churn rate, churned revenue, **retention by cohort** |
| Collections | AR aging on Billing overview → Collections (unpaid invoices by age). Historical AR aging requires Revenue Recognition. |

**Configurable definitions.** From Billing overview → Configure (24–48 h to apply):

- Subtract recurring discounts from MRR, or one-time discounts, or both. Permanent recurring discounts always subtract.
- Active subscriber = start of first billing period **or** first payment received.

This is the industry’s answer to “why doesn’t my MRR match my invoices.” Stripe publishes the mismatch: dashboard MRR is monthly-normalized **active + past-due** subscriptions, excluding trials, taxes, free plans, and usage-based products; annual ÷ 12. Invoice exports are cash events. **Neither is wrong; they measure different things.** A console that cannot say which one it is showing is lying.

**Drill-down.** Explore on MRR growth / churned revenue opens the chart plus a table of underlying events (who churned, who expanded). Filter/group by Product or Price (preview; not for multi-currency volume).

**CSV that finance can use (not a dump of a HTML table):**

- MRR per subscriber per month  
- Subscription metrics summary (MRR roll-forward, subscriber roll-forward, trial conversion, LTV)  
- Customer MRR changes (new / upgrade / downgrade / reactivation / churn log)

Payments themselves export as column-picked CSV. **Balance Summary** is the bank-statement analogue (charges, refunds, disputes, fees). **Payout Reconciliation** ties each automatic payout to the batch of transactions it settles, with summarized and itemized CSV, date and currency filters. Reporting API can request the same reports. Sigma is SQL + templates (active subscriptions, MRR trends, churned customers, failed payments) + scheduled email/webhook + AI query assist; it does **not** pre-compute a blessed LTV. Data Pipeline dumps to a warehouse.

**Failed payments / recovered.** Stripe Smart Retries + Revenue Recovery live next to Billing, not as three counters on a campaign row. Collections = unpaid invoices + aging. Failed-payment rate is a Sigma template, not a default KPI. Independent 2026 reviews still say Stripe does **not** give a single “recovery rate” the way Paddle Retain does.

**Net after fees and tax.** Fees live on `balance_transaction`. Tax is a separate Stripe Tax / invoice tax line. Net to merchant is **payout amount**, not “gross minus a guess.” The dashboard balance is the source of truth for “what will hit the bank.”

**Geographic.** Customer country, Radar, Sigma (`charges` × country), Tax location. Billing Explore is product/price, not geo, in the public docs.

**Roles (not a boolean admin).** Account Owner (unique; can close the account). Super Administrator, Administrator, IAM Administrator. Payment-side Analyst (refund, export, view payouts; cannot edit payout schedule). Connect-specific analysts. View-only / support / developer variants exist in the same catalog. Admins can **view security history audit logs**. SSO role IDs are first-class (`admin`, `analyst`, `super_admin`, …). Multiple roles union their permissions.

**Multi-account.** Organizations group accounts; Super Admin can attach accounts. Reports for multiple accounts are a documented reporting mode.

**CRM depth.** Customers object + Billing customer portal. Not a CRM. The job is “find this payer, refund, send invoice,” not pipeline.

**What Stripe trains merchants to expect that we do not have:** payout reconciliation, configurable MRR definition, cohort retention, LTV as a chart, role-separated refund vs settings, security audit log, balance-as-bank-statement, Sigma-grade ad hoc.

### Paddle (Billing dashboard + ProfitWell Metrics + Retain)

**Primary docs:** [ProfitWell Metrics](https://developer.paddle.com/concepts/retain/metrics), [Retain recovery rate](https://www.paddle.com/help/profitwell-metrics/retain/how-it-works/retain-recovery-rate). Live surface: vendors.paddle.com → Subscription Metrics. No extra install for Paddle Billing merchants.

**Metrics product (still free as of mid-2026 reviews, no MRR cap).** Refresh every **3–6 hours**. Benchmarks against **30,000+** companies.

Documented datapoints:

| Metric | Paddle’s published definition |
|--------|-------------------------------|
| MRR | Monthly revenue from all active subscriptions. All periods (month / quarter / year) converted to monthly. |
| Upgrade / downgrade | Value change. Switching to annual still counts as upgrade even if monthly MRR dips. |
| Churn | Customer stops. Counted when **paid period ends**, not when they click cancel. |
| Reactivation | Returning churned customers, **not** counted as new. |
| ARPU | MRR / customer count. |
| LTV | Typical tenure × typical spend. |

Dashboards / reports named in the developer doc:

- **MRR overview** — MRR, customers, ARPU, LTV, movements on one page. Demo: `demo.profitwell.com/app/trends/mrr-overview`.
- **Customers and engagement** — counts by segment, inactivity as churn risk.
- **Cohorts** — MRR retained into subsequent months by signup month.
- **Segment comparison** — preset + custom traits (account owner, campaign).
- **By plan** — up to five plans on MRR, churn, LTV.
- **Cash flow** — subscriptions, one-time charges, **refunds**, **platform fees**, filterable, **exportable**.

Distribution: Slack, HubSpot, Intercom, Salesforce. This is “metrics as a team product,” not a private founder screen.

**Retain (failed → recovered).** Separate product, historically take-rate on recovered revenue (~8–10% in 2026 comparison writeups). Metrics dashboard is explicit about honesty:

- Baseline = 3-month average recovery **before** Retain.  
- Recovery rate after activation. Credit taken if the user saw an in-app notification or recovery email (that attribution rule is something we should **not** copy blindly).  
- Daily recovery vs baseline.  
- Monthly: **Recovered MRR**, **At-risk MRR** (still in cycle; can revise last month), **Total past-due MRR**.  
- They publish the trap: recovery rate can stay 80% while delinquent churn **rises** on a growing book. We currently have no language for this.

**Net after fees.** Cash-flow view includes Paddle platform fees because Paddle is MoR. That is a different company shape from Lazuar Pay BYOK. Copying “net” without being MoR means we must net **gateway** fees, not a platform take on GMV.

**Roles / audit / multi-workspace.** Team invites exist (`paddle.com/help/start/set-up-paddle/can-i-invite-members-of-my-team`). Paddle is MoR-grade on tax/VAT; their console is one vendor account, not our workspace switcher. Do not treat Paddle as the RBAC template; Stripe and HitPay are.

**CRM.** Customers in Billing + Metrics segments. Not a CRM module.

**What Paddle trains merchants to expect:** MRR on the home screen the day they sell the first sub; cohort chart; recovered MRR as a first-class number; “churn = end of paid period” written down; cash flow that includes fees.

### Chargebee RevenueStory

**Primary docs:** [RevenueStory](https://www.chargebee.com/docs/billing/2.0/reports-and-analytics/chargebee-analytics), metric taxonomy under `/metric_description` (Accounting, Acquisitions, Billing & Taxes, Churn, Collections, Customer, Customer Insights, Leakage, Order, Premium, Receivable, Recurring Revenue, Expansion/Contraction MRR, Scheduled/Growth MRR, Retention, Subscriptions, Transactions, **CFO Dashboard**), [Users & Roles](https://www.chargebee.com/docs/billing/2.0/site-configuration/account_settings), activity/event logs.

**Shape.** 150+ ready-made reports behind a **RevenueStory** tab, plus a Home dashboard. Plan-gated. This is what “we have analytics” means at billing-platform scale.

Public metric definitions (FAQ / metric pages):

- **LTV** = paid subscription lifetime × ARPS. ARPS = month MRR / active paid subscriptions that month. Imported historical MRR included.  
- **MRR retention cohort** = MRR from a signup-month cohort retained over the next 12 months.  
- **Quick Ratio** = (New MRR + Expansion MRR) / (Downgrade MRR + Churned MRR).  
- **Total Billing** = sum of invoice amounts (cash/invoice view, not MRR).  
- Collections family = payment success, dunning, card expiry, refunds, cashflow — **Reveal** (2026) is the newer “payment performance end-to-end” product sitting on top.

Report builder + custom fields in RevenueStory + Reports Explorer. Classic reports are being sunset (dedicated FAQ). Stitch / warehouse integrations for people who outgrow the UI.

**Failed / recovered.** Collections + dunning reports + card-expiry. Not a single campaign row with `saved / churned`. Chargebee’s dunning is a product; RevenueStory is how finance sees whether it worked.

**Net after fees and tax.** Billing & Taxes + Accounting + Receivable + CFO dashboard. Chargebee is not the acquirer; fee fidelity depends on the gateway connected. They still **name** tax, refunds, credits, and unbilled separately. RevRec is a sibling product (ASC 606 / IFRS 15) — we have a parked `RevenueRecognitionJob` and should not claim parity.

**Geographic / cohort.** Retention + customer insights. Custom fields flow into reports. Multi-business-entity is a first-class Chargebee concept (multiple legal entities on one site).

**Export.** Report builder / explorer exports; event logs; bulk operations. Finance teams live here.

**Roles.** Settings → Team Members. Published roles include Owner (site creator), Admin, Finance Executive, Analyst, Sales Agent, Sales Manager, Customer Support, Tech Support, Developer. Custom roles exist. Activity: **Logs → Events** plus per-record activity logs (“which teammate did this”).

**CRM.** Full Customers object, account hierarchy, comments, attachments, reason codes. Still not Salesforce, but **orders of magnitude** deeper than a `ClientProfile` row.

**What Chargebee trains merchants to expect:** a Reports tab with more than four numbers; named LTV/cohort/quick-ratio formulas; collections as its own dashboard; teammate-attributed activity; finance-shaped exports.

### ChartMogul and Baremetrics (adjacent specialists)

These are **not** payment processors. Merchants add them when Stripe/Paddle/Chargebee dashboards are not enough. If we leave a hole, this is where the CSV goes.

**Shared core (both, 2026 comparisons):** MRR, ARR (often a toggle on the same chart), LTV, ARPU/ARPA, customer churn, revenue churn, **cohort retention tables**, segmentation, annotations, live event stream, CSV / API export.

**ChartMogul** ([MRR chart](https://help.chartmogul.com/article/154-chart-monthly-recurring-revenue-mrr), updated 29 June 2026):

- MRR = sum of MRR of **active + past due** in the period. Monthly plans = amount paid; other intervals from invoice × interval.  
- One-time lines excluded. Discounts offset MRR.  
- Refunds deducted from MRR **only for Stripe**; other billing systems do not deduct. That footnote is the kind of honesty we need in our own glossary.  
- Transaction-fee handling is a **setting** (include or exclude fees from MRR).  
- Movements: New / Expansion / Reactivation / Contraction / Churn; Net MRR Movements chart.  
- Segmentation on plan, geography, custom attributes.  
- CMRR (committed future MRR). Benchmarks. Mobile app. Goals/targets.  
- Warehouse export on Pro+ (Snowflake, BigQuery, Redshift, S3, Azure).  
- **Native CRM on every plan** (opportunities, tasks, email sequencing; CRM Pro seats called out at $39/user/mo in 2026 Baremetrics comparison copy). Customer profile = contact + MRR/ARR + subscriptions + transactions + MRR movements. Multi-billing-system merge of the same human.

**Baremetrics:**

- Daily MRR waterfall (New / Expansion / Reactivation / Contraction / Churn).  
- Net revenue **and fees**, user + revenue churn, at-risk revenue (expiring cards).  
- Unlimited segments, overlayable, each with a saved dashboard.  
- Recover product: failed-payment chase, flat-fee positioning in 2026 ($129/mo cited on their Chargebee comparison; treat as marketing until we price).  
- Stripe-native visual storytelling. Connects to Stripe and Chargebee.

**Geographic.** First-class segment in both. ChartMogul’s cohort-by-geo is the depth target people mean when they say “cohort, geographic.”

**Roles / audit.** Team seats, shared dashboards. Not a payments audit log. The audit they care about is **metric reconstructability** (why did MRR move on Tuesday).

**What they train merchants to expect:** MRR **waterfall**, not a single stock number; LTV; cohort heatmaps; geo slices; fee toggle; a customer record that is the metric, not a row in a PII table.

**Implication for us:** we will never out-ChartMogul ChartMogul. We must (a) compute a **documented** MRR/churn, (b) expose movements or admit we don’t, (c) make export good enough that ChartMogul can ingest us if the merchant wants the specialist, (d) not invent a CRM-with-opportunities.

### HitPay dashboard (SEA / MY cash ops)

**Primary docs:** [Payment reports](https://docs.hitpayapp.com/reporting/payments) (modified 15 July 2026), [User roles](https://docs.hitpayapp.com/setup/user-management-roles) (16 October 2025), [cashier restrictions](https://docs.hitpayapp.com/setup/user-management-cashier), payout / MY settlement marketing (T+1 MYR, DuitNow / FPX / e-wallet / cards into one payout cycle).

HitPay is the local product a Malaysian SME already understands. It is **not** an MRR console. It is a **cash and settlement** console. That is the job our “Net Cash in Bank” card is pretending to do.

**Transactions + reports.**

- Web dashboard → Transactions → **Export** (column picker). Email when ready. Cashiers cannot export others’ sales.  
- Sample export columns: ID, Channel (Payment Gateway vs POS), Plugin + plugin reference, Method, Status (successful / voided / refunded), additional provider reference, Order ID, customer name, receipt recipient, remarks, products, currency, **charge amount**, **refunded amount**, cashback + cashback fee, converted amount, **all-inclusive fee**, **net amount**, payment details (domestic vs international card), completed date, store URL (multi-storefront).  
- **Bank Payouts → Reports:** Transactions list, Sales summary (gross, refunds, net sales, fees, net collected), Sales by payment method, Sales by products (summary + detailed). Filters: sales channel + date from/to.  
- Per-charge **Fees breakdown**: method fee (fixed + %), plugin fees, FX, total.

**Payouts / reconciliation.** HitPay is closer to an acquirer / facilitator. MY methods settle T+1–T+2 to the merchant bank. Dashboard unifies online + POS + plugins into **one payout cycle**. Accounting sync (Bukku, Xero) pushes payout records so the merchant does not journal from bank statements. Multi-brand: filter transactions by payment link or storefront before payout.

This is the reconciliation job: **gross → fees → net → payout batch → bank**. We cannot copy the payout, but we can copy the **shape of the report** using `external_reference` + gateway fee + date, and tell the merchant to match against Billplz / Stripe payout CSV.

**Roles.** Owner, Admin, Manager, Cashier.

| Role | Money | Team | Reports |
|------|-------|------|---------|
| Owner | All refunds, all locations | Invite anyone, remove Admins | All |
| Admin | All refunds | Invite Manager/Cashier only; cannot remove Admins | All |
| Manager | Refunds for unlocated + own location | No | Own location (+ unlocated API/plugin) |
| Cashier | Own POS / recurring / invoices; refund **off** unless Owner/Admin enables | No | Own transactions only |

2FA recommended. Password sharing explicitly discouraged so auditability is possible.

**Audit.** Role split + per-user transaction scope is the audit. Not a Stripe-style security history, but a cashier cannot see the owner’s refunds.

**CRM.** Customers on invoices / recurring, not a CRM module.

**What HitPay trains MY merchants to expect:** fee and net on every row; payment-method mix; payout reports; CSV emailed; staff who cannot refund; one login, many brands/locations.

### Competitor synthesis (jobs, not logos)

| Merchant job | Stripe | Paddle | Chargebee | CM/Bare | HitPay |
|--------------|--------|--------|-----------|--------------------------|--------|
| See MRR / ARR today | Y (Billing; ARR via ×12 / Sigma) | Y (Metrics) | Y (RevenueStory) | Y | N (not their job) |
| See churn + LTV | Y | Y | Y | Y | N |
| Failed → recovered | P (retries + collections; weak single rate) | Y (Retain) | Y (collections + Reveal) | P / Y (Baremetrics Recover) | N |
| Net after fees + tax | Y (balance + tax + payout) | Y (MoR cash flow) | P (tax/invoice strong; fees via gateway) | P (fee toggle, not payout) | Y (fee + net + payout) |
| Cohort | Y (retention by cohort) | Y | Y | Y (deepest) | N |
| Geographic | P (Sigma / Tax / customer country) | P (segments) | P (insights / custom fields) | Y | P (store / channel, not geo heatmap) |
| Export CSV | Y (many reports + Sigma) | Y (cash flow + metrics) | Y | Y | Y (transactions + sales reports) |
| Reconcile to gateway / bank | Y (payout recon) | Y (MoR settlement) | P | N | Y (payouts) |
| Staff vs owner | Y (rich roles + audit) | P | Y (Owner … Developer + custom) | P (seats) | Y (Owner/Admin/Manager/Cashier) |
| Multi-workspace / brand | Y (Orgs / accounts) | P | Y (sites + multi-entity) | P | Y (brands / store URL / locations) |
| Audit log | Y (security history) | P | Y (events + activity) | N | P (role isolation) |
| CRM depth | Thin customer | Thin | Medium | Medium–high (ChartMogul CRM) | Thin |

---

## Our ops surfaces

### Console shell (what a merchant can actually click)

**App:** `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops`  
**Auth:** cookie JWT via `GET /one/auth/me`. Empty entitlements → “Access Denied.”  
**Tenant:** `X-Tenant-Id` = `localStorage.ops_active_workspace_id`. Workspace switcher in `PageLayout` (not the sidebar). Switching navigates to `/commerce/dashboard`.

**Routed IA (`App.tsx`, ADR 023 Pure CaaS):**

| Nav group | Label in sidebar | Route | Page |
|-----------|------------------|-------|------|
| Commerce | Dashboard | `/commerce/dashboard` | `DashboardPage.tsx` |
| Commerce | Checkout Links | `/commerce/products` | `ProductsPage.tsx` |
| Commerce | Subscribers | `/commerce/subscribers` | `SubscribersPage.tsx` |
| Commerce | Transaction Logs | `/commerce/transactions` | `TransactionsPage.tsx` |
| Commerce | Promotions | `/commerce/coupons` | `CouponsPage.tsx` |
| Commerce | Dunning Campaigns | `/commerce/dunning-campaigns` | `DunningCampaignsPage.tsx` + builder |
| Commerce | Notification Templates | `/commerce/templates` | `TemplatesPage.tsx` |
| Developer | API Keys | `/developer/api-keys` | `ApiKeysPage.tsx` |
| Developer | Outbound Webhooks | `/developer/webhooks` | `DeveloperSettingsPage.tsx` |
| Developer | Delivery Logs | `/developer/logs` | `DeliveryLogsPage.tsx` |
| Workspace | General Settings | `/workspace/general` | `GeneralSettingsPage.tsx` |
| Workspace | Payment Gateways | `/workspace/payment-gateways` | `PaymentSettingsPage.tsx` |
| Workspace | Email Provider | `/workspace/email` | `EmailSettingsPage.tsx` |

**Routed but not in the sidebar (orphan / dark matter):**

- `/workspace/billing` — `BillingSettingsPage.tsx` (platform **utility credits** top-up).  
- `/workspace/ledger` — `UtilityLedgerPage.tsx` (last 50 credit-wallet rows).  
- No page at all for `GET /admin/billing/ledger` (double-entry journal).  
- No page at all for `GET /admin/billing/net-profit`.  
- No page at all for CRM.  
- No page at all for workspace members / invites (API exists).

**Intentionally unrouted (`[MVP-HIDE]`, ADR 023):** quotes, tax invoices, credit notes, billing profile (legal/TIN), ops AI chat. Backend left intact. Do not count them as merchant analytics.

**Platform admin (`lazuar-admin`)** is a different app: superadmin gateway settings only. Not a merchant dashboard.

There is **no** Reports tab, **no** Analytics tab, **no** Customers tab, **no** Team tab, **no** Payouts tab, **no** Audit tab.

### Dashboard — “Sales Insights”

File: `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx`

**Queries:**

| Query key | Endpoint | Used for |
|-----------|----------|----------|
| `commerce-stats` | `GET /admin/commerce/stats` | Active subs, past due, churn %, cash-flow bars, payment-method footer, “Total” on the chart |
| `financial-summary` | `GET /admin/billing/summary` | **Only** `net_revenue`, labeled **“Net Cash in Bank”** |
| `commerce-products` | `GET /admin/commerce/products` | Product catalog table (name, price, interval, active/archived) |
| `payment-config-status` | `GET /admin/commerce/payment-config` | Gateway warning banner |
| `email-config-status` | `GET /admin/communications/email-config` | Email warning banner |

**Four KPI cards, in order:**

1. **Net Cash in Bank** — `formatMYR(financials?.net_revenue || 0)`. Not cash. Not bank. See Metrics honesty.  
2. **Active Subscribers** — `stats.active_subscribers`.  
3. **Past Due** — `stats.past_due_subscribers`, amber when > 0.  
4. **Cancellation Rate** — `` `${stats.churn_rate_percentage || 0}%` ``. Label says cancellation; field is `churn_rate_percentage`.

**Not rendered, despite being in `CommerceStatsDto`:**

- `mrr`  
- `cancelled_subscribers`  
- `net_new_last_30_days`  
- `average_revenue_per_user`  

The API computes MRR and ARPU. The home screen hides them. Paddle’s MRR overview is the opposite design.

**Revenue Trend.** Recharts `BarChart` on `stats.cash_flow_trend` (`month`, `amount`). Header “Total” is `total_revenue_collected` (lifetime confirmed transaction-log sum), **not** the sum of the six bars. Empty state: “No confirmed payments yet.” Footer “By source” shows up to three `payment_methods` rows.

**Product Catalog.** Half the dashboard is a product list. That is merchandising, not analytics. No revenue-per-product, no subscriber-per-product, no attachment to the MRR the API already has.

**Banners.** Gateway not configured (no active key) → rose, link to Payment Gateways. Email not configured → amber, link to Email Provider. These are onboarding, not metrics.

**Date range.** None. No from/to on the dashboard even though `GET /admin/billing/summary?from_date&to_date` exists and the query service honors it.

**Refresh / compare / annotate.** None.

### Stats endpoint (the real dashboard backend)

**Contract:** `packages/api-spec/modules/commerce/models/stats.tsp` + `GET /admin/commerce/stats` (`StatsEndpoints.cs`).  
**Impl:** `apps/lazuar-api/Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs`.  
**Auth:** `/admin/commerce` group is `OrgAdmin` (JWT role `SUPER_ADMIN` or `ADMIN` after tenant middleware injects membership).

DTO:

```
mrr, active_subscribers, past_due_subscribers, cancelled_subscribers,
net_new_last_30_days, churn_rate_percentage, average_revenue_per_user,
total_revenue_collected, cash_flow_trend[], payment_methods[]
```

**Algorithm (verbatim from code, 16 Aug 2026):**

1. Load every non-`PENDING` subscription for the org, joined to current product price and interval.  
2. **Active set** = `ACTIVE` **or** `PAST_DUE`.  
3. **MRR** = sum of product price; if `Interval == "yr"` then `price / 12`; **else treat as monthly**. Weekly, quarterly, `one_time` leftovers in the non-pending set, custom intervals — all become “monthly.”  
4. **Cancelled last 30** = status `CANCELED` and `UpdatedAt >= now-30d`. There is no `CanceledAt`. Any update to a canceled row (or a row that was canceled earlier and touched) can move the window.  
5. **New active last 30** = members of the active set with `CreatedAt >= now-30d`.  
6. **Active 30 days ago** = `activeNow + cancelledLast30 - newActiveLast30`. This is a stock-flow identity that **ignores** mid-window status flicker, `SUSPENDED`, reactivations, and imports.  
7. **Churn %** = `cancelledLast30 / active30DaysAgo * 100`, 2 dp, or 0 if denominator 0.  
8. **ARPU** = `mrr / activeSubs.Count` (so PAST_DUE is in the denominator).  
9. Load **all** `commerce.TransactionLogs` for the org (unbounded).  
10. **Total revenue** = sum of `CONFIRMED` amounts (refunds are a different status; a refunded row is **excluded entirely**, not netted).  
11. **Cash-flow trend** = CONFIRMED amounts grouped by UTC calendar month, last 6 months including zeros.  
12. **Payment methods** = CONFIRMED grouped by `RecordedByName` uppercased (fallback `UNKNOWN`). This is **who recorded it** (`SYSTEM`, `BANK_TRANSFER`, `CASH`, a human name, `AI Agent`), not FPX vs card vs DuitNow.

No ARR. No LTV. No expansion/contraction. No failed count. No recovered count. No geo. No cohort. No date parameters. No pagination of the source queries — full table into memory.

Older gap note (`docs/001-gaps/07-commerce-module.md`) said revenue/trend/methods were **stub zeros**. That is **stale**. They are filled from `TransactionLogs` as of this read. The remaining honesty problems are definitional, not “always zero.”

### Transactions — “Transaction Logs”

**UI:** `TransactionsPage.tsx` + `TransactionDetailPanel.tsx`.  
**API:** `GET /admin/commerce/transactions`, `POST /admin/commerce/transactions/{id}/refund`.  
**Table:** `commerce.TransactionLogs` / `CommerceTransactionLog`.

**List columns:** date/time, customer name+email, amount (RM, amber if REFUNDED), status badge + “method”, internal id + `external_reference`, recorded-by (system shield vs user icon).

**Filters in the UI:** search (name/email), status `ALL | CONFIRMED | REFUNDED`, method `ALL | ONLINE_GATEWAY | BANK_TRANSFER | CASH | COMPED`.

**What the API actually filters (`CommerceQueryService.Transactions.cs`):**

- `OrganizationId`  
- `Status` if provided  
- Search on `CustomerName` / `CustomerEmail` ILIKE  
- **`payment_method` query parameter is accepted by TypeSpec and the endpoint and then ignored.**  
- `RecordedByName` is selected as `PaymentMethod` in SQL and mapped to **`recorded_by_name`** on `TransactionLogDto`.

`TransactionLogDto` (`models/subscriber.tsp`) has **no** `payment_method` field. The React table reads `tx.payment_method`. That is undefined at runtime unless something else hydrates it. The “method” column is theater.

Statuses on the entity: created as `CONFIRMED`, can `TransitionToRefunded()`. **There is no `FAILED`, `PENDING`, `DISPUTED`.** Failed charges live in `ChargeAttemptLog` (no admin list, no ops page).

**Detail panel “Financial Breakdown”:** Gross = `amount`, Gateway Fee = `fee_amount`, Net Cash Settled = `net_amount` (`amount - feeAmount` at write time). Refund = full refund request; reason collected in UI and **not sent** (body `{}` from the detail panel; subscribers panel sends `{ subscription_id }` only).

**No CSV.** No date range. No gateway-id column beyond `external_reference`. No fee totals in the list header. No failed-payment inbox.

**Write paths into the log:**

| Path | Amount | Fee | RecordedBy |
|------|--------|-----|------------|
| Gateway payment completed | `AmountPaid` | `GatewayFee` from webhook parse | `"SYSTEM"` |
| Admin record-payment | entered amount (`0` if COMPED) | **always 0** | the method string (`BANK_TRANSFER` / `CASH` / `COMPED`) |
| Refund | same row flipped to `REFUNDED` | fee not reversed on the row | — |

### Subscribers — “Subscriber Directory”

**UI:** `SubscribersPage.tsx` + `CreateSubscriberModal.tsx`.  
**API:** list, export, create, cancel, record-payment, dunning pause/resume, portal-link.

**List:** customer, product+price, status, period end. Client-side status filter (`ALL/ACTIVE/PAST_DUE/CANCELED/SUSPENDED`) over the **current page** of the API. API itself does not take a status query (TypeSpec: page, limit, search only). Filtering PAST_DUE on page 2 of a mixed list is wrong.

**Query impl:** loads **all** non-pending subs for the org (no SQL pagination), joins products + dunning campaign name, hydrates CRM profiles in bulk, **then** filters search in memory, **then** pages. Export reuses the same query with `limit: 10_000`.

**CSV (`GET /admin/commerce/subscribers/export`):** UTF-8 BOM. Columns: `id, customer_name, customer_email, customer_phone, product_name, product_price, status, current_period_end, next_billing_date, created_at`. No MRR, no LTV, no geo, no failed-payment count, no lifetime paid, no dunning fields. Filename `subscribers_export_yyyyMMdd.csv`.

**Member console (side panel):** profile (name, mailto, tel, `wa.me`), product, status, period end, auto-debit yes/no, PAST_DUE-only dunning card (campaign name, step, pause/resume), Log Payment / Cancel / Copy Stripe portal link, payment list **re-queried as transactions search-by-email** (can collide if two people share an email; can miss if the log email drifted).

**Not a CRM.** One subscription row ≠ a customer. Two products = two rows. No notes, tags, company, country, timeline, consent editor, merge.

**Charge attempts** are not shown. Vault-failed but still `ACTIVE` subscribers do not get the recovery panel (`status === "PAST_DUE"` only) — this matches the dunning gap doc.

### Dunning campaigns (recovered / saved / churned)

**UI:** `DunningCampaignsPage.tsx`. Columns include **Recovered Revenue**, **Saved / Churned**.

**Domain:** `DunningCampaign.RecoveredRevenue`, `SavedSubscriptions`, `ChurnedSubscriptions`. `RecordRecovery(amount)` adds amount and increments saved. `RecordChurn()` increments churned.

**When recovery fires:** `GatewayPaymentCompletedIntegrationEventHandler.Subscription` if the sub **was** `PAST_DUE` or `SUSPENDED`, campaign id from metadata or `CurrentDunningCampaignId`, `RecordRecovery(@event.AmountPaid)` (gross paid, not net, not MRR).

**When churn fires:** cancel terminal path (asymmetric: `SUSPEND` final action does not increment churned — see `docs/001-gaps/01-dunning-engine.md`).

There is **no** dashboard roll-up of recovered revenue. No recovery rate. No at-risk MRR. No failed-payment count. Counters are campaign-lifetime, not monthly. No identity of *which* sub recovered (Paddle’s “radically transparent” Retain dashboard this is not).

`ChargeAttemptLog` (`PENDING/SUCCEEDED/FAILED`, source `BILLING`/`DUNNING`, attempt number, gateway response) is the raw material for a failed-payments report. It has **zero** query surface.

### Billing summary, net-profit, double-entry ledger

**README claim** (`Modules/Billing/README.md`): Billing is financial truth; never compute net cash from Commerce logs; always `LedgerLines`.

**Endpoints (all `OrgAdmin`, all implemented — the 2025 gap “net-profit not implemented” is stale):**

| Route | Query service | UI |
|-------|---------------|----|
| `GET /admin/billing/summary?from_date&to_date` | `GetFinancialSummaryAsync` | Dashboard card only (`net_revenue`, no dates) |
| `GET /admin/billing/net-profit?period=monthly\|yearly` | `GetNetProfitAsync` | **None** |
| `GET /admin/billing/ledger` | `GetLedgerEntriesAsync` | **None** |
| `GET /admin/billing/ledger/{id}/document` | R2 presign | **None** |
| `GET /admin/billing/credits` | wallet + last 50 | orphan `/workspace/ledger` |
| `GET/PUT /admin/billing/profile` | legal/TIN | ADR 023 hidden |

**`FinancialSummaryDto`:** `gross_revenue`, `total_gateway_fees`, `total_tax_liabilities`, `net_revenue`, `deferred_revenue`, `recognized_revenue`, `currency` (hardcoded `'MYR'`).

**Net revenue formula (signed sums):**

```
net = -SUM(REVENUE_GROSS)
    - SUM(CONTRA_REVENUE_REFUNDS)
    - SUM(EXPENSE_DISCOUNT)
    - SUM(EXPENSE_GATEWAY_FEE)
    - (-SUM(LIABILITY_TAX_PAYABLE))
```

Tested (`BillingQueryServiceTests`): sale 100 / fee 5 / tax 10 → net 85; software top-up expense ignored; half refund + tax reverse → net 40. This is **accounting net after fees, discounts, refunds, and tax liability**, not a bank balance.

**`NetProfitDto` (per UTC month or year):** gross, gateway_fees, refunds_issued, net_profit, MYR.

Net profit formula **drops tax** and **subtracts `EXPENSE_COMMISSION`**. So:

- Dashboard “Net Cash in Bank” **subtracts tax**.  
- `/net-profit` **does not subtract tax**, **does subtract commissions**.  
- Two endpoints, two “nets,” no UI for the second, misleading label on the first.

**Ledger write for a gateway sale** (`GatewayPaymentCompletedHandler`): skip `utility_credit_topup`; idempotent on `(GATEWAY_PAYMENT, GatewayTransactionId)`; lines = `ASSET_CASH` (net), `EXPENSE_GATEWAY_FEE` (fee), `REVENUE_GROSS` (−(amount−tax)), `LIABILITY_TAX_PAYABLE` (−tax). B2C gets a receipt number + PDF. Deferred revenue **not** created. `RevenueRecognitionJob` parked (decision 00.3).

**Fee that enters those lines** is `@event.GatewayFee` from Payments. Webhook handler passes `estimatedFeePercentage = 0`, `fixedFee = 0`, `taxRate = 0` (“removed from config”). Billplz therefore computes `gatewayFee = 0` unless the adapter has another source (it does not). Stripe can expand `balance_transaction` and still **falls back to 0** if expand fails. CHIP/Razorpay extract provider fees when the payload has them.

**Consequence:** `total_gateway_fees` and the dashboard net are **gross-as-net** for the MY default rail (Billplz) until we persist real settlement fees.

### Utility ledger (do not confuse with the accountant)

`UtilityLedgerPage.tsx` + `GET /admin/billing/credits`.

This is the **platform prepaid wallet** (`TenantCreditBalance` / `CreditLedgers`): integer credits for LHDN submit / WhatsApp (product copy on `BillingSettingsPage`: “tax e-Invoicing or sending WhatsApp recovery messages”). Last **50** rows. Refresh button. Not GMV. Not MYR. Not merchant settlement.

`BillingSettingsPage` sells credit packages and starts a top-up checkout. Top-up is explicitly excluded from merchant GMV in `GatewayPaymentCompletedHandler` (`type == utility_credit_topup`).

**Naming collision:** merchants will think “Utility Ledger” is the sales ledger. It is FinOps for our own SaaS meters. The real sales ledger has no screen.

### CRM module (thin, internal, no console)

**Intent (README):** tenant-scoped PII registry. Not leads, deals, tickets, messaging, or access control.

**Entity:** `ClientProfileEntity` — name, email, phone, TIN, id type/value, `BillingAddress` (default country `MYS` on the value object), `ConsentedToMarketing`, optional `GlobalUserId`. `Anonymize()` wipes to `deleted_{id}@localhost`.

**HTTP:** none. TypeSpec models exist “models-only” (`packages/api-spec/modules/crm/models.tsp`). NSwag emits DTOs for inter-module use.

**Contracts in use:** `ResolveClientProfileCommand` (checkout / enroll), `ICrmQueryService` (get by id / bulk / by email). `CreateClientProfileCommand` still exists. `AnonymizeClientProfileCommand` + `ClientProfileAnonymizedIntegrationEvent` exist; workers **are now registered** (`AddModuleOutboxInbox` in `CRM/Infrastructure/DependencyInjection.cs`) — older gap text that said “no workers” is stale. Downstream consumers of anonymize are still the thing to verify before claiming PDPA-complete.

**Resolve behavior:** match by **email only**; enrich empty phone/TIN/id/address; `ConsentedToMarketing` default **false** on the command (stale gap said forced true). Phone normalize: strip punctuation, leading `0` → `60`.

**Ops appearance:** name/email/phone on subscriber rows and transaction rows, denormalized onto `TransactionLogs` at write. WhatsApp deep link. No customer directory, no search-all-profiles, no consent toggle, no address display, no merge, no notes, no last-seen, no LTV on the person.

A merchant coming from Chargebee Customers or ChartMogul profiles will call this “we don’t have CRM.” That is accurate.

### Multi-workspace

**What works:**

- `GET /one/me/entitlements` → list of `{ workspace_id, name, slug, role }`.  
- Superadmin: **every active organization**, role stamped `SUPER_ADMIN`.  
- Everyone else: memberships joined to orgs.  
- `PageLayout` switcher + “Create New Workspace” (`CreateWorkspaceModal` posts `provision_apps: ["OPS","BILLING","PAYMENTS","CRM","LHDN"]`).  
- General settings edit name/slug (slug change confirm: breaks public links).  
- LocalStorage remembers last workspace.

**What does not:**

- No cross-workspace roll-up dashboard (Stripe Organizations / HitPay multi-brand reports).  
- No workspace-level role shown in the switcher.  
- No members UI.  
- Superadmin entitlements list all orgs, but `TenantSecurityMiddleware` **does not** special-case `IsSystemAdmin`. `GetTenantRoleAsync` is membership-only. **No membership row → 403** on `/admin/*`. Superadmin “operate any workspace” is true in the switcher and false at the API unless someone also inserted `TenantMembership`.  
- Invite API (`POST /one/workspaces/{id}/invites`) requires the inviter’s membership role **exactly** `"ADMIN"`. A `SUPER_ADMIN` membership string fails. A system admin with no membership fails. Role string on the invite is **unvalidated** (any string stored).  
- `CLIENT` memberships exist (portal / buyer). They get an entitlement. If they open ops and pick that workspace, middleware injects `CLIENT`. `OrgAdmin` requires `ADMIN`/`SUPER_ADMIN` → 403 on every commerce/billing call. There is no “viewer” experience; there is a brick wall.

### Role-based access (staff vs owner)

**JWT at login** (`AuthEndpoints.IssueCookie`): `SUPER_ADMIN` if `IsSystemAdmin`, else **`CLIENT`**. The merchant who just registered is returned `Role: "ADMIN"` on the register response body, but the cookie is still `CLIENT` until tenant middleware adds the membership role.

**Membership roles in the wild:** `ADMIN`, `CLIENT`, occasionally `SUPER_ADMIN` as a string. There is **no** `OWNER`, `STAFF`, `ANALYST`, `CASHIER`, `VIEWER`, `FINANCE`.

**`OrgAdmin` policy:** authenticated + role `SUPER_ADMIN` or `ADMIN`. This gates keys, payment config, commerce admin, billing admin, communications admin.

**Invite / remove:** handlers check `membership.Role != "ADMIN"` and throw. No owner-vs-admin split (HitPay: Admin cannot remove Admins; only Owner can). No “this person can see the dashboard but cannot refund.” Refund is the same `OrgAdmin` as “rotate the Billplz secret.”

**Ops UI:** user menu is email + logout. No team page. Prompt library still has “Invite Staff” / “Revoke Access” for the **hidden** ops chat. That is a ghost feature.

Stripe Analyst vs Admin, HitPay Cashier vs Owner, Chargebee Finance Executive vs Developer — we have none of these jobs.

### Audit log

**Grep for `AuditLog` / `audit_log` / `AuditEvent` in the Pay repo: no matches.**

Closest artifacts:

| Surface | What it is | What it is not |
|---------|------------|----------------|
| `DeliveryLogsPage` | Outbound **webhook** deliveries (status, event type, attempts, last error) | Human actions |
| `TransactionLogs.RecordedByName` | SYSTEM vs a display name | Immutable actor id, IP, before/after |
| Ledger `ReferenceType` + `ReferenceId` | Idempotent economic event | Who clicked refund |
| Ops chat proposed-action (hidden) | HITL for the agent | Staff audit |

Stripe “view security history audit logs.” Chargebee “Logs → Events” + per-object activity. We cannot answer “which staff member refunded this at 16:02.”

### Reconciliation against the gateway

There is **no** payouts page, **no** settlement batch, **no** “compare to Billplz CSV / Stripe Payout Reconciliation.”

What exists instead:

- `external_reference` = gateway transaction id on commerce logs.  
- Billing ledger unique `(ReferenceType, ReferenceId)` = same gateway id for payments.  
- Two books that are **not** tied in the UI: `commerce.TransactionLogs` (cash-register) vs `billing.LedgerEntries` (accountant). Offline record-payment writes a commerce log + `ManualSubscriberEnrolledIntegrationEvent` (Billing handler exists for enroll). Custom/offline mark-paid historically diverged (commerce gap doc).  
- Fees on Billplz are zero by construction at the webhook boundary. Matching a Billplz payout (which **does** deduct the real FPX/card fee) will never equal our “Net Cash Settled.”  
- Stripe expand can be right, or silently zero.  
- No import of gateway payout objects. No “unmatched ledger rows” report.

HitPay’s Sales Summary + Payouts is the MY merchant’s reconciliation ritual. Stripe’s Balance Summary is the global one. We offer neither.

---

## Metrics honesty

This section is the point of the chapter. Every number the console shows, and every number a competitor has trained the merchant to ask for, with **what we compute**, **what we display**, and **whether a grown-up can take it to a board or a bank**.

### MRR

| | |
|--|--|
| **Computed?** | Yes. `CommerceQueryService.GetStatsAsync` → `CommerceStatsDto.mrr`. |
| **Shown?** | **No.** Dashboard does not bind it. |
| **Definition** | Sum of current catalog `Products.Price` for subs in `ACTIVE` or `PAST_DUE`, annual ÷ 12, everything else treated as monthly. |
| **Includes PAST_DUE?** | Yes (Stripe Billing and ChartMogul also include past-due in stock MRR). |
| **Includes SUSPENDED?** | No. |
| **Includes PENDING?** | No (filtered in SQL). |
| **Discounts / coupons** | Ignored. Price is catalog, not invoiced. Stripe lets you subtract discounts; we cannot. |
| **Tax** | Not in MRR (good; Stripe excludes tax too). |
| **Fees** | Not in MRR (ChartMogul makes this a setting; we have no setting). |
| **One-time products** | If a leftover non-pending `one_time` row exists, its full price is added as if monthly. |
| **Weekly / quarter** | Treated as monthly → **overstated**. |
| **Movements** | None. No New / Expansion / Contraction / Churn / Reactivation. A single stock float. |
| **ARR** | Not computed. Anyone who wants ARR will mentally ×12 an already-wrong MRR. |
| **Currency** | Implicit MYR. No FX. Multi-currency would silently add numbers. |
| **Honesty verdict** | **Partial, and hidden.** Definition is a reasonable v0 if documented (active+past-due, catalog price, yr/12). It is not documented, not configurable, not on the home screen, and not a waterfall. Do not put “MRR” on a marketing site until it is visible and footnoted. |

### ARR

Not a field. Not a chart. Chargebee / ChartMogul treat ARR as a toggle or a first-class report. **Absence is honest.** Do not derive it in the client as `mrr * 12` without the MRR caveats.

### Churn (“Cancellation Rate”)

| | |
|--|--|
| **Shown?** | Yes, card 4, label **Cancellation Rate**, value `churn_rate_percentage`. |
| **Definition** | Logo-style: canceled-in-30d / reconstructed-active-30d-ago. |
| **Revenue churn?** | No. A RM 15 and a RM 1,500 cancel count the same. |
| **Voluntary vs delinquent?** | No. Paddle Retain’s whole product is this split. |
| **When is a cancel counted?** | `Status == CANCELED` and `UpdatedAt` in the window. Not `CanceledAt`. Touching an old canceled row re-counts it. |
| **SUSPENDED** | Invisible to both numerator and the “active 30d ago” reconstruction (they are not in `activeSubs`). A suspend-heavy book understates the base and ignores involuntary loss. |
| **Reactivations** | A cancel+resubscribe in-window looks like −1 +1 in `net_new`; Paddle would call the second a reactivation. |
| **Configurable?** | No (Stripe: when is a subscriber active; we have no twin). |
| **Honesty verdict** | **Unsafe to show without a tooltip.** The label “Cancellation Rate” is closer to the truth than “churn,” but the 30-day reconstruction is a blog-post formula, not Chargebee. |

### LTV

Not computed. ARPU exists in the DTO (`mrr / active count`) and is **not shown**. Chargebee LTV = lifetime × ARPS. Paddle LTV = typical stay × typical spend. We have neither tenure nor a paid-lifetime table.

**Do not** invent `ARPU / churn_rate` in the client. With a broken churn denominator that number is fan fiction.

### Failed payments

| Layer | Exists? | Visible? |
|-------|---------|----------|
| `GatewayPaymentFailedIntegrationEvent` | Yes | No list |
| `ChargeAttemptLog` FAILED | Yes | No list |
| `TransactionLogs` FAILED | **No** | — |
| Dashboard KPI | No | — |
| Subscriber inbox of failures | No | PAST_DUE card only |

A failed Billplz/Stripe charge that never flips the sub to `PAST_DUE` (the dunning-entry gap) is **invisible** in every merchant screen. Stripe Collections and HitPay’s failed status are table-stakes next to this.

### Recovered

| Layer | Exists? | Visible? |
|-------|---------|----------|
| `DunningCampaign.RecoveredRevenue` / `SavedSubscriptions` | Yes | Campaign table only |
| Monthly recovered MRR / at-risk MRR | No | — |
| Dashboard | No | — |
| Attribution (which step, which channel) | No | — |
| Record-payment while PAST_DUE | Recovers the sub; **does not** call `RecordRecovery` | Campaign counters stay stale |

Paddle Retain’s dashboard is the bar: recovered vs at-risk vs past-due MRR, plus a baseline. We have three lifetime integers on a campaign row, increment-only, no identity, no month.

### Net cash after fees and tax

**What the merchant sees:** one card, “Net Cash in Bank,” `FinancialSummaryDto.net_revenue`, all-time (no dates passed).

**What that number is:** signed double-entry net of gross, contra-refunds, discounts, **posted** gateway fees, and **tax liability**. Currency forced MYR. Utility credit top-ups excluded from GMV (good). Software subscription expense excluded (good; tested).

**Why it is not cash in the bank:**

1. **Billplz fees post as 0** (`ProcessGatewayWebhookCommandHandler` hard-codes estimated % and fixed fee to 0). Net ≈ gross − tax. Real FPX/card fees still leave the merchant’s Billplz balance. We overstate cash.  
2. **Stripe fees** are real only when expand works; otherwise 0. Same overstatement.  
3. **Tax** is liability, not necessarily remitted. Subtracting it from “cash in bank” is conservative for profit and wrong for cash. `/net-profit` does the opposite (keeps tax in, subtracts commissions).  
4. **No payout timing.** Cash in bank is a settlement concept (HitPay T+1, Stripe payout). We have realization at webhook time.  
5. **Deferred vs recognized** are computed and unused. Annual prepay hits `REVENUE_GROSS` immediately. Recognition job is parked. An annual RM 1,200 looks like RM 1,200 cash profit this minute.  
6. **Two books.** Commerce `total_revenue_collected` (confirmed logs, refunds excluded not netted) sits next to billing net. They will diverge (refunds, offline fees=0, mark-paid gaps). The dashboard shows billing net beside a commerce bar chart. Mixed epistemology on one page.  
7. **Label.** “Net Cash in Bank” is the most dishonest string in the console. Accurate labels: “Accounting net (fees + tax, all-time)” or “Estimated take-home (fees incomplete).”

Transaction detail “Net Cash Settled” is `amount - fee_amount` on **that row**. For gateway rows with fee 0, it is a lie in a smaller font. For offline rows, fee is always 0 by design (correct for cash/bank transfer if we do not charge a platform fee).

### Cohort

Zero implementation. No signup-month table, no revenue retention, no logo retention. Stripe, Paddle, Chargebee, ChartMogul, Baremetrics all ship this. We should not fake a heatmap from `CreatedAt` in the client without a backend definition.

### Geographic

`BillingAddress.CountryCode` defaults `MYS`. Profiles can store city/state. **No query groups by country or state.** Dashboard has no map, no “by state” table. HitPay at least splits domestic vs international cards on the export. We do not persist card BIN country.

For a MY-first CaaS this is **Later**, not a scandal — unless we start selling regional plans and still cannot say “Johor vs Selangor.”

### Export CSV

| Object | Export | Quality vs HitPay / Stripe |
|--------|--------|----------------------------|
| Subscribers | Yes, 10k cap, fixed columns | Missing LTV, geo, lifetime paid, dunning, MRR |
| Transactions | **No** | This is the export merchants need weekly |
| Ledger | **No** | Finance cannot close books |
| Stats / MRR roll-forward | **No** | Stripe’s three billing CSVs |
| Dunning | **No** | — |
| Webhook logs | **No** | — |
| Credits | **No** | — |
| Customers (CRM) | **No** (no list) | — |

Subscriber export is a real endpoint with BOM and quoting. That is more than a stub. It is not a reporting suite.

### Reconciliation against gateway

**Not a product.** A diligent merchant today: open Transaction Logs, copy `external_reference`, paste into Billplz/Stripe search, hope fees match (they will not on Billplz). No payout batch id, no unsettled bucket, no “ledger row without gateway id.”

Billing README’s golden rule (“never calculate net cash from Commerce logs”) is **violated by the dashboard chart**, which is commerce logs, sitting next to a billing-net card.

### Role-based access

**Honest description:** one human role that can do everything money-shaped (`ADMIN`), one that can do nothing in ops (`CLIENT`), one platform god (`SUPER_ADMIN`) who may 403 on tenant routes without a membership row. No staff. No view-only finance. No cashier. Refund = rotate keys = export = cancel subs.

Invite API is an unshipped control plane. Prompt-library “Invite Staff” is a fossil of the hidden agent.

### Multi-workspace

**Honest description:** a working switcher and create-workspace flow for true multi-brand merchants. Isolation is tenant-id on every admin query (good). No consolidated analytics. Superadmin UX over-promises. `CLIENT` entitlements are a footgun if a buyer account can log into ops.

### Audit log

**Honest description:** we do not have one. Webhook delivery logs are operator-grade for **outbound HTTP**, not for humans. `RecordedByName` is a display string.

### CRM depth

**Honest description:** a PII sidecar. Checkout resolve + list join. Consent field exists; no UI. Address exists; no UI. Anonymize exists; no merchant privacy console. ChartMogul putting CRM on every plan is a different company. We should stay thin **on purpose**, but then the subscriber directory must become a **customer** directory (one human, many subs) or we stop saying CRM in sales conversations.

### Cross-cutting honesty bugs on the current dashboard

1. **MRR computed, hidden; “Cancellation Rate” shown; “Net Cash in Bank” mislabeled.** The three most important SaaS/cash words are the three we handle worst.  
2. **Payment methods = recorded-by.** A bar footer that says `SYSTEM` is not a rail mix.  
3. **Method filter and `tx.payment_method`** do not exist on the wire.  
4. **Refunds:** commerce total ignores refunded rows; billing nets contra. Chart vs card disagree after the first refund.  
5. **Dates:** billing summary can time-bound; UI never sends dates. Stats cannot time-bound.  
6. **In-memory subscriber stats/list** will not survive a serious book (thousands of subs × unbounded transaction logs).  
7. **ADR 023** correctly hid tax-invoice theater. It also hid the only screens that could have explained tax on the net-cash card (billing profile, invoices). The card still subtracts tax.

---

## Gap table

Legend: **Y** = job exists and is used. **P** = partial, workaround, or honesty gap. **N** = not a product job. **—** = not applicable to that stack.  
**Us** = Lazuar Console as of 2026-08-16.  
**V** = suggested verdict for Pay (Ours / Theirs / Both / Partial / Later / Never).  
**W** = suggested wave once the Pay tracker exists (0 = correctness of numbers we already show).  
**Src** = this file unless noted.

| ID | Job | Us | Stripe | Paddle | Chargebee | CM/Bare | HitPay | V | W | Why |
|----|-----|:--:|:------:|:------:|:---------:|:-------:|:------:|---|--:|-----|
| MD-001 | Home dashboard with named KPIs | P | Y | Y | Y | Y | Y | Partial | 0 | We have a home; labels and missing MRR fail the job |
| MD-002 | MRR stock, documented | P | Y | Y | Y | Y | — | Partial | 0 | Computed, hidden, catalog-price, yr/12 only |
| MD-003 | ARR | N | P | P | Y | Y | — | Later | 6 | ×12 after MD-002 is honest |
| MD-004 | MRR movements / waterfall | N | P | Y | Y | Y | — | Later | 6 | Do not invent without event log |
| MD-005 | Churn % with written definition | P | Y | Y | Y | Y | — | Partial | 0 | Relabel + `CanceledAt` + exclude touch-ups |
| MD-006 | Revenue churn vs logo churn | N | Y | Y | Y | Y | — | Later | 6 | Needs movements |
| MD-007 | Voluntary vs delinquent churn | N | P | Y | Y | P | — | Later | 4 | After failed-payment inbox |
| MD-008 | LTV | N | Y | Y | Y | Y | — | Later | 7 | After tenure + honest ARPU |
| MD-009 | ARPU / ARPA shown | P | Y | Y | Y | Y | — | Partial | 1 | DTO exists; show with footnote |
| MD-010 | Failed payments list | N | Y | Y | Y | P | Y | Both | 1 | `ChargeAttemptLog` is unused inventory |
| MD-011 | Recovered revenue (monthly) | P | P | Y | Y | P | — | Partial | 2 | Campaign lifetime counters only |
| MD-012 | Recovery rate vs at-risk | N | N | Y | P | P | — | Later | 5 | Copy Paddle’s *shape*, not their take-rate |
| MD-013 | Net after **actual** fees | P | Y | Y | P | P | Y | Partial | 0 | Billplz fee=0 is a finance bug |
| MD-014 | Tax separated from cash | P | Y | Y | Y | P | P | Partial | 1 | Ledger has tax; UI subtracts it from “bank” |
| MD-015 | Honest cash / payout view | N | Y | Y | P | N | Y | Theirs | 3 | BYOK: explain gateway payouts, don’t invent a bank |
| MD-016 | Date range on dashboard | N | Y | Y | Y | Y | Y | Both | 1 | Summary already accepts dates |
| MD-017 | Cohort retention | N | Y | Y | Y | Y | N | Later | 7 | Specialist-grade; don’t fake |
| MD-018 | Geographic slice | N | P | P | P | Y | P | Later | 8 | MY-first; address exists unused |
| MD-019 | Product / plan breakdown of revenue | N | Y | Y | Y | Y | Y | Both | 2 | Catalog table has no money |
| MD-020 | Payment-method mix (real rails) | P | Y | P | Y | P | Y | Partial | 1 | Grouping by `RecordedByName` is wrong |
| MD-021 | Transaction CSV | N | Y | Y | Y | Y | Y | Both | 1 | Highest-leverage export |
| MD-022 | Subscriber CSV (richer) | P | Y | P | Y | Y | P | Partial | 2 | Exists; thin columns |
| MD-023 | Ledger / journal CSV | N | Y | P | Y | P | P | Later | 3 | After ledger UI |
| MD-024 | MRR roll-forward CSV | N | Y | P | Y | Y | — | Later | 6 | Stripe’s three reports are the template |
| MD-025 | Gateway payout reconciliation | N | Y | Y | P | N | Y | Later | 3 | Match `external_reference` to provider payout CSV |
| MD-026 | Dual-book tie-out (commerce log vs ledger) | N | — | — | — | — | — | Ours | 2 | Only we have this split; we must show it or kill one book in the UI |
| MD-027 | Refunds netted consistently | P | Y | Y | Y | P | Y | Partial | 0 | Chart vs card diverge |
| MD-028 | Owner vs staff roles | N | Y | P | Y | P | Y | Both | 2 | Refund ≠ rotate keys |
| MD-029 | View-only / analyst role | N | Y | P | Y | P | P | Later | 4 | After MD-028 |
| MD-030 | Team members UI | N | Y | Y | Y | Y | Y | Both | 2 | API exists |
| MD-031 | Multi-workspace switcher | Y | Y | P | Y | P | Y | Both | — | Shipped; keep |
| MD-032 | Multi-workspace roll-up | N | Y | N | Y | P | Y | Later | 8 | Isolation first |
| MD-033 | Superadmin tenant access without membership hole | P | — | — | — | — | — | Ours | 0 | Switcher lists orgs that 403 |
| MD-034 | Audit log of staff money actions | N | Y | P | Y | N | P | Both | 2 | Refund, cancel, record-payment, key change |
| MD-035 | Webhook delivery log | Y | Y | P | Y | — | P | Both | — | Shipped; not an audit log |
| MD-036 | Customer directory (one human) | N | Y | Y | Y | Y | P | Partial | 2 | Subscribers ≠ customers |
| MD-037 | Customer notes / tags / timeline | N | P | P | Y | Y | N | Later | 8 | Stay thin; don’t chase ChartMogul CRM |
| MD-038 | Consent + PDPA anonymize in console | P | P | P | Y | P | N | Partial | 5 | Commands exist; no merchant UX |
| MD-039 | Merge duplicate profiles | N | P | P | Y | Y | N | Later | 8 | Email-only resolve already hides dupes |
| MD-040 | Chargebee/ChartMogul-grade report gallery | N | P | P | Y | Y | N | Never | — | Trap. Export + 8 honest KPIs beat 150 reports |
| MD-041 | Sigma / warehouse SQL | N | Y | N | P | Y | N | Never | — | Trap for a CaaS MVP |
| MD-042 | Marketplace / take-rate analytics | N | P | — | — | — | N | Never | — | Not our company |
| MD-043 | Platform credit wallet UI | P | — | — | — | — | — | Ours | 4 | Exists, unlinked; rename so it is not “the ledger” |
| MD-044 | Double-entry ledger UI | N | P | N | P | N | N | Later | 3 | Dark matter today; finance differentiator later |
| MD-045 | Net-profit time series UI | N | P | Y | Y | Y | P | Later | 3 | Endpoint exists |
| MD-046 | Configurable metric definitions | N | Y | P | P | Y | N | Later | 7 | After we publish a glossary |
| MD-047 | Drill-down from KPI to rows | N | Y | P | Y | Y | Y | Both | 2 | Past due card should open filtered subscribers |
| MD-048 | Failed-payment recovery on dashboard | N | P | Y | Y | P | N | Partial | 2 | We sell WhatsApp dunning; home is silent |
| MD-049 | Transaction method filter that works | P | Y | Y | Y | Y | Y | Partial | 0 | Spec + UI vs ignored SQL |
| MD-050 | Status filter server-side on subscribers | P | Y | Y | Y | Y | P | Partial | 1 | Client filter on one page is wrong |
| MD-051 | Hide or fill zeroed / unused DTO fields | P | Y | Y | Y | Y | Y | Ours | 0 | Show MRR or stop computing it in secret |
| MD-052 | Glossary in-product (what MRR includes) | N | Y | Y | Y | Y | N | Both | 1 | Stripe’s “invoices ≠ MRR” FAQ is the model |

**Traps (Never):** MD-040, MD-041, MD-042. Building RevenueStory or Sigma is how a two-person CaaS dies. Building payout-as-acquirer (copying HitPay’s bank rail) is a license change, not a dashboard ticket.

**Ours (keep / finish):** MD-026 (only we have two books — make that a feature or a single pane), MD-031, MD-035, MD-043 (wallet is real), MD-033 (fix the hole we dug), MD-051.

---

## Tracker IDs

Stable IDs for the Pay competitor-feature tracker. **Prefix `MD`** = merchant dashboard / analytics / CRM (this chapter). Do not reuse Aura `AN-*` (salon EOD / mix). Do not treat a row as a commit to ship.

### Status vocabulary (same spirit as `20-sequencing-and-tracker-schema.md`)

| Mark | Meaning |
|------|---------|
| **Y** | Job exists and is sold / used |
| **P** | Partial, workaround, or honesty gap |
| **N** | Not a product job |
| **—** | Not applicable |
| **X** | Never / trap |

**Us depth:** `shipped` · `partial` · `hidden` (backend only) · `orphan` (routed, unlinked) · `stub` · `none` · `n/a`

**V** = Ours · Theirs · Both · Partial · Later · Never · N/A  
**W** = wave. `—` = already done enough.  
**Prio** = 0 (now in that wave) … 3.

### Wave intent (Pay console only)

| Wave | Intent |
|------|--------|
| **0** | Stop lying. Relabel, fee=0, filter bugs, superadmin 403, show or hide MRR. |
| **1** | Cash-register completeness. Transaction CSV, date range, real method mix, failed-payment list, ARPU card. |
| **2** | Recovery + people. Dashboard recovered, customer directory, team UI, audit of money actions, KPI drill-down. |
| **3** | Finance pane. Ledger or net-profit UI, payout-matching guide/CSV, dual-book tie-out. |
| **4+** | Roles refinement, monthly recovery rate, LTV/cohort, geo, glossary configurability. |
| **Never** | Sigma, 150-report gallery, marketplace GMV analytics, becoming the acquirer. |

### Seed catalog

| ID | Feature | Us depth | V | W | Prio | Class | Notes |
|----|---------|----------|---|--:|:----:|-------|-------|
| MD-001 | Honest home KPIs | partial | Partial | 0 | 0 | table-stakes | Relabel “Net Cash”; show MRR or drop the field |
| MD-002 | Documented MRR | hidden | Partial | 0 | 0 | table-stakes | Publish definition next to the number |
| MD-003 | ARR | none | Later | 6 | 3 | later-nice | After MD-002 |
| MD-004 | MRR waterfall | none | Later | 6 | 3 | later-nice | Needs movement events |
| MD-005 | Honest churn card | partial | Partial | 0 | 0 | table-stakes | `CanceledAt`; tooltip |
| MD-006 | Revenue churn | none | Later | 6 | 2 | later-nice | |
| MD-007 | Delinquent vs voluntary | none | Later | 4 | 2 | differentiator | Pairs with dunning story |
| MD-008 | LTV | none | Later | 7 | 3 | later-nice | Do not `ARPU/churn` in JS |
| MD-009 | Show ARPU | hidden | Partial | 1 | 2 | hygiene | Already in DTO |
| MD-010 | Failed payments inbox | none | Both | 1 | 0 | table-stakes | Project `ChargeAttemptLog` |
| MD-011 | Monthly recovered | partial | Partial | 2 | 0 | differentiator | We sell recovery; home is silent |
| MD-012 | Recovery rate + at-risk | none | Later | 5 | 2 | differentiator | Shape from Paddle Retain docs |
| MD-013 | Actual gateway fees | partial | Partial | 0 | 0 | table-stakes | Stop passing 0,0 into ParseWebhook |
| MD-014 | Tax vs cash labeling | partial | Partial | 1 | 1 | hygiene | Don’t call tax “left the bank” |
| MD-015 | Payout / settlement view | none | Theirs | 3 | 1 | table-stakes | Guide + import; we are not the bank |
| MD-016 | Dashboard date range | none | Both | 1 | 1 | table-stakes | Wire existing summary query params |
| MD-017 | Cohorts | none | Later | 7 | 3 | later-nice | Or export for ChartMogul |
| MD-018 | Geo slice | none | Later | 8 | 3 | later-nice | Address already on profile |
| MD-019 | Revenue by product | none | Both | 2 | 1 | table-stakes | Replace dead catalog widget |
| MD-020 | Real payment-method mix | partial | Partial | 1 | 0 | table-stakes | Persist rail, not recorded-by |
| MD-021 | Transactions CSV | none | Both | 1 | 0 | table-stakes | HitPay column set is the template |
| MD-022 | Richer subscribers CSV | partial | Partial | 2 | 2 | hygiene | |
| MD-023 | Ledger CSV | none | Later | 3 | 2 | later-nice | |
| MD-024 | MRR roll-forward CSV | none | Later | 6 | 3 | later-nice | Stripe’s three reports |
| MD-025 | Reconcile to gateway payout | none | Later | 3 | 1 | table-stakes | `external_reference` is the join key |
| MD-026 | Commerce log vs billing ledger tie-out | none | Ours | 2 | 1 | differentiator | Only we have two books |
| MD-027 | Refunds netted one way | partial | Partial | 0 | 0 | hygiene | Chart vs card |
| MD-028 | Owner vs staff (refund vs settings) | none | Both | 2 | 0 | table-stakes | HitPay Owner/Admin/Cashier |
| MD-029 | Analyst / view-only | none | Later | 4 | 2 | later-nice | Stripe Analyst |
| MD-030 | Team members + invites UI | none | Both | 2 | 0 | table-stakes | Endpoints exist |
| MD-031 | Workspace switcher + create | shipped | Both | — | — | table-stakes | Keep |
| MD-032 | Cross-workspace roll-up | none | Later | 8 | 3 | later-nice | |
| MD-033 | Superadmin membership / 403 | partial | Ours | 0 | 0 | hygiene | Entitlements lie |
| MD-034 | Staff audit log (money) | none | Both | 2 | 0 | table-stakes | Refund, cancel, keys |
| MD-035 | Webhook delivery logs | shipped | Both | — | — | hygiene | Keep; don’t call it audit |
| MD-036 | Customer directory | none | Partial | 2 | 1 | table-stakes | Group subs by `ClientProfileId` |
| MD-037 | Notes / tags / timeline | none | Later | 8 | 3 | later-nice | Thin CRM stays thin |
| MD-038 | Consent + anonymize UX | partial | Partial | 5 | 2 | hygiene | PDPA |
| MD-039 | Profile merge | none | Later | 8 | 3 | later-nice | |
| MD-040 | 150-report gallery | none | Never | — | — | trap | Chargebee RevenueStory clone |
| MD-041 | In-dashboard SQL / Sigma | none | Never | — | — | trap | |
| MD-042 | Take-rate / marketplace analytics | none | Never | — | — | trap | |
| MD-043 | Credit wallet discoverability | orphan | Ours | 4 | 2 | hygiene | Rename; link or hide |
| MD-044 | Double-entry ledger screen | hidden | Later | 3 | 2 | differentiator | Backend ready |
| MD-045 | Net-profit series screen | hidden | Later | 3 | 2 | later-nice | `GET /admin/billing/net-profit` |
| MD-046 | Configurable MRR rules | none | Later | 7 | 3 | later-nice | Stripe Configure |
| MD-047 | KPI → filtered list | none | Both | 2 | 1 | table-stakes | Past due card → subscribers |
| MD-048 | Recovery KPI on home | none | Partial | 2 | 0 | differentiator | We market dunning |
| MD-049 | Transaction `payment_method` honesty | partial | Partial | 0 | 0 | hygiene | Spec/UI/SQL/DTO four-way drift |
| MD-050 | Server-side subscriber status | partial | Partial | 1 | 1 | hygiene | |
| MD-051 | Don’t compute secret KPIs | partial | Ours | 0 | 1 | hygiene | Render or delete |
| MD-052 | In-product metrics glossary | none | Both | 1 | 1 | hygiene | “This is not cash” |

### Suggested implement-later queue (open this first)

Rows that are not Never and not already shipped, sorted by wave then prio:

1. **MD-013 / MD-001 / MD-014 / MD-027** — fee zero, “Net Cash in Bank,” tax vs cash, refund chart.  
2. **MD-002 / MD-051 / MD-005** — put MRR on the home with a footnote, or stop calculating it; fix churn label + `CanceledAt`.  
3. **MD-049 / MD-033** — method filter/DTO; superadmin 403.  
4. **MD-021 / MD-010 / MD-016 / MD-020 / MD-009** — transaction CSV, failed inbox, dates, real rails, ARPU.  
5. **MD-048 / MD-011 / MD-047 / MD-036** — recovery on home, monthly recovered, drill-down, customer directory.  
6. **MD-030 / MD-028 / MD-034** — team UI, staff vs owner, audit.  
7. **MD-026 / MD-025 / MD-044 / MD-045** — tie-out, payout matching, ledger/net-profit panes.  
8. Everything wave 4+ only after the home screen can be read aloud to an accountant without apologizing.

### Evidence pointers (do not implement from memory)

| Concern | Path |
|---------|------|
| Dashboard UI | `/Users/akmalfirdaus/Code/lazuar/lazuar-pay/apps/lazuar-ops/src/modules/commerce/pages/DashboardPage.tsx` |
| Sidebar IA | `.../apps/lazuar-ops/src/components/Sidebar.tsx` |
| Routes / hidden islands | `.../apps/lazuar-ops/src/App.tsx` |
| Stats SQL | `.../Modules/Commerce/Infrastructure/Services/CommerceQueryService.Stats.cs` |
| Tx list SQL | `.../CommerceQueryService.Transactions.cs` |
| Subscriber list + in-memory page | `.../CommerceQueryService.Subscribers.cs` |
| Subscriber CSV | `.../Modules/Commerce/Infrastructure/Endpoints/SubscriberEndpoints.cs` |
| Stats DTO | `.../packages/api-spec/modules/commerce/models/stats.tsp` |
| Tx DTO | `.../packages/api-spec/modules/commerce/models/subscriber.tsp` |
| Billing summary / net-profit | `.../Modules/Billing/Infrastructure/Services/BillingQueryService.cs` |
| Billing routes | `.../packages/api-spec/modules/billing/routes.tsp` |
| Sale journal | `.../Modules/Billing/Infrastructure/EventHandlers/GatewayPaymentCompletedHandler.cs` |
| Fee args = 0 | `.../Modules/Payments/Application/Commands/ProcessGatewayWebhookCommandHandler.cs` |
| Billplz fee formula | `.../Modules/Payments/Infrastructure/Gateways/BillplzGatewayAdapter.cs` |
| Recovery counters | `.../Modules/Commerce/Domain/Aggregates/DunningCampaign.cs` |
| Recovery write | `.../GatewayPaymentCompletedIntegrationEventHandler.Subscription.cs` |
| Charge attempts | `.../Modules/Commerce/Domain/Entities/ChargeAttemptLog.cs` |
| CRM entity | `.../Modules/CRM/Domain/ClientProfileEntity.cs` |
| CRM no HTTP | `.../packages/api-spec/modules/crm/models.tsp` |
| OrgAdmin policy | `.../src/Lazuar.Api/Composition/AuthAndCorsExtensions.cs` |
| Role inject / 403 | `.../src/Lazuar.Api/Middleware/TenantSecurityMiddleware.cs` |
| Cookie role | `.../Modules/One/Infrastructure/Endpoints/AuthEndpoints.cs` |
| Members API | `.../Modules/One/Infrastructure/Endpoints/WorkspaceEndpoints.cs` |
| Invite ADMIN-only | `.../Modules/One/Application/Commands/InviteUserToWorkspaceCommand.cs` |
| ADR 023 lobotomy | `.../docs/architecture-decision-log/023-pure-caas-mvp-ui-lobotomy.md` |
| Billing golden rule | `.../Modules/Billing/README.md` |
| CRM intent | `.../Modules/CRM/README.md` |

### Competitor doc pointers (fetched 2026-08-16)

| Stack | URL |
|-------|-----|
| Stripe Billing Analytics | https://docs.stripe.com/billing/subscriptions/analytics |
| Stripe roles | https://docs.stripe.com/get-started/account/teams/roles |
| Stripe payout recon | https://docs.stripe.com/connect/supported-embedded-components/payout-reconciliation-report |
| Paddle ProfitWell Metrics | https://developer.paddle.com/concepts/retain/metrics |
| Paddle Retain recovery | https://www.paddle.com/help/profitwell-metrics/retain/how-it-works/retain-recovery-rate |
| Chargebee RevenueStory | https://www.chargebee.com/docs/billing/2.0/reports-and-analytics/chargebee-analytics |
| Chargebee users & roles | https://www.chargebee.com/docs/billing/2.0/site-configuration/account_settings |
| ChartMogul MRR | https://help.chartmogul.com/article/154-chart-monthly-recurring-revenue-mrr |
| HitPay reports | https://docs.hitpayapp.com/reporting/payments |
| HitPay roles | https://docs.hitpayapp.com/setup/user-management-roles |

---

*End of 17 — Merchant dashboard, analytics, CRM. Do not condense this file. Promote tracker rows only after the sibling Pay chapters exist.*
