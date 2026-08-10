# S25 — Diagram maintenance policy

**Track:** Docs diagrams · **Analysis:** `../01` §8  
**Depends on:** S20–S24 ideally landed first  
**Goal:** Prevent diagram rot.

---

## S25.1 Edit `guide/how-to-maintain.md`

- [x] Section: diagram format (Mermaid/ASCII per S20)
- [x] Rule: same PR as API path/header/event changes
- [x] Rule: product-line label Payments M2M on cashier diagrams
- [x] Rule: hop1 vs hop2 not conflated
- [x] Rule: success_url never drawn as fulfillment
- [x] Rule: prose summary required under every diagram
- [x] Rule: no live secrets in diagrams
- [x] Rule: canonical E2E lives on `payment-flow` — don’t fork
- [x] Rule: envelope shape documented when changing webhooks
- [x] Checklist for diagram PRs (copy from analysis 01 maintainer checklist)

## S25.2 Sources table

- [x] Add architecture-who-does-what, payment-flow, run-sample-app (when exists)
- [x] Note sample path `examples/hub-cashier-next`
- [x] Remove dead second-app-proof as live source if still listed (kept as curl harness; sample path added)

## S25.3 Exit

- [x] Maintainers can update diagrams without rediscovering rules
