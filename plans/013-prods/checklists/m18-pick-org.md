# M18 — Pick org

**Track:** Merchant · **Depends:** M17  
**Analysis:** [04](../04-merchant-frontend.md)  
**Goal:** Selected One tenant id is Pay `org_id` in the path. Header is a hint only.  
**011:** NP-ONE-007

---

## M18.1 Selection

- [x] Selecting a tenant sets the path (`org` id in the URL)
- [x] May set `X-Lazuar-Tenant-Id` as a **hint only** (One name, not Hub `X-Tenant-Id`)
- [x] Subsequent money routes use org id in **path/body**

## M18.2 Authz

- [x] Header must **not** authorize (already host C23)
- [x] No cookie as authz (`lazuar_auth`, `lazuar_active_tenant`, or a new Pay cookie)
- [x] Path + One membership remain SoT

## M18.3 Persistence

- [x] Active-org hint may live in `sessionStorage` as UX only
- [x] Do not treat a stored id as membership

## M18.4 Exit

- [x] Picker navigates by org id; header is optional and non-authorizing
- [x] Unblocked for M19
