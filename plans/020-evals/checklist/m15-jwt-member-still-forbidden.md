# M15 — Human member still cannot mint

**Track:** M · **Depends:** M14  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4 test 6  
**Goal:** Key-as-writer must not regress JWT member.

**Why:** M14 treats a key with synthetic `/me.role=member` as writer. If the branch is “role is member → writer”, human members could mint. The branch must be **prefix `lzr_sk_`**, not the role string.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | JWT writer overlay |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Credentials/GatewayTests.cs` | `Member_cannot_put_gateway` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs` | Member cannot mint |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/PaymentLinks/PaymentLinkTests.cs` | Member vs admin vs suspended |

**Current (`6d730d15`):** Human member 403 on writer doors is tested and must stay.

---

## M15.1 Overlay

- [ ] JWT `role: member` + `authz/check` allowed → writer doors **403** `Writer role required`
- [ ] JWT `owner` / `admin` still 201 on mint

## M15.2 Tests

- [ ] Existing `Member_cannot_put_gateway` still 403
- [ ] Existing member cannot `POST /v1/checkouts` still 403
- [ ] New: same factory, key fixture 201 **and** member JWT 403 (do not share one Responder that always allows)

## M15.3 Must not

- [ ] Do not infer “machine” from missing JWT claims on a random Bearer string — prefix `lzr_sk_` only

## M15.4 Exit

- [ ] Unblocked for M16
