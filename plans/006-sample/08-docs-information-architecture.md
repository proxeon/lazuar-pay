# 08 — Docs information architecture (`lazuar-docs`)

**Status:** analysis complete 2026-08-10  
**Config today:** `apps/lazuar-docs/docs/.vitepress/config.ts`  
**Content root:** `apps/lazuar-docs/docs/`

---

## 1. Current IA

### Nav (top)

| Label | Link |
|-------|------|
| Guide | `/` |
| Payments | `/integrations/payments-cashier` |
| Webhooks | `/integrations/webhooks` |
| API → OpenAPI overview | `/reference/openapi` |
| API → Developers (Scalar) | `http://localhost:3000` ⚠️ likely stale vs developers on **3002** |

### Sidebar

**Start**

- Introduction `/`  
- Product lines `/guide/product-lines`  
- Concepts `/guide/concepts`  

**Integrations**

- Overview `/integrations/`  
- Payments cashier (M2M)  
- Provision a workspace  
- Create a checkout  
- Webhooks  
- API keys & scopes  
- Environments & public URLs  
- Aura as a reference client  
- Second-app checklist  

**Reference**

- Error codes  
- Event catalog  
- OpenAPI & Scalar  
- Glossary  
- How to maintain  

### Gaps vs program goals

| Missing page | Why needed |
|--------------|------------|
| Architecture / who does what | Matrices M1–M7 home |
| Payment flow narrative | Single scroll with diagrams D-E2E + D-WH |
| Run sample app | Operational path to `examples/hub-cashier-next` |
| Hub vs DIY | Condensed comparison (09) |
| Mermaid diagrams | Content exists only as ASCII on integrations index |

### Homepage (`index.md`)

Hero + 4 features + start table. No sample CTA. Status “drafts for refinement.”

---

## 2. Proposed IA

### Design principles

1. **Learning path left-to-right:** concepts → architecture → flow → integrate → run sample → reference.  
2. **Task pages stay short;** deep matrices live on architecture page.  
3. **Aura is reference, not gate.** Sample is the runnable proof.  
4. **One port truth:** API 8080; Scalar 3002 (fix nav).  
5. Draft footer can remain until public publish.

### Proposed sidebar

```ts
const sidebar = [
  {
    text: "Start",
    collapsed: false,
    items: [
      { text: "Introduction", link: "/" },
      { text: "Product lines", link: "/guide/product-lines" },
      { text: "Concepts", link: "/guide/concepts" },
      { text: "Who does what", link: "/guide/architecture-who-does-what" },
      { text: "Payment flow", link: "/guide/payment-flow" },
      { text: "Hub vs DIY", link: "/guide/hub-vs-diy" },
    ],
  },
  {
    text: "Integrations",
    collapsed: false,
    items: [
      { text: "Overview", link: "/integrations/" },
      { text: "Payments cashier (M2M)", link: "/integrations/payments-cashier" },
      { text: "Provision a workspace", link: "/integrations/provision" },
      { text: "Create a checkout", link: "/integrations/create-checkout" },
      { text: "Webhooks", link: "/integrations/webhooks" },
      { text: "API keys & scopes", link: "/integrations/api-keys" },
      { text: "Environments & public URLs", link: "/integrations/environments" },
      { text: "Run the sample app", link: "/integrations/run-sample-app" },
      { text: "Second-app checklist", link: "/integrations/second-app-checklist" },
      { text: "Aura as a reference client", link: "/integrations/aura-reference" },
    ],
  },
  {
    text: "Reference",
    collapsed: false,
    items: [
      { text: "Error codes", link: "/reference/error-codes" },
      { text: "Event catalog", link: "/reference/events" },
      { text: "OpenAPI & Scalar", link: "/reference/openapi" },
      { text: "Glossary", link: "/reference/glossary" },
      { text: "How to maintain", link: "/guide/how-to-maintain" },
    ],
  },
];
```

### Proposed nav

```ts
nav: [
  { text: "Guide", link: "/guide/product-lines" },
  { text: "Payments", link: "/integrations/payments-cashier" },
  { text: "Sample", link: "/integrations/run-sample-app" },
  { text: "Webhooks", link: "/integrations/webhooks" },
  {
    text: "API",
    items: [
      { text: "OpenAPI overview", link: "/reference/openapi" },
      {
        text: "Developers (Scalar)",
        link: "http://localhost:3002", // fix from 3000
      },
    ],
  },
],
```

---

## 3. New pages — content briefs

### 3.1 `guide/architecture-who-does-what.md`

**Title:** Who does what (app vs Hub vs gateway)

**Sections:**

1. At-a-glance matrix (02 §10)  
2. M1 Create payment  
3. M2 Webhook hops  
4. M3 Secrets  
5. M4 Multi-tenant + BYOK  
6. M5 Errors  
7. M6 Billplz vs Stripe vs Hub  
8. M7 Anti-patterns  
9. Links to sample + flow diagrams  

**Audience:** architects, tech leads evaluating Hub.

### 3.2 `guide/payment-flow.md`

**Title:** Payment flow (end-to-end)

**Sections:**

1. Intro: domain in app, rails in Hub  
2. Mermaid D-E2E + ASCII  
3. Step-by-step prose with links to provision/checkout/webhooks  
4. Mermaid D-WH dual hops  
5. What “paid” means (webhook only)  
6. Next: run sample  

### 3.3 `integrations/run-sample-app.md`

**Title:** Run the Hub cashier sample

**Sections:** per `06-provision-and-env.md` §9  

**Code links:** monorepo path `examples/hub-cashier-next` (relative mention; VitePress may not deep-link monorepo files unless configured).

### 3.4 `guide/hub-vs-diy.md`

**Title:** Hub cashier vs DIY gateways

**Sections:** per `09-hub-vs-diy-docs.md` — condensed tables only, no DIY tutorials.

---

## 4. Homepage updates

Add to features or start table:

| If you want to… | Read |
|-----------------|------|
| See who owns each step | [Who does what](/guide/architecture-who-does-what) |
| Walk the full money path | [Payment flow](/guide/payment-flow) |
| Run a Next.js sample | [Run the sample app](/integrations/run-sample-app) |
| Compare to DIY Billplz/Stripe | [Hub vs DIY](/guide/hub-vs-diy) |

Optional third brand action: “Run sample”.

Update status line when sample lands: “Includes runnable sample under `examples/hub-cashier-next`.”

---

## 5. Existing page edits (non-structural)

| Page | Change |
|------|--------|
| `integrations/index.md` | Upgrade ASCII → Mermaid; link architecture + sample |
| `payments-cashier.md` | Link payment-flow + sample |
| `provision.md` | Port 8080; optional mermaid; link sample env |
| `create-checkout.md` | Embed short M1; mermaid D-CHK |
| `webhooks.md` | Envelope honesty; M2; mermaid; Next raw body note |
| `environments.md` | Mermaid D-ENV; 8080 |
| `product-lines.md` | Mermaid D-PL |
| `second-app-checklist.md` | Point to sample as evidence; mermaid D-2ND |
| `api-keys.md` | Embed M3 secrets table |
| `error-codes.md` | Link M5 |
| `events.md` | Envelope + data shape |
| `concepts.md` | Link architecture page |
| `aura-reference.md` | “Runnable twin: sample app” |
| `how-to-maintain.md` | Diagram + matrix maintenance rules |
| `reference/openapi.md` | Scalar port 3002 |

---

## 6. Phased docs PRs (align D01/D02/D06)

| PR | Phase | Scope | Risk |
|----|-------|-------|------|
| **Docs-PR0** | D00/D01 | Fix Scalar port 3002; normalize 8090→8080 on pages you touch | Low |
| **Docs-PR1** | D01 | New architecture-who-does-what + hub-vs-diy; sidebar/nav; homepage links; embed M tables on existing pages | Low |
| **Docs-PR2** | D02 | Mermaid plugin + diagrams on existing pages; payment-flow page | Med (plugin) |
| **Docs-PR3** | D06 | run-sample-app page; second-app checklist links; homepage sample CTA | Depends on sample |

Do not wait for sample to land PR1 matrices.

### Parallelism

- PR1 ∥ sample D03 after D00 freeze  
- PR2 can follow PR1 or ship ASCII-only if Mermaid blocked  
- PR3 after sample README stable  

---

## 7. Cross-linking map

```text
index
  → product-lines
  → architecture-who-does-what
  → payment-flow
  → payments-cashier
      → provision → api-keys
      → create-checkout → error-codes
      → webhooks → environments
      → run-sample-app → second-app-checklist
  → hub-vs-diy
  → aura-reference (optional branch)
```

Avoid circular walls of links: prefer forward “Next” footers already used in guides.

---

## 8. SEO / publish later

Not goals for 006. When public:

- Set `base` if subpath  
- Point Scalar to prod `/docs`  
- Promote draft → stable badges  
- Keep anti-DIY tutorial policy  

---

## 9. Maintainability

Update `how-to-maintain.md`:

| Content type | SSoT |
|--------------|------|
| Paths / scopes | TypeSpec + IntegrationEndpoints |
| Signature | OutboundWebhookSignature |
| Matrices | architecture page (single full copy) |
| Diagrams | must match code; PR with API changes |
| Sample steps | examples README + run-sample-app |

Rule: **do not** maintain three conflicting checkout examples (quickstart, docs, sample) without a quarterly sync. Prefer sample + docs; engineer quickstart stays twin.

---

## 10. Implementation checklist

- [ ] Sidebar + nav in `config.ts`  
- [ ] Four new markdown pages  
- [ ] Homepage start table  
- [ ] Fix Developers link port  
- [ ] Cross-links from existing integrations  
- [ ] `pnpm --filter lazuar-docs build`  
