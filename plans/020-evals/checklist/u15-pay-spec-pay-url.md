# U15 — TypeSpec `pay_url`

**Track:** U · **Depends:** U11 (host first)  
**Analysis:** honesty script; [`../09-spec-docs-sample.md`](../09-spec-docs-sample.md)  
**Goal:** Spec matches live JSON. Dist stays gitignored.

**Why:** 019 spec lag was 22 Map* / 13 tsp. 002 honesty is green. A new live field without tsp is the same hole.

**Related files**

| Path | Role today |
|------|------------|
| `packages/pay-spec/main.tsp` | `CheckoutSession`, `PaymentLink` |
| `scripts/check-pay-openapi-honesty.mjs` | Path honesty; add a field check if you want |
| `Taskfile.yml` | `pay:spec` |
| `packages/pay-spec/dist/openapi.yaml` | Gitignored; compile locally |

**Current (`6d730d15`):** Honesty 22 spec / 24 Map*. No `pay_url`.

---

## U15.1

- [ ] `CheckoutSession.pay_url?: string` (or required if host always sets it)
- [ ] `PaymentLink.pay_url?: string`
- [ ] `task pay:spec` + `node scripts/check-pay-openapi-honesty.mjs` exit 0
- [ ] Do not add unversioned probes to tsp

## U15.2 Exit

- [ ] Track U complete
