# P60 — Old ops / portal (parked refuse for C-phases)

**Do not retarget in this program.**  
**Analysis:** [04](../04-pay-spec-contract.md), [10](../10-dogfood-and-tests.md)

---

## P60.1 Keep

- [ ] `lazuar-ops` `VITE_API_URL` → Hub `http://localhost:8080/api/v1`
- [ ] `lazuar-portal` same
- [ ] `@repo/api-types-ts` stays Hub’s spec

## P60.2 Why 8081 would fail today

- [ ] Ops `POST /one/auth/login` is Hub homemade IdP — Pay must not implement it
- [ ] Ops `GET /one/auth/me` is not One `GET /me`
- [ ] Hundreds of `/admin/commerce`, `/lhdn`, `/ops/chat` routes are not Consumer-0

## P60.3 Later (new clients)

- [ ] New merchant UI: OIDC to One (P10) + Pay `/v1` (whoami, then money). Origin is `lazuar-pay-merchant` `:5178` (scaffold; OIDC unwired)
- [ ] Hosted checkout origin is `lazuar-pay-checkout` `:5179` (not `lazuar-portal`)
- [ ] Generate `@repo/pay-types-ts` only when that UI calls `/v1` for real
- [ ] Do not mix Hub OpenAPI honesty CI with Pay
