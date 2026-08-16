# W1-LP-183 — Time-to-first-checkout (onboarding friction)

**Status:** Analysis only — **do not implement from this file**  
**Date:** 2026-08-16  
**ID authority:** [00-implement-ids.md](../00-implement-ids.md) Wave 1 `LP-183`. Tracker: *Time-to-first-checkout &lt; 15 min* — Lazuar **P**.  
**Not this ID:** Public marketing/pricing page (`LP-006`). Self-serve **second** workspace empty-state (`LP-184`). Sandbox host match (`LP-182`). BM/EN / branding (`LP-020`/`LP-025`).

**Invariant:** A new merchant who just registered can go **signup → gateway → (email) → product → openable pay link** in one sitting, with a **checklist** that names the blockers. Today the blockers exist but are scattered banners.

---

## 0. Scope lock

In scope:

- Post-signup linear checklist on Ops dashboard (or a `/getting-started` route)
- Honest copy of the two hard gates: **payment config** and **Resend**
- Copy-link CTA once a product exists
- Optional: stop requiring email to **create** a product (keep requiring it to **activate** public checkout)

Out of scope:

- Removing BYOK (never)
- Removing Resend from **live** checkout (receipts/dunning)
- Email verify / forgot-password UI (later, 18-pricing LP-013)
- Wizard that calls Stripe Connect OAuth
- Public pricing (`LP-006`)

---

## 1. Verdict

The loop is **demoable** for someone who already knows the product. It is **not** &lt; 15 min for a stranger.

| Step | Exists? | Friction |
|------|---------|----------|
| Sign up + first workspace | **Y** — `LoginPage` + `RegisterPublicUser` | No TOS; name = email local-part |
| Land on dashboard | **Y** | Empty KPIs + rose/amber banners |
| Payment gateway | **Y** — Settings page | Must know collection id / Stripe sk |
| Resend | **Y** — **hard gate** on `CreateProduct` and `InitiateCheckout` | Merchant must have a Resend account **before** a link exists |
| Create product | **Y** | Fails with email error if banner ignored |
| Copy checkout URL | **Y** on product card | Easy once created |
| First test pay | Depends on LP-182 + public webhook URL | Tunnel not mentioned in Ops |

Dashboard already warns. There is **no** numbered checklist, **no** “you are 2 of 4 done,” **no** link to VitePress hosted-checkout (LP-144).

---

## 2. Current files

| Path | Role |
|------|------|
| `apps/lazuar-ops/src/components/LoginPage.tsx` | Signup |
| `RegisterPublicUserCommand.cs` | User + org + entitlements OPS/BILLING/PAYMENTS/CRM/LHDN |
| `DashboardPage.tsx` | `showGatewayWarning`, `showEmailWarning` |
| `CreateProductCommandHandler.cs` | `HasValidEmailConfigAsync` → throw |
| `InitiateCheckoutCommandHandler.cs` | Same throw — checkout disabled |
| `UpdateProductCommandHandler.cs` | Same |
| `PaymentSettingsPage.tsx` / `EmailSettingsPage.tsx` | Forms |
| `StarterCreditSeederHandler.cs` | 50 credits (LHDN, not checkout) |

---

## 3. Gaps

### G1 — No single onboarding path (P0 UX)

Four clicks across sidebar vs one checklist.

### G2 — Email gate is unexplained until create fails

Banner exists; create product still the first “real” error for many users.

### G3 — No “open your link” success state

After product create, we do not highlight the public URL or a “open as buyer” button.

### G4 — Production webhook reachability invisible

Billplz needs public `App:ApiBaseUrl`. Ops never says “set tunnel / wait for prod.” Pair with LP-182 docs; one checklist line is enough.

**Not gaps**

- Signup itself (that is why this is **P** not **N**).  
- KYC (refused).

---

## 4. Minimal changes

### 4.1 Must — Getting started checklist

On `DashboardPage` (top, above KPIs) when any step is incomplete:

| # | Done when | CTA |
|---|-----------|-----|
| 1. Workspace | Always true after register | — |
| 2. Payment gateway | Same predicate as `showGatewayWarning` (inverted) | `/workspace/payment-gateways` |
| 3. Email (Resend) | `emailConfig.is_active && has_api_key` | `/workspace/email` |
| 4. First product | `products.length > 0` | `/commerce/products` |
| 5. Share link | product has slug | Copy `/{workspace_slug}/checkout/{product_slug}` (need slug from entitlements + product) |

Hide the block when all five are done (or dismiss for 30 days `localStorage`).

Keep the existing rose/amber banners **or** replace them with this list — do not duplicate three warnings.

### 4.2 Should — soften create-product vs checkout

- **Create/update product** allowed without Resend (draft).  
- **InitiateCheckout** still requires Resend (buyer would get no receipt; gate stays).  
- Checklist still shows email as required for “first **paid** checkout.”

This lets a merchant design the product while waiting on Resend DNS.

### 4.3 Should

- After create product, toast + copy link.  
- One line: “Test cards: Stripe test mode / Billplz sandbox (see docs).” Link VitePress hosted-checkout + environments.

### 4.4 Do not

- Auto-provision a demo gateway.  
- Skip Resend on live initiate.  
- Build a multi-page modal wizard (checklist is enough).

---

## 5. Tests

Mostly UI. API:

| Case | Expect |
|------|--------|
| If §4.2: `CreateProduct` without email | 200; product exists |
| `InitiateCheckout` without email | throw / 400 same message as today |
| With email + gateway | checkout URL |

Manual stopwatch: new account → Billplz sandbox + Resend test → product → open portal checkout &lt; 15 min on a machine that already has those accounts.

---

## 6. Risks

| Risk | Mitigation |
|------|------------|
| Products created, checkout still dead | Checklist step 3 stays red; initiate still gated |
| Workspace slug missing on client | Entitlements already have `workspace_slug` |

---

## 7. Acceptance

1. New tenant sees a 5-step checklist naming the two hard gates.  
2. Completing gates + product yields a copyable hosted URL without hunting.  
3. Checkout still cannot start without Resend.  
4. Tracker **P → Y** if a clean run is documented &lt; 15 min **assuming** the merchant already has Billplz/Stripe + Resend accounts (we cannot mint those).

---

## 8. Implement order

1. Dashboard checklist  
2. Optional create-product gate relax + tests  
3. Copy-link affordance  
