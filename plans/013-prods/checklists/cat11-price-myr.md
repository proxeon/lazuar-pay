# CAT11 — Price on a product, currency MYR

**Track:** Catalog · **Depends:** CAT10, D21  
**Analysis:** [01](../01-production-ready-bar.md) §3.1.2, [04](../04-merchant-frontend.md) Screen C  
**Goal:** At least one price (NP-CAT-002). Currency MYR (NP-CAT-003). Amount `> 0`.  
**011:** NP-CAT-002, NP-CAT-003

---

## CAT11.1 Attach

- [ ] Attach price to a CAT10 product (nested POST or body on create — one door)
- [ ] Persist on D21 `prices`
- [ ] Interval: at least **one** of `mo` / `yr` / `one_off` (both mo+yr not required for first product)
- [ ] `amount` **> 0** else 400
- [ ] Bar B may ship qty=1; seats (NP-CAT-004) are not a gate

## CAT11.2 Currency

- [ ] Default **MYR** if omitted (same as fixture checkouts)
- [ ] Bar B: **only MYR** — reject other currency with 400 (do not silently store USD)
- [ ] Store uppercase `MYR`

## CAT11.3 Authz

- [ ] Same write policy as CAT10 (MemberGate + M24 owner/admin)
- [ ] `member` cannot attach a price (403)

## CAT11.4 Must not

- [ ] No Hub `/admin/commerce` price catalogs
- [ ] No SST/TIN fields as a legal feature
- [ ] No Stripe Price id as Pay SoT

## CAT11.5 Exit

- [ ] Product with one MYR price round-trips
- [ ] Unblocked for CAT13 create form to offer a price (CAT12 may already be open)
