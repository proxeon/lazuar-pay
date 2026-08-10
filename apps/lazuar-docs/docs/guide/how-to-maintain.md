# How to maintain these guides

## Where content lives

```text
apps/lazuar-docs/
  docs/                 ← Markdown sources (this site)
  docs/.vitepress/      ← VitePress config
```

Related monorepo sources of truth:

| Concern | Source |
|---------|--------|
| OpenAPI / TypeSpec | `packages/api-spec/` |
| Live payments quickstart (engineers) | `docs/payments-integration-quickstart.md` |
| Curl harness | `script/second-app-proof.md` |
| Sample cashier app | `examples/hub-cashier-next` (when present) |
| Architecture ADRs | `docs/architecture-decision-log/` |
| Diagram design sources | `plans/006-sample/01-docs-flow-diagrams.md` |
| Wave locks | `plans/006-sample/wave-decisions.md` |

### Docs pages that carry diagrams / flow

| Page | Role |
|------|------|
| [Payment flow](/integrations/payment-flow) | **Canonical E2E** sequence (SSoT) |
| [Webhooks](/integrations/webhooks) | Dual hops + envelope + verify |
| [Provision](/integrations/provision) | Provision-only sequence |
| [Create checkout](/integrations/create-checkout) | Checkout + status notes |
| [Environments](/integrations/environments) | Hop1/hop2 network |
| [Product lines](/guide/product-lines) | Decision tree (mirrors table) |
| [Second-app checklist](/integrations/second-app-checklist) | Independence + proof sequence |
| [Payments cashier](/integrations/payments-cashier) | System context; links E2E to payment-flow |
| Architecture who-does-what | Matrices (when page exists) |
| Run sample app | Sample run sequence (when page exists) |

## Diagrams (S20 format + maintenance)

**Format (locked S20 Option A):** **ASCII-only** fenced `text` diagrams for this wave. Mermaid via `vitepress-plugin-mermaid` is an optional later upgrade — do not block content on the plugin. Every diagram needs a short **prose summary** (2–4 sentences) underneath for accessibility.

### Rules

1. **Same PR as API change** — If path, header, or event renames ship, update diagram labels in the same PR.  
2. **Product-line label** — Cashier diagrams are **Payments M2M** only; never mix Commerce `subscription.*` or LHDN `invoice.*`.  
3. **Hops** — Keep hop1 (Gateway → Hub) distinct from hop2 (Hub → your app). Browser `checkout_url` is not a webhook hop.  
4. **`success_url` is never fulfillment** — Do not draw browser return as unlock.  
5. **Prose under every diagram** — Required (a11y). ASCII twin or bullets also fine.  
6. **No live secrets** — Never paste real `sk_` / `whsec_` values.  
7. **Canonical E2E** — Full multi-party sequence lives on **`/integrations/payment-flow`**. Other pages deep-dive or link; do not fork a third diverging full E2E.  
8. **Envelope honesty** — Webhook docs must show runtime shape `{ id, event_type, created_at, data }` when outbound body changes.  
9. **Port discipline** — New diagrams use Hub API **8080**; mention 8090 only as historical drift.  
10. **Sources** — Prefer HTML comments like `<!-- source: IntegrationEndpoints.cs -->` near diagrams when paths come from code.

### Diagram PR checklist

- [ ] Labels match runtime modules / OpenAPI paths  
- [ ] Payments M2M product fence intact  
- [ ] Hop1 vs hop2 not conflated  
- [ ] `success_url` not drawn as fulfillment  
- [ ] Prose summary under each new/changed diagram  
- [ ] No live secrets  
- [ ] E2E changes updated on `payment-flow` first  
- [ ] Envelope fields updated on webhooks page if body changed  
- [ ] `pnpm --filter lazuar-docs build` green  
- [ ] Readable with CSS disabled (ASCII/prose present)

### Anti-patterns

- Diagrams that show Your app calling Billplz/Stripe directly  
- Mixing Commerce events into Payments cashier sequences  
- Aura-only actor names as the only sample (prefer “Your app”)  
- Mermaid-only pages without prose (when Mermaid is eventually enabled)

## Workflow

1. Change API or product behavior in the same PR as guide updates when practical.  
2. Prefer examples that match **snake_case** JSON (Hub ASP.NET default).  
3. Never commit live `sk_` / `whsec_` secrets into docs.  
4. Mark experimental paths with **Status: draft** in the page.  

## Local commands

```bash
# from monorepo root
pnpm --filter lazuar-docs dev      # http://localhost:5180
pnpm --filter lazuar-docs build
pnpm --filter lazuar-docs preview
```

## Publishing later

- Set `base` in `.vitepress/config.ts` if served under a subpath.  
- Point nav “Developers (Scalar)” at production lazuar-developers (hub `/docs`) URL.  
- Promote pages from draft → stable when contracts freeze.  
