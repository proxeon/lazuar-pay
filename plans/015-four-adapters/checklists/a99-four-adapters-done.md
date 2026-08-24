# A99 — Definition of done

**Track:** Program · **Depends:** Q17, U21, K17, C32, B28, X23, R25  
**Analysis:** [00](../00-what-must-be-done.md) §10–§11  
**IDs:** NP-GW-003, NP-LAT-002 (conditional)  
**Goal:** Honest close of 015. Not Hub dark. Not tax. Not off-session.

---

## A99.1 Lived sentence (not only unit tests)

- [ ] Merchant signs in on `:5178` via One
- [ ] Merchant sets **one** active provider and pastes that rail’s secrets (encrypted)
- [ ] Buyer opens `:5179/c/{token}` with **no** One account
- [ ] Buyer pays on the **processor hosted page**
- [ ] Pay shows one `RCPT-` and a balanced two-line journal
- [ ] Webhook retry no-ops
- [ ] `member` can see the payment; cannot paste keys; cannot `POST /v1/checkouts`

Hermetic `task pay:test` covers the money path with fake PSP HTTP. A99.1 stays open until a human loop.

## A99.2 All five names in code, one active at a time

- [x] Hermetic tests exist for `stripe`, `chip`, `billplz`, `xendit`, `razorpay`: paid + replay + not-paid
- [x] PUT unknown provider still 400
- [x] Buyer page has no PSP dropdown

## A99.3 Tax still out

- [x] Fulfillment has no SST throw
- [x] No tax journal line
- [x] Receipt title Official Receipt
- [x] No LHDN types under `apps/lazuar-pay`

## A99.4 Cathedral still banned

- [x] IsolationTests still fail `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api` refs
- [x] IsolationTests fail `IPaymentGatewayAdapter` and `PaymentGatewayFactory` in Pay `src/`
- [x] No `Razorpay.Api` package unless A00 was amended

## A99.5 Still not done (must remain explicit)

- [x] Refunds, off-session, CHIP registrar, `PublicDnsFallback`, LHDN, Hub cutover, SST×seats — parked files still parked
- [x] Do not claim Hub replaced; root compose may still boot `lazuar-api`

## A99.6 Tracker

- [ ] Flip only IDs listed in phase Exits that were actually proven (011/11 not flipped from this branch)
- [x] Do not mark “Pay v1 complete”

## A99.7 Exit

- [x] PR / note says **015 four hosted_link rails, tax out**, not “payments module ported”
