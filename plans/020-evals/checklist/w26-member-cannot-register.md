# W26 — Member cannot register Plane C

**Track:** W · **Depends:** W14  
**Goal:** Same writer rule as vault.

**Why:** A member who can set the outbound URL can steal payment events (PII: payer email/name in `data`). Writer only. GET is member (ops visibility).

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs` | `Member_cannot_put_gateway` |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | Writer overlay |
| W14 | PUT |

**Current (`6d730d15`):** N/A.

---

## W26.1 Tests

- [x] JWT member PUT `/v1/orgs/t1/webhooks` → 403
- [x] JWT owner → 200
- [x] After M14: bound key → 200
- [x] GET allowed for member

## W26.2 Exit

- [x] Unblocked for W27
