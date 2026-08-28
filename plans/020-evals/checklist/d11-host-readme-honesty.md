# D11 — Host README allowed sentences

**Track:** D · **Depends:** K00  
**Analysis:** [`../10-honesty-production-bar.md`](../10-honesty-production-bar.md) §10; [`../11-what-next.md`](../11-what-next.md)  
**Goal:** README cannot outrun the code.

**Why:** 10-honesty lists allowed vs forbidden sentences. After M14/W21, **revisit** this file and tick the new true claims.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/README.md` | Cashier + One webhook PUT |
| `plans/020-evals/10-honesty-production-bar.md` §10 | Allowed sentences |
| `plans/020-evals/11-what-next.md` | Default Job A |

**Current (`6d730d15`):** README already says no PAT, no Pay POST to One webhooks. Does not claim M2M or Plane C (good). Must not start claiming them early.

---

## D11.1 May

- [ ] Hosted cashier for One workspaces
- [ ] Staff via One OIDC; buyers have no One account
- [ ] `/v1` is the door; Official Receipt is not a tax invoice

## D11.2 Must not (until the matching phase is done)

- [ ] “Production-ready”
- [ ] “API keys” / M2M until M14+M22
- [ ] “We send `payment.completed`” until W21
- [ ] “Test is always available” (only when host lists Test)

## D11.3 Exit

- [ ] Revisit after M22 and W29 to add the true sentences
