# P13 — Cache `/me` only with `api_key.revoked` (parked)

**Track:** Parked  
**Analysis:** [`../02-machine-keys-m2m.md`](../02-machine-keys-m2m.md) NP-ONE-006  
**Unpark when:** `/me` volume on mint is a measured problem. Hatch is uncached (M19).

**Why parked:** Caching without revoke HMAC keeps a deleted key alive. One publishes `api_key.revoked`. Pay Plane A does not handle that type today.

**Related files**

| Path | Role today |
|------|------------|
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/OneWebhooks/OneWebhookEndpoints.cs` | `tenant.suspended` / `reactivated` only |
| `apps/lazuar-pay/src/Lazuar.Pay/Identity/Client/OneClient.cs` | `/me` every request |
| M19 | Uncached 401 |
| Sibling One `ApiKeyService` | Event catalog |

**Current (`6d730d15`):** No cache. Plane A ignores unknown types after recording delivery id.

---

## P13.1 When unparking

- [ ] Cache key → whoami projection with TTL
- [ ] Plane A `api_key.revoked` invalidates by `key_id` (not prefix uniqueness)
- [ ] Tests: revoke event then mint 401
