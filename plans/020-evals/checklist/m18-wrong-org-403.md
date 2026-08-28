# M18 — Cross-org key is 403

**Track:** M · **Depends:** M14  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) §10.4 tests 4, 16  
**Goal:** One key, one shop.

**Why:** After M14 a key is a writer. Without a cross-org test, a bug that uses the first tenant in `/me` instead of the **path** org would mint money on the wrong shop.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Checkouts/CheckoutEndpoints.cs` | Body `org_id` + writer gate |
| `apps/lazuar-pay/src/Lazuar.Pay/Catalog/CatalogEndpoints.cs` | Path `{orgId}` |
| `apps/lazuar-pay/src/Lazuar.Pay/Money/Queries/PaymentQueryEndpoints.cs` | `Get_receipt_other_org_is_403` |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Money/PaymentQueryTests.cs` | t2 `authz/check` allowed:false |

**Current (`6d730d15`):** Cross-org 403 is proven for JWT. Not for keys.

---

## M18.1

- [x] Key `/me.tenants[0].id == t1`
- [x] `POST /v1/checkouts` body `org_id: t2` → 403
- [x] `POST /v1/orgs/t2/products` → 403
- [x] `GET /v1/orgs/t2/receipts` → 403 (member door, same bound-tenant rule)

## M18.2 Tests

- [x] `Key_bound_to_other_tenant_is_403` on mint and on list
- [x] Do not 404 (existence oracle)

## M18.3 Exit

- [x] Unblocked for M19
