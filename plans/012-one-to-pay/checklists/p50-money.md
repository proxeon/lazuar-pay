# P50 — Money / S1 (parked)

**Do not start until C99.**  
**Analysis:** 011/01, 011/12 steps 8–12

---

## P50.1 Door

- [x] `POST /v1/checkouts` and status GET on **Pay** `/v1`, not Hub `/public/commerce/*` (fixture, `status: open`)
- [x] Tenant/org is One tenant id from whoami/authz
- [ ] Buyer pays **without** a One account

## P50.2 Still out

- [ ] Homemade LHDN
- [ ] Stripe Billing `subscription.updated` as SoT
- [ ] Fixture strings that do not match JSON
- [ ] Stubbing all of ops to mimic Hub

## P50.3 Fixtures

- [x] If stubbing first: JSON fixtures on `/v1/checkouts`, not 100 Hub paths. Hosted page, rails, journal, `RCPT-` still out.
