# 09 — Hub vs DIY documentation policy

**Status:** analysis complete 2026-08-10  
**Goal:** Document honest trade-offs of Hub cashier vs integrating Billplz/Stripe yourself — **without** publishing insecure DIY tutorials that recreate the problem Hub solves. Sample app reinforces “no gateway SDK in the app.”

---

## 1. Product positioning (one paragraph)

Lazuar Hub’s **Payments cashier** is a BYOK multi-gateway façade: your server creates ad-hoc checkouts with a scoped `sk_`, guests pay on the **merchant’s** processor page, and your server receives **one** signed webhook shape (`payment.completed` / `payment.failed`) regardless of Billplz, Stripe, CHIP, or Razorpay. Hub is **not** Merchant of Record for guest GMV. Domain objects stay in your app.

DIY means your app calls processors directly, verifies each provider’s signatures, and normalizes events yourself.

---

## 2. Audience for the comparison page

| Audience | Need |
|----------|------|
| Founder / eng lead | Build vs buy rails decision |
| Migrating from Aura legacy mode | Why Hub-only is end state |
| Security reviewer | Secret surface comparison |
| Sample user | Confirmation they should not add Billplz SDK |

Not for: developers seeking “how to implement Billplz X-Signature from scratch” (point to **provider docs**, not Hub docs).

---

## 3. Placement

| Location | Role |
|----------|------|
| **Primary** `guide/hub-vs-diy.md` | Full condensed comparison |
| **Secondary** embed short table on `guide/architecture-who-does-what.md` (M6) | Same numbers, single link out |
| **Tertiary** callout on `integrations/payments-cashier.md` | “Why not call Billplz from my app?” → hub-vs-diy |
| **Sample README** | One paragraph + link |
| **Not** root marketing README long DIY section | Keep Hub monorepo README product-focused |

Hybrid placement: deep page + embeds. **No** third full copy.

---

## 4. Condensed comparison tables (paste-ready)

### 4.1 Pros / cons

```markdown
## Hub cashier vs DIY gateways

| | Hub Payments cashier | DIY (Billplz/Stripe in your app) |
|--|----------------------|----------------------------------|
| **Integration surface** | One HTTP API + one signature scheme | One API + signature scheme **per** processor |
| **Hosted pay page** | Yes (via gateway) | Yes (you wire each) |
| **Settlement** | Merchant account (BYOK) | Merchant account |
| **MoR for guest GMV** | No | No (unless you use a MoR product) |
| **Credential storage** | Hub vault per workspace | Your vault / secrets manager |
| **Multi-gateway** | Adapters already in Hub | You write adapters |
| **Metadata quirks** | Hub session survives (e.g. Billplz strip) | You discover edge cases in prod |
| **Ops UI for keys** | Hub Ops | You build or use scripts |
| **Fulfillment signal** | Normalized `payment.*` webhooks | Provider-specific events |
| **Vendor lock-in** | Hub API (open HTTP) | Processor APIs (also lock-in) |
| **Moving parts in your app** | Keys + verify + domain unlock | Keys + N verifies + N clients + unlock |
| **When DIY wins** | Extreme custom processor features Hub lacks; single gateway forever; regulatory need to avoid intermediary | — |
| **When Hub wins** | Multi-gateway MY/SEA stack; multi-tenant SaaS; want domain-focused eng | — |
```

### 4.2 Security / responsibility (condensed)

```markdown
| Concern | DIY | Hub |
|---------|-----|-----|
| Processor API secrets in app | Yes | No (BYOK in Hub) |
| Webhook secrets in app | Per processor | One `whsec_` (Hub) |
| Risk of trusting browser redirect | Same footgun | Same footgun — docs forbid |
| Signature bugs | N implementations | 1 implementation (`OutboundWebhookSignature`) |
| Cross-tenant key misuse | Your bug classes | Workspace-bound `sk_` |
```

### 4.3 Cost / ops (honest, non-pricing)

```markdown
| Topic | Note |
|-------|------|
| Processor fees | Unchanged — merchant pays gateway fees either way |
| Hub product pricing | Separate from GMV MoR cut; document commercially outside this tech guide |
| Eng time | DIY costs ongoing adapter + verify maintenance |
| Incident debug | DIY: provider logs only; Hub: Ops delivery logs + session |
```

### 4.4 Feature matrix (M6 short)

```markdown
| Capability | DIY Billplz | DIY Stripe | Hub |
|------------|-------------|------------|-----|
| Create payment | Billplz API | Stripe API | `POST …/integrations/payments/checkouts` |
| Verify | Billplz rules | Stripe rules | `X-Lazuar-Signature` |
| Multi-gateway | You | You | Built-in allow-list |
| Normalized paid event | You | You | `payment.completed` |
```

---

## 5. Explicit non-content (do not publish)

| Forbidden in Hub docs | Why |
|-----------------------|-----|
| Full Billplz create-bill tutorial with production secrets patterns | Undermines cashier; duplicates provider docs poorly |
| Copy-paste Stripe webhook Express app as “alternative path” | Diverts from supported path |
| Dual-stack code samples “Hub **and** Billplz in same route” | Encourages dual-run complexity for new apps |
| Hardcoded collection IDs / API keys in snippets | Secret hygiene |
| “Skip signature in development” without huge warning | Teaches insecure default |
| Claiming Hub is MoR | False |
| Claiming DIY is “unsupported and will be blocked” | False — DIY remains possible; we just do not teach it |

### Allowed references

- Link to **official** Billplz/Stripe docs for merchants configuring BYOK **inside Hub Ops**.  
- Aura dual-run as **migration history**, not recommended greenfield.  
- Error `PAYMENTS_NOT_CONFIGURED` when BYOK missing.

---

## 6. Hybrid messaging (careful)

Some tenants may run:

1. Hub for new products  
2. Legacy DIY for old code paths  

Docs language:

> **Greenfield:** Hub-only.  
> **Migration:** temporary dual-run may exist in first-party apps (e.g. Aura). Do not design new systems around dual-run.

Sample app: **Hub-only**, zero gateway SDKs.

---

## 7. Sample app reinforcement

| Sample artifact | Message |
|-----------------|---------|
| `package.json` dependencies | No `billplz`/`stripe` packages |
| README badge/line | “No processor SDK — Hub cashier only” |
| Webhook code comments | “Verify Hub only; gateway signatures are Hub’s job” |
| Anti-pattern section | M7 rows from 02 |
| run-sample-app docs | Prerequisites never include Billplz API secret in sample env |

Optional README table:

```markdown
| In sample `.env` | Not in sample |
|------------------|---------------|
| `HUB_API_KEY` | Billplz Collection ID |
| `HUB_WEBHOOK_SECRET` | Stripe Secret Key |
| `HUB_API_BASE_URL` | Provider webhook secrets |
```

---

## 8. Page outline `guide/hub-vs-diy.md`

1. When to use Hub  
2. Comparison table (§4.1)  
3. Security table (§4.2)  
4. What Hub does **not** replace (domain, MoR SaaS seats, Commerce catalog)  
5. Migration note (dual-run)  
6. Next: payment flow / sample / cashier guide  
7. **No** DIY code samples beyond “you would maintain N clients” prose  

Length target: short (≈1 screen of tables + short prose). Depth lives in architecture matrices.

---

## 9. Review criteria for future docs PRs

Reject or rewrite if PR:

- Adds processor-specific create-payment code to integrator guides  
- Shows verifying Billplz signature **in the app** while also using Hub checkout  
- Puts gateway secrets in sample env examples  

Accept if PR:

- Improves BYOK Ops configuration honesty  
- Adds gateway names to allow-list docs  
- Clarifies Hop 1 vs Hop 2  

---

## 10. Implementation checklist

- [ ] Create `guide/hub-vs-diy.md` with condensed tables only  
- [ ] Link from homepage, payments-cashier, architecture page  
- [ ] Sample README anti-DIY paragraph  
- [ ] Sidebar entry (08)  
- [ ] No DIY tutorial code in PR  
