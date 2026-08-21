# F19 — List payments (member-gated)

**Track:** Fulfillment · **Depends:** F12  
**Analysis:** [07](../07-fulfillment-ledger-docs.md)  
**IDs:** NP-FUL-003  
**Goal:** `GET /v1/orgs/{orgId}/payments` lists paid checkouts for that org.

---

## F19.1 Route

- [ ] `GET /v1/orgs/{orgId}/payments` on 8081
- [ ] Member gate (`authz/check member`) — same as other merchant `/v1` org routes
- [ ] Other org / not a member → **403** (not 200 empty as a leak)
- [ ] Missing Bearer → 401
- [ ] `{orgId}` in path is SoT; `X-Lazuar-Tenant-Id` is a hint

## F19.2 Body

- [ ] snake_case. Amount, currency, status, receipt number if any, payer email
- [ ] Do not return other orgs’ rows
- [ ] Not Hub `/admin/billing/ledger`

## F19.3 Exit

- [ ] Hermetic 200 / 401 / 403
- [ ] Unblocked for F21 and F23
