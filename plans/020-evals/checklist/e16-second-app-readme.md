# E16 — Second-app README on the host

**Track:** E · **Depends:** E10–E15, M22  
**Goal:** One page: key → mint → pay_url → webhook → unlock.

**Why:** Host README is cashier-shaped. After M22+W29+E15 the second-app story must live next to the JWT curl, with three webhook planes named.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/README.md` | JWT mint curl |
| `plans/020-evals/11-what-next.md` | Sentences |
| W14 vs One inbound vs Stripe vault | Three `whsec_` |

**Current (`6d730d15`):** No second-app section.

---

## E16.1

- [ ] `apps/lazuar-pay/README.md` section **Second app**
- [ ] Links `examples/pay-node`
- [ ] Three webhook planes named so Stripe `whsec_` is not pasted into Plane C
- [ ] Testing loopback hatch mentioned
- [ ] Honesty: first-party SPA not required to take money

## E16.2 Exit

- [ ] Track E complete; K99a can close
