# G14 — `member` cannot PUT keys

**Track:** Rails · **Depends:** G12  
**Analysis:** [06](../06-money-rails.md) §4.6, [012/07](../../012-one-to-pay/07-authz-roles.md)  
**IDs:** NP-ONE-021  
**Goal:** `NP-ONE-021` on key write. One has no `viewer` role — **member** is the deny.

---

## G14.1 Law

- [x] One membership is `owner` \| `admin` \| `member` only. Do not invent Pay `VIEWER`
- [x] Dummy `/v1/orgs/{orgId}/ready` `check(member)` is **not** this gate
- [x] PUT keys = `authz/check` **admin** (owner has admin)

## G14.2 Test (one each)

- [x] `member` Bearer on G12 PUT → **403**. Secret not stored
- [x] `owner` or `admin` Bearer → **204** or **200**
- [x] Missing Bearer → **401**; One `authz/check` **not** called
- [x] Fake One: assert relation is **admin**, `object.type=tenant`, `object.id` = path `orgId`

## G14.3 Must not

- [x] Must not treat `check(member)` as VIEWER-cannot-charge and tick `NP-ONE-021` done
- [x] Must not add FGA type `payment`

## G14.4 Exit

- [x] Cases in G14.2 green under `task pay:test`
- [x] `NP-ONE-021` may move **for key paste only** (charge/refund still paper 07 / O)
- [x] Unblocked for G16
