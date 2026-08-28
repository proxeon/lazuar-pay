# K99a — Kernel definition of done (Job A)

**Track:** Program  
**Depends:** M14, M18, U11, W21, E15  
**Analysis:** [`../11-what-next.md`](../11-what-next.md) §3  
**Goal:** Close Job A honestly. Not Bar B. Not platform.

**Why:** Without a close sentence, the board grows refunds and OTel and still has no second caller.

**Related files (proof, not new work)**

| Path | Must be true |
|------|----------------|
| M14 tests | Key `POST /v1/checkouts` 201 |
| U11 | `pay_url` on 201 |
| W21 | Worker 2xx + HMAC round-trip |
| `examples/pay-node` | E15 unlock |
| IsolationTests | Still bans Hub outbound names (W28) |
| `apps/lazuar-pay/README.md` | Second-app section (E16) |

**Current until then:** Do not tick.

---

## K99a.1 Sentence a stranger can run

- [ ] Created a One workspace
- [ ] Minted a scoped `lzr_sk_` (`tenant:read` + `authz:check`)
- [ ] `POST http://localhost:8081/v1/checkouts` with that Bearer → **201** + `pay_url`
- [ ] Buyer paid (Test start on laptop, or sandbox rail)
- [ ] Sample received signed `payment.completed` and unlocked a toy row
- [ ] Human JWT **member** still cannot mint
- [ ] Key of org A cannot mint on org B
- [ ] No endpoint still pays (cashier without a second app)

## K99a.2 Evidence in-repo

- [ ] Hermetic tests for M14, M15, M18, U14, W21, W27, W28
- [ ] `task pay:spec` + honesty exit 0
- [ ] IsolationTests still red on cathedral strings + Hub outbound names
- [ ] Hub sample marked museum
- [ ] README may say: One `lzr_sk_` is a Pay merchant credential; Pay signs `payment.completed` (One dialect)

## K99a.3 Still not claimed

- [ ] Not production-ready
- [ ] Not refunds / subscriptions / pagination
- [ ] Not Standard Webhooks
- [ ] Not “Pay mints API keys”
- [ ] Not Job B (`/ready`, persist-before-PSP, captured One pause) — see K99b

## K99a.4 Must not

- [ ] Do not flip 011/11 Status cells from this close
- [ ] Do not delete Hub code “as the kernel proof”

## K99a.5 Exit

- [ ] Job A program closed. Next: either K99b / track G, or parked P-list
