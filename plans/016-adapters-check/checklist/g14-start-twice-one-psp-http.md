# G14 — Two starts, one PSP HTTP

**Track:** Prove Beat 1 · **Depends:** I10, I11, I12  
**Analysis:** [`../09-tests-inventory.md`](../09-tests-inventory.md) T1  
**IDs:** P0-A  
**Goal:** Double-click is proven, not a comment.

---

## G14.1 Method `PublicPayTests.Start_twice_returns_same_url_without_second_psp_http` (CHIP)

- [ ] PUT chip + brand + PEM, FakePsp 200 `{id, checkout_url}`
- [ ] Seed checkout, start with email twice
- [ ] Both 200, same `redirect_url`
- [ ] `Psp` send count **1** (or LastUri unchanged after first)
- [ ] `ProviderSessionId` still `purch_1` (or stub id)

## G14.2 Optional Billplz clone

- [ ] Same idea on billplz — **not** required if CHIP is green; do not skip CHIP

## G14.3 Must not

- [ ] Do not use Stripe.net for this
- [ ] Do not assert 409 as the success path (I10 returns URL)

## G14.4 Exit

- [ ] Green
