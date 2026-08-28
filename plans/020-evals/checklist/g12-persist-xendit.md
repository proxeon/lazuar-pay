# G12 — Xendit persist-before-PSP

**Track:** G · **Depends:** G10 pattern · **Does not gate K99a**  
**Analysis:** [`../07-money-remaining.md`](../07-money-remaining.md) 014  
**Goal:** Same persist-after-HTTP hole on Xendit invoice/link create.

**Why:** XenditHosted uses named client `"xendit"` and returns `HostedSession`. PublicPay persists after HTTP. SETTLED vs paid is a **parse** leftover (07); this phase is only retry-duplicate session.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditHosted.cs` | Create hosted URL |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Xendit/XenditWebhook.cs` | Parse — do not mix into this PR |
| `apps/lazuar-pay/src/Lazuar.Pay/PublicPay/PublicPayEndpoints.cs` | Persist after HTTP |
| Xendit tests under `apps/lazuar-pay/tests/` | Paid path |

**Current (`6d730d15`):** HTTP then persist.

---

## G12.1

- [x] Xendit idempotency header (`Idempotency-key` is their usual) keyed on `checkout.Id` **or** persist-before-HTTP
- [x] FakePsp retry test
- [x] Do not change SETTLED mapping in this phase

## G12.2 Exit

- [x] Unblocked for G13
