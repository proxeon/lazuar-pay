# S25 — Diagram maintenance policy

**Track:** Docs diagrams · **Analysis:** `../01` §8  
**Depends on:** S20–S24 ideally landed first  
**Goal:** Prevent diagram rot.

---

## S25.1 Edit `guide/how-to-maintain.md`

- [ ] Section: diagram format (Mermaid/ASCII per S20)
- [ ] Rule: same PR as API path/header/event changes
- [ ] Rule: product-line label Payments M2M on cashier diagrams
- [ ] Rule: hop1 vs hop2 not conflated
- [ ] Rule: success_url never drawn as fulfillment
- [ ] Rule: prose summary required under every diagram
- [ ] Rule: no live secrets in diagrams
- [ ] Rule: canonical E2E lives on `payment-flow` — don’t fork
- [ ] Rule: envelope shape documented when changing webhooks
- [ ] Checklist for diagram PRs (copy from analysis 01 maintainer checklist)

## S25.2 Sources table

- [ ] Add architecture-who-does-what, payment-flow, run-sample-app (when exists)
- [ ] Note sample path `examples/hub-cashier-next`
- [ ] Remove dead second-app-proof as live source if still listed

## S25.3 Exit

- [ ] Maintainers can update diagrams without rediscovering rules
