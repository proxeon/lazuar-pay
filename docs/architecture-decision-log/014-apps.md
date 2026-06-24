
# Lazuar Platform — Module Reference Documentation

> **Purpose:** Comprehensive reference for each module in the Lazuar superapp. Use this as the single source of truth for scope, features, and strategic role.

---

## Architecture Taxonomy

```
┌─────────────────────────────────────────────────────┐
│                    OPS-PAGE SUPERAPP                 │
│         (Deterministic UI + Data Tables)             │
├──────────────┬──────────────┬───────────────────────┤
│ ACQUISITION  │ FULFILLMENT  │      RETENTION        │
│  (Lead Gen)  │  (Delivery)  │  (Nurture & Compound) │
└──────────────┴──────────────┴───────────────────────┘
```

**Module Pattern:** Every module follows `Dashboard → List → Detail → Settings`

---

## Core Infrastructure Modules

These are non-revenue modules that enable the 15 business apps.

<details>
<summary><strong>One Module (CIAM & Workspaces)</strong></summary>

### `One` — Identity, Authentication, Workspaces

**Inspiration:** AWS IAM + Stripe Account

**Purpose:** The foundational identity layer. Manages global users, tenant workspaces, memberships, invitations, and app entitlements.

**Core Entities:**
- `GlobalUser` — Single identity across all Lazuar apps
- `Organization` — Tenant/workspace boundary
- `TenantMembership` — User-to-workspace mapping
- `WorkspaceInvitation` — Pending invites
- `TenantAppEntitlement` — Which apps a tenant can access

**Key Features:**
- Email/password + magic link authentication
- Workspace provisioning and slug-based routing
- App entitlement toggles (enable/disable modules per tenant)
- Email verification and password reset flows
- Member roles (Owner, Admin, Member)

**Integration Points:**
- Emits `TenantProvisionedIntegrationEvent` → triggers seeding in all modules
- Emits `AppEntitlementGrantedIntegrationEvent` → unlocks module access
- Emits `GlobalUserProfileUpdatedIntegrationEvent` → syncs CRM

**Strategic Role:** Enabler. Multi-tenant isolation lives here.

</details>

<details>
<summary><strong>Payments Module (Gateway Adapters)</strong></summary>

### `Payments` — Payment Gateway Abstraction

**Inspiration:** Stripe + Billplz + ChipCollect + Razorpay

**Purpose:** Unified adapter layer for multiple regional payment gateways. Isolates payment logic from business modules.

**Core Entities:**
- `TenantPaymentConfiguration` — Per-tenant gateway credentials
- `PaymentWebhookLog` — Idempotent webhook processing log

**Supported Gateways:**
| Gateway | Region | Use Case |
|---|---|---|
| Stripe | Global | International cards, subscriptions |
| Billplz | Malaysia | FPX bank transfers |
| ChipCollect | Malaysia | Recurring FPX |
| Razorpay | India | Regional expansion |

**Key Features:**
- Checkout session generation
- Customer portal link generation
- Webhook ingestion and idempotent processing
- Refund initiation and tracking
- Off-session charge execution

**Integration Points:**
- Emits `GatewayPaymentCompletedIntegrationEvent` → Billing, Community
- Emits `GatewayPaymentFailedIntegrationEvent` → Community (dunning)
- Emits `GatewayRefundCompletedIntegrationEvent` → Billing, LHDN

**Strategic Role:** Infrastructure. Never build a gateway — wrap existing ones.

</details>

<details>
<summary><strong>Billing Module (Double-Entry Ledger)</strong></summary>

### `Billing` — Financial Ledger & Revenue Recognition

**Inspiration:** Stripe Billing + QuickBooks

**Purpose:** Source of truth for all financial transactions. Double-entry ledger ensures audit-grade integrity.

**Core Entities:**
- `LedgerEntry` — Immutable financial record
- `LedgerLine` — Individual debit/credit line
- `CreditLedger` — Tenant credit balance
- `DeferredRevenueSchedule` — Revenue recognition over time
- `TenantCreditBalance` — Prepaid credits

**Key Features:**
- Double-entry bookkeeping (every transaction balanced)
- Invoice issuance and consolidation (B2C monthly consolidation)
- Deferred revenue recognition (subscriptions recognized over period)
- Commission accrual for affiliates
- Credit balance management (wallet system)
- Manual payment recording (bank transfers, cash)

**Integration Points:**
- Consumes `GatewayPaymentCompletedIntegrationEvent` → records revenue
- Consumes `GatewayRefundCompletedIntegrationEvent` → reverses revenue
- Emits `InvoiceIssuedIntegrationEvent` → LHDN (tax submission)
- Emits `ConsolidatedInvoiceIssuedIntegrationEvent` → LHDN

**Strategic Role:** Financial integrity. The deterministic core.

</details>

<details>
<summary><strong>CRM Module (Client Profiles)</strong></summary>

### `CRM` — Customer Profile Management

**Inspiration:** HubSpot Lite

**Purpose:** Unified customer profiles across all apps. Single view of the customer.

**Core Entities:**
- `ClientProfileEntity` — Customer record
- `BillingAddress` — Address for invoicing/shipping

**Key Features:**
- Auto-creation on first interaction (checkout, form, giveaway entry)
- Profile consolidation (same email = same profile)
- GDPR-compliant anonymization
- Address collection (critical for Giveaway module)
- Tagging and segmentation

**Integration Points:**
- Consumes `GlobalUserProfileUpdatedIntegrationEvent` → syncs name/email
- Emits `ClientProfileAnonymizedIntegrationEvent` → all modules purge PII

**Strategic Role:** Data foundation. Every module references CRM profiles.

</details>

<details>
<summary><strong>Messaging Module (Notifications)</strong></summary>

### `Messaging` — Notification Dispatch

**Inspiration:** Resend + AWS SES

**Purpose:** Centralized notification system for transactional and broadcast messaging.

**Core Entities:**
- `TenantReplica` — Per-tenant messaging configuration

**Key Features:**
- Transactional email dispatch (receipts, magic links, reminders)
- Template rendering with tenant-specific branding
- Multi-channel routing (email, SMS, in-app)
- Tenant provisioning on workspace creation

**Integration Points:**
- Consumes `DispatchMessageIntegrationEvent` → sends notifications
- Consumes `TenantProvisionedIntegrationEvent` → seeds messaging config
- Consumes `WorkspaceUpdatedIntegrationEvent` → updates branding

**Strategic Role:** Communication backbone.

</details>

<details>
<summary><strong>LHDN Module (Malaysia e-Invoicing)</strong></summary>

### `LHDN` — Malaysia e-Invoice Compliance

**Inspiration:** Storehub + LHDN MyInvois API

**Purpose:** Automated submission of tax documents to Malaysia's LHDN gateway. Required for businesses operating in Malaysia.

**Core Entities:**
- `TaxDocument` — Invoice/credit note record
- `LhdnTenantConfig` — Per-tenant certificate and credentials
- `DeveloperApiKey` — API key for LHDN gateway
- `WebhookSubscription` — LHDN webhook registration

**Supported Document Types:**
- Standard Invoice
- Consolidated Invoice (B2C monthly)
- Credit Note
- Self-Billed Invoice
- Self-Billed Credit Note

**Key Features:**
- UBL XML document generation via Scriban templates
- XSD validation before submission
- Certificate-based authentication
- Taxpayer TIN validation
- Webhook status polling and reconciliation
- Document cancellation within legal window

**Integration Points:**
- Consumes `InvoiceIssuedIntegrationEvent` → submits to LHDN
- Consumes `ConsolidatedInvoiceIssuedIntegrationEvent` → submits monthly batch
- Consumes `GatewayRefundCompletedIntegrationEvent` → submits credit note
- Emits `LhdnDocumentValidatedIntegrationEvent` → Billing
- Emits `LhdnDocumentCancelledIntegrationEvent` → Billing

**Strategic Role:** Legal compliance for Malaysian operations. May spin off as separate product.

</details>

<details>
<summary><strong>Ops Module (AI Agent — Hibernating)</strong></summary>

### `Ops` — AI Agent Orchestration (Hibernating)

**Inspiration:** Cursor + Linear AI

**Purpose:** Probabilistic AI assistant layer over deterministic APIs. Currently hibernating in favor of deterministic console.

**Core Entities:**
- `OpsConversation` — Chat session
- `OpsMessage` — Individual message in conversation

**Key Components:**
- `ToolRegistry` — Registry of deterministic tools the AI can call
- `LlmOrchestratorService` — Manages LLM interaction and tool execution
- `UiRequestCard` — Bridge between AI suggestion and human approval
- `FormRegistry` — Dynamic form rendering for AI-triggered actions

**Architecture:**
```
User Input → LLM → Tool Call Proposal → UiRequestCard → Human Approves → Deterministic API Execution
```

**Current Status:** Architecture complete, hibernating. Read-only queries (analytics, summaries) safe to activate. Mutation queries remain hibernated.

**Strategic Role:** Future differentiator. Reactivate for read-only analysis first.

</details>

---

## Acquisition Apps (Lead Generation)

<details>
<summary><strong>1. Bio App (Linktree Clone)</strong></summary>

### `Bio` — Link-in-Bio Hub

**Inspiration:** Linktree, Beacons, Bento

**Purpose:** Single link hub for all social profiles. Entry point for cold traffic.

**Core Features:**
- Custom slug routing (`lazuar.com/{creatorSlug}`)
- Link management (title, URL, icon, active/inactive)
- Click tracking and analytics
- Scheduling (links appear/disappear on schedule)
- Theme customization (colors, avatar, layout)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Total clicks, top links, traffic sources |
| List | Manage all links |
| Detail | Edit individual link |
| Settings | Page theme, slug, avatar |

**Revenue Model:** Indirect (drives traffic to other modules)

**Build Priority:** 11 (low effort, low differentiation)

</details>

<details>
<summary><strong>2. Form App (Typeform Clone)</strong></summary>

### `Form` — Lead Capture Forms

**Inspiration:** Typeform, Tally

**Purpose:** Collect structured lead data. Feed CRM with qualified prospects.

**Core Features:**
- Drag-and-drop form builder
- Field types: text, email, phone, dropdown, file upload, address
- Conditional logic (show field B if field A = X)
- Custom success message / redirect URL
- Webhook dispatch on submission
- Anti-spam (honeypot, rate limiting)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Submission count, conversion rate, completion time |
| List | Manage forms |
| Detail | View submissions, export CSV |
| Settings | Webhook config, notifications |

**Integration Points:**
- Auto-creates `ClientProfileEntity` on submission
- Triggers `Broadcast` welcome sequence

**Revenue Model:** Indirect (lead qualification)

**Build Priority:** 10

</details>

<details>
<summary><strong>3. Event App (Eventbrite Clone) — ✅ VALIDATED</strong></summary>

### `Event` — Paid Events & Bootcamps

**Inspiration:** Eventbrite, Luma

**Purpose:** Sell tickets to live events, bootcamps, workshops. **This is your validated revenue module.**

**Core Features:**
- Event creation (title, description, date, location, capacity)
- Ticket tiers (early bird, regular, VIP)
- Checkout flow with payment integration
- Attendee management (check-in, badges)
- Reminder emails (T-7, T-1, T-0)
- Post-event follow-up sequence
- Refund processing

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Revenue, tickets sold, upcoming events |
| List | Manage events |
| Detail | Attendee list, revenue breakdown, refund log |
| Settings | Payment config, reminder templates |

**Public Pages:**
- `/{eventSlug}` — Event landing page
- `/{eventSlug}/checkout` — Checkout flow
- `/{eventSlug}/success` — Confirmation

**Integration Points:**
- Creates `ClientProfileEntity` on purchase
- Emits payment events to Billing
- Triggers CRM tagging (`event-attendee`)
- Feeds Broadcast for post-event nurture

**Revenue Model:** Direct (ticket sales)

**Validation:** RM4,890 in 2 weeks (Docker bootcamp)

**Build Priority:** 1 ✅ COMPLETE

</details>

<details>
<summary><strong>4. Consult App (Calendly Clone)</strong></summary>

### `Consult` — Paid Consulting Booking

**Inspiration:** Calendly, SavvyCal

**Purpose:** Sell 1-on-1 consulting time. High-ticket, low-volume revenue stream.

**Core Features:**
- Availability calendar (recurring slots, blackout dates)
- Booking page with service tiers
- Payment-required booking (pre-pay to reserve)
- Timezone detection and conversion
- Automated reminders (T-24, T-1)
- Reschedule and cancellation logic
- Post-consult follow-up

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Upcoming sessions, monthly revenue |
| List | Bookings |
| Detail | Client info, notes, session recording link |
| Settings | Availability, services, pricing, reminder templates |

**Public Pages:**
- `/{consultSlug}` — Service menu
- `/{consultSlug}/checkout` — Booking + payment
- `/{consultSlug}/success` — Confirmation

**Integration Points:**
- Creates `ClientProfileEntity` on booking
- Syncs to Calendar (Google Calendar integration)
- Triggers Broadcast reminders

**Revenue Model:** Direct (high-ticket consulting)

**Build Priority:** 8

</details>

<details>
<summary><strong>5. Giveaway App (Viral Lead Gen) — NEW, HIGHEST PRIORITY</strong></summary>

### `Giveaway` — Viral Giveaway Engine

**Inspiration:** Kingsumo, Gleam, MrBeast organic model

**Purpose:** Generate qualified leads through viral giveaway mechanics. Solves the distribution bottleneck.

**Core Features:**
- Giveaway creation (title, prize, description, image, end date)
- Entry form (name, email, phone, shipping address — configurable)
- Winner selection (deterministic random algorithm)
- Referral tracking (unique links per entrant)
- Bonus entries for sharing (social media actions)
- Leaderboard ("You're #3 of 247 entries")
- Winner notification email
- Consolation discount code for non-winners

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Entries, viral coefficient, conversion to paid |
| List | Manage giveaways |
| Detail | Entrant list, referral tree, winner selection |
| Settings | Entry form fields, anti-fraud rules, terms template |

**Public Pages:**
- `/{giveawaySlug}` — Giveaway landing page
- `/{giveawaySlug}/enter` — Entry form
- `/{giveawaySlug}/refer/{referralCode}` — Referral tracking

**Integration Points:**
- Creates `ClientProfileEntity` on entry
- Auto-adds winner to `Vault` for digital fulfillment
- Auto-adds non-winners to `Broadcast` with consolation coupon
- Tags all entrants in CRM (`giveaway-{slug}`)

**Anti-Fraud:**
- IP rate limiting (max 3 entries per IP)
- Email verification required
- Phone verification (optional)
- Duplicate detection (email + phone + address)

**Prize Strategy Framework:**

$$
\text{Prize Score} = \frac{\text{Audience Overlap} \times \text{Perceived Value}}{\text{Actual Cost to You}}
$$

| Tier | Prize Example | Cost | Quality |
|---|---|---|---|
| 1 | Bootcamp seat | RM0 | 🟢🟢🟢 |
| 2 | Claude Pro 1yr | RM1,100 | 🟢🟢🟢 |
| 3 | AI API credits | RM450 | 🟢🟢🟢 |
| 4 | Tech tool JV bundle | RM0 | 🟢🟢🟢 |
| 5 | iPhone/Motorbike | RM5K+ | 🔴 AVOID |

**Revenue Model:** Indirect (lead generation → conversion via other modules)

**Build Priority:** 2 (build immediately after Event)

</details>

---

## Fulfillment Apps (Delivery)

<details>
<summary><strong>6. Vault App (Gumroad Clone)</strong></summary>

### `Vault` — Digital Product Delivery

**Inspiration:** Gumroad, Lemon Squeezy

**Purpose:** Sell and deliver digital products (PDFs, templates, code, videos).

**Core Features:**
- Product creation (title, description, price, cover image)
- File upload and secure storage (R2)
- Checkout flow with payment integration
- Download link generation (signed URLs, expiry)
- License key generation (for software)
- Version management (update file, notify buyers)
- Bundle creation (multiple products at discount)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Revenue, units sold, top products |
| List | Manage products |
| Detail | Buyers list, version history, download stats |
| Settings | Payment config, storage config, download limits |

**Public Pages:**
- `/{productSlug}` — Product landing page
- `/{productSlug}/checkout` — Checkout flow
- `/{productSlug}/success` — Download page

**Integration Points:**
- Creates `ClientProfileEntity` on purchase
- Emits payment events to Billing
- Feeds Broadcast for buyer nurture sequence
- Receives giveaway winners from Giveaway module

**Revenue Model:** Direct (digital product sales)

**Build Priority:** 3

</details>

<details>
<summary><strong>7. Academy App (Kajabi Clone)</strong></summary>

### `Academy` — Course Platform

**Inspiration:** Kajabi, Teachable, Maven

**Purpose:** Sell and deliver structured courses with modules, lessons, and progress tracking.

**Core Features:**
- Course creation (title, description, price, cover)
- Module and lesson management (video, text, attachments)
- Drip scheduling (unlock lessons on schedule)
- Progress tracking (completion percentage, last accessed)
- Quiz/assessment (optional)
- Certificate generation (on completion)
- Student discussion (per-lesson comments)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Enrollments, completion rates, revenue |
| List | Manage courses |
| Detail | Student list, lesson management, analytics |
| Settings | Payment config, drip rules, certificate template |

**Public Pages:**
- `/{courseSlug}` — Course landing page
- `/{courseSlug}/checkout` — Enrollment flow
- `/{courseSlug}/learn` — Student portal

**Integration Points:**
- Creates `ClientProfileEntity` on enrollment
- Emits payment events to Billing
- Triggers Broadcast for lesson reminders
- Integrates with Community (students get community access)

**Revenue Model:** Direct (course sales, highest ARPU)

**Build Priority:** 4

</details>

<details>
<summary><strong>8. Funnel App (ClickFunnels Clone)</strong></summary>

### `Funnel` — Landing Page Builder

**Inspiration:** ClickFunnels, Framer

**Purpose:** Build conversion-optimized landing pages for specific campaigns (launches, webinars, promotions).

**Core Features:**
- Drag-and-drop page builder
- Component library (hero, features, testimonial, CTA, FAQ)
- A/B testing (variant comparison)
- Conversion tracking (visit → signup → purchase)
- Custom domains
- Exit-intent popups
- Countdown timers

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Conversion rates, A/B test results |
| List | Manage funnels |
| Detail | Page builder, variant config, analytics |
| Settings | Custom domains, integrations |

**Public Pages:**
- `/{funnelSlug}` — Landing page (no basePath)
- Various custom domains

**Integration Points:**
- Embeds checkout flows from Event, Vault, Academy, Community
- Captures leads via Form module
- Triggers Broadcast sequences

**Revenue Model:** Indirect (conversion optimization)

**Build Priority:** 6

</details>

<details>
<summary><strong>9. Invoice App</strong></summary>

### `Invoice` — Manual Invoicing

**Inspiration:** Stripe Invoicing, Xero

**Purpose:** Generate and send manual invoices (B2B clients, sponsorships, custom services).

**Core Features:**
- Invoice creation (line items, quantities, tax rates)
- Client management (billing address, tax ID)
- PDF generation and email dispatch
- Payment tracking (paid, partial, overdue)
- Recurring invoice templates
- Credit note issuance

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Outstanding, paid this month, overdue |
| List | All invoices |
| Detail | Line items, payment status, email log |
| Settings | Company info, tax rates, templates |

**Integration Points:**
- Uses Billing module ledger for recording
- Triggers LHDN submission for Malaysia
- Syncs to CRM client profiles

**Revenue Model:** Direct (B2B invoicing)

**Build Priority:** 12

</details>

<details>
<summary><strong>10. Pipeline App</strong></summary>

### `Pipeline` — Sales CRM

**Inspiration:** Pipedrive, HubSpot Deals

**Purpose:** Track B2B deals from lead to close. Manage sponsorship, partnership, and enterprise sales.

**Core Features:**
- Deal creation (value, stage, expected close date)
- Stage management (Lead → Qualified → Proposal → Negotiation → Closed)
- Activity tracking (calls, emails, meetings)
- Contact linking
- Forecasting (weighted pipeline value)
- Win/loss analysis

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Pipeline value, win rate, cycle time |
| List | Kanban board |
| Detail | Activities, notes, contact history |
| Settings | Stages, custom fields |

**Integration Points:**
- Syncs with CRM contacts
- On close → creates Invoice

**Revenue Model:** Indirect (B2B sales enablement)

**Build Priority:** 13

</details>

---

## Retention Apps (Nurture & Compound)

<details>
<summary><strong>11. Community App (Skool Clone)</strong></summary>

### `Community` — Membership Platform

**Inspiration:** Skool, Circle, Mighty Networks

**Purpose:** Recurring-revenue membership community. The retention engine.

**Core Features:**
- Plan creation (monthly, annual, lifetime)
- Subscription lifecycle (active, past due, cancelled, banned)
- Coupon management (percentage, fixed, usage limits)
- Member directory and profiles
- Discussion feed (posts, comments, reactions)
- Magic link authentication (passwordless)
- Reminder schedules (renewal, re-engagement)
- Grace period management
- Payment retry logic (dunning)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | MRR, churn rate, active subscribers |
| Subscribers | Member list, status, payment history |
| Plans | Plan management, pricing, features |
| Coupons | Coupon creation and tracking |
| Templates | Email/SMS template management |
| Automations | Reminder schedules, lifecycle rules |
| Transactions | Payment log, refunds |
| Payment Settings | Gateway config, retry rules |

**Public Pages:**
- `/{communitySlug}` — Community landing
- `/{communitySlug}/{planSlug}` — Plan details
- `/{communitySlug}/{planSlug}/checkout` — Subscription checkout
- `/{communitySlug}/{planSlug}/success` — Welcome page
- `/{communitySlug}/portal` — Member portal

**Integration Points:**
- Creates `ClientProfileEntity` on subscription
- Emits `CommunitySubscriptionActivatedIntegrationEvent` → grants app entitlements
- Emits `CommunityCheckoutInitiatedIntegrationEvent` → tracks conversion
- Consumes `GatewayPaymentCompletedIntegrationEvent` → activates subscription
- Consumes `GatewayPaymentFailedIntegrationEvent` → dunning
- Consumes `GatewayRefundCompletedIntegrationEvent` → cancels subscription
- Triggers Broadcast for member communications

**Revenue Model:** Direct (recurring subscriptions)

**Build Priority:** 4 (tied with Academy)

</details>

<details>
<summary><strong>12. Broadcast App (Mailchimp Clone)</strong></summary>

### `Broadcast` — Email Marketing & Automation

**Inspiration:** Mailchimp, Kit (ConvertKit), Resend

**Purpose:** Nurture leads and customers through email sequences and broadcasts.

**Core Features:**
- Subscriber management (lists, segments, tags)
- Broadcast campaigns (one-time emails)
- Automated sequences (trigger-based, time-based)
- Template builder (HTML, Markdown, drag-and-drop)
- Variable interpolation (name, product, etc.)
- Open/click tracking
- Unsubscribe management
- A/B subject line testing

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Open rate, click rate, subscriber growth |
| Campaigns | Broadcast list and creation |
| Automations | Sequence builder |
| Templates | Email template management |
| Subscribers | List management, segmentation |
| Settings | Domain config, sender identity |

**Integration Points:**
- Receives leads from Giveaway, Form, Event, Vault, Academy, Community
- Triggers based on CRM tags
- Unsubscribe syncs to CRM

**Revenue Model:** Indirect (nurture → conversion)

**Build Priority:** 5

</details>

<details>
<summary><strong>13. Affiliate App (Rewardful Clone)</strong></summary>

### `Affiliate` — Partner Referral Program

**Inspiration:** Rewardful, PartnerStack, Tolt

**Purpose:** Enable customers and partners to refer new customers for commission.

**Core Features:**
- Affiliate onboarding (application, approval)
- Unique referral link generation
- Click and conversion tracking
- Commission rules (percentage, fixed, recurring)
- Payout management (threshold, schedule)
- Affiliate dashboard (clicks, conversions, earnings)
- Fraud detection (self-referral, IP matching)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Affiliate revenue, top affiliates |
| List | Manage affiliates |
| Detail | Stats, payouts, commission rules |
| Settings | Commission structure, payout config |

**Integration Points:**
- Tracks referral attribution on checkout
- Emits `CommissionAccruedIntegrationEvent` → Billing
- Pays out via Billing credit ledger

**Revenue Model:** Indirect (distribution multiplier)

**Build Priority:** 7

</details>

<details>
<summary><strong>14. Sponsor App (Passionfroot Clone)</strong></summary>

### `Sponsor` — Sponsorship Marketplace

**Inspiration:** Passionfroot, Sponsorcat

**Purpose:** Sell sponsorship inventory (newsletter slots, event sponsors, podcast ads) to brands.

**Core Features:**
- Media kit generation (audience stats, pricing)
- Sponsorship inventory (slots, dates, availability)
- Booking and payment flow
- Sponsor management (brand, contact, assets)
- Delivery tracking (proof of placement)
- Renewal automation

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Sponsorship revenue, upcoming slots |
| List | Inventory management |
| Detail | Booking details, sponsor info, delivery |
| Settings | Pricing, media kit config |

**Integration Points:**
- Creates `ClientProfileEntity` on booking
- Emits payment events to Billing
- Generates invoice via Invoice module
- Syncs to Pipeline for B2B deal tracking

**Revenue Model:** Direct (sponsorship sales)

**Build Priority:** 14

</details>

<details>
<summary><strong>15. Support App</strong></summary>

### `Support` — Customer Helpdesk

**Inspiration:** Crisp, Intercom

**Purpose:** Manage customer support tickets and inquiries.

**Core Features:**
- Ticket creation (email, form, chat)
- Ticket assignment and status (open, pending, resolved, closed)
- Threaded conversation
- Canned responses
- Satisfaction survey (post-resolution)
- Knowledge base (optional)

**Admin Pages:**
| Page | Function |
|---|---|
| Dashboard | Open tickets, response time, satisfaction |
| List | Ticket queue |
| Detail | Conversation thread, history |
| Settings | Workflows, canned responses |

**Integration Points:**
- Links to CRM client profiles
- Triggers Broadcast for satisfaction surveys

**Revenue Model:** Indirect (retention enabler)

**Build Priority:** 15

</details>

---

## Module Priority Summary

| Priority | Module | Category | Revenue Type | Status |
|---|---|---|---|---|
| 1 | `Event` | Acquisition | Direct | ✅ Complete |
| 2 | `Giveaway` | Acquisition | Indirect | 🔨 Build Next |
| 3 | `Vault` | Fulfillment | Direct | 📋 Planned |
| 4 | `Academy` | Fulfillment | Direct | 📋 Planned |
| 4 | `Community` | Retention | Direct | 📋 Planned |
| 5 | `Broadcast` | Retention | Indirect | 📋 Planned |
| 6 | `Funnel` | Fulfillment | Indirect | 📋 Planned |
| 7 | `Affiliate` | Retention | Indirect | 📋 Planned |
| 8 | `Consult` | Acquisition | Direct | 📋 Planned |
| 9 | `CRM` | Core | — | ✅ Exists |
| 10 | `Form` | Acquisition | Indirect | 📋 Planned |
| 11 | `Bio` | Acquisition | Indirect | 📋 Planned |
| 12 | `Invoice` | Fulfillment | Direct | 📋 Planned |
| 13 | `Pipeline` | Fulfillment | Indirect | 📋 Planned |
| 14 | `Sponsor` | Retention | Direct | 📋 Planned |
| 15 | `Support` | Retention | Indirect | 📋 Planned |

---

## Kill Criteria

Each module must meet its revenue threshold within 3 months of launch:

$$
\text{Keep Module} \iff \text{Revenue}(M_i) > \text{Maintenance Cost}(M_i) \times 12
$$

| Module | Monthly Revenue Threshold |
|---|---|
| Event | ✅ Passed (RM2,445/mo) |
| Giveaway | RM500/mo equivalent lead value |
| Vault | RM2,000/mo |
| Academy | RM3,000/mo |
| Community | RM3,000/mo |
| Broadcast | RM2,000/mo equivalent time saved |
| Funnel | RM2,000/mo conversion lift |
| Affiliate | RM1,000/mo |
| Consult | RM1,000/mo |
| Form | RM500/mo |
| Bio | RM500/mo |
| Invoice | Infrastructure (no threshold) |
| Pipeline | RM1,000/mo |
| Sponsor | RM1,000/mo |
| Support | Infrastructure (no threshold) |

**If a module fails its threshold for 3 consecutive months → replace with external integration.**

---

## Standard Module Structure

Every backend module follows:

```
Modules/{ModuleName}/
├── Application/         # Commands, queries, handlers, DTOs
├── Contracts/           # Public interfaces, integration events
├── Domain/              # Aggregates, entities, value objects, rules
└── Infrastructure/      # EF Core, endpoints, workers, gateways
```

Every frontend app follows:

```
ops-page/src/modules/{appName}/
├── pages/
│   ├── DashboardPage.tsx
│   ├── ListPage.tsx
│   ├── DetailPage.tsx
│   └── SettingsPage.tsx
└── components/
```

