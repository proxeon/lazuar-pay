# C24 — VIEWER honesty (no fake gate)

**Track:** Authz · **Depends:** C20  
**Analysis:** [07](../07-authz-roles.md)  
**Goal:** Do not implement NP-ONE-021 as `check(member)`.

---

## C24.1 Facts to encode in comments / README (not a One PR)

- [x] One tenant membership roles are `owner` \| `admin` \| `member` only
- [x] OpenFGA `viewer` is on type **`app`**, not staff read-only on the merchant tenant
- [x] Dummy `/ready` uses `member` — that is “has the tenant”, not “cannot charge”

## C24.2 Must not

- [x] Must not map Hub `VIEWER` onto One `member` and call it done
- [x] Must not add FGA type `payment` in One for this program
- [x] Must not flip NP-ONE-021 to `done`

## C24.3 Later (note only)

- [x] README one paragraph: money routes will need Pay-side enforcement (e.g. only `admin`/`owner` change keys) until One ships a staff read-only role

## C24.4 Exit

- [x] Honesty is in `apps/lazuar-pay/README.md` (short)
- [x] Unblocked for C99
