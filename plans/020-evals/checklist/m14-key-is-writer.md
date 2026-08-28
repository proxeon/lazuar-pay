# M14 — Bound key is Pay writer of that org

**Track:** M · **Depends:** M13  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) freeze “typical key role is member”; [`checklist/decisions.md`](./decisions.md)  
**Goal:** `POST /v1/checkouts` with `lzr_sk_` → **201**.

**Why:** One’s `/me` for a typical key projects `role: member`. `RequireWriterAsync` then 403s `Writer role required` even if M12 skipped `authz/check`. Freeze: the human who minted the key already bound it to one tenant; that is Pay writer for that org. Do not require `admin`/`*` on the key.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/MemberGate.cs` | `RequireWriterAsync` — `/me` role `owner`/`admin` + `status=active` |
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | Writer mint `POST /v1/checkouts` |
| `apps/lazuar-pay/src/Lazuar.Pay/PaymentLinks/PaymentLinkEndpoints.cs` | Writer mint links |
| `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` | Writer products |
| `apps/lazuar-pay/src/Lazuar.Pay/Credentials/GatewayEndpoints.cs` | Writer vault |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Checkouts/CheckoutTests.cs` | Owner JWT 201; member 403 |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Infrastructure/PayTest.cs` | `Owner` fixture |

**Current (`6d730d15`):** Writer = JWT overlay only. Keys never reach this overlay because hop 1 400s (M12). After M12, overlay would still 403 `member`.

---

## M14.1 Writer

- [ ] `RequireWriterAsync` for `lzr_sk_`: after M13 member/bound/active, **return allow**
- [ ] Do **not** demand `/me.tenants[].role` is `owner` or `admin` for keys
- [ ] JWT writer overlay **unchanged** (`owner`/`admin` + active)

## M14.2 Mint

- [ ] Key `POST /v1/checkouts` `{ org_id: t1, amount, provider: test }` → 201 (Testing)
- [ ] `org_id` in body must match bound tenant (existing mint checks)

## M14.3 Tests

- [ ] `Key_member_role_can_create_checkout` → 201
- [ ] Vault PUT with key also 200 if you keep writer=key (same rule). If you split “vault still JWT-only”, write that exception **here** and test 403 — **default is key is writer for all writer doors**

## M14.4 Must not

- [ ] Do not add One scope `payments.checkouts:write`
- [ ] Do not treat key as platform admin

## M14.5 Exit

- [ ] Unblocked for M15, U11, E13
