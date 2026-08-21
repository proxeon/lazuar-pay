# F19 — List payments (member-gated)

**Track:** Fulfillment · **Depends:** F12  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-FUL-003  
**Goal:** `GET /v1/orgs/{orgId}/payments` lists paid checkouts for that org.

---

## F19.1 Route

- [x] `GET /v1/orgs/{orgId}/payments` on 8081
- [x] Member gate (`authz/check member`) — same as other merchant `/v1` org routes
- [x] Other org / not a member → **403** (not 200 empty as a leak)
- [x] Missing Bearer → 401
- [x] `{orgId}` in path is SoT; `X-Lazuar-Tenant-Id` is a hint

## F19.2 Body

- [x] snake_case. Amount, currency, status, receipt number if any, payer email
- [x] Do not return other orgs’ rows
- [x] Not Hub `/admin/billing/ledger`

## F19.3 Exit

- [x] Hermetic 200 / 401 / 403
- [x] Unblocked for F21 and F23
