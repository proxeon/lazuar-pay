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

## A99.2 All five names in code, one active at a time

- [ ] Hermetic tests exist for `stripe`, `chip`, `billplz`, `xendit`, `razorpay`: paid + replay + not-paid
- [ ] PUT unknown provider still 400
- [ ] Buyer page has no PSP dropdown

## A99.3 Tax still out

- [ ] Fulfillment has no SST throw
- [ ] No tax journal line
- [ ] Receipt title Official Receipt
- [ ] No LHDN types under `apps/lazuar-pay`

## A99.4 Cathedral still banned

- [ ] IsolationTests still fail `MediatR` / `Modules.` / `BuildingBlocks` / `lazuar-api` refs
- [ ] IsolationTests fail `IPaymentGatewayAdapter` and `PaymentGatewayFactory` in Pay `src/`
- [ ] No `Razorpay.Api` package unless A00 was amended

## A99.5 Still not done (must remain explicit)

- [ ] Refunds, off-session, CHIP registrar, `PublicDnsFallback`, LHDN, Hub cutover, SST×seats — parked files still parked
- [ ] Do not claim Hub replaced; root compose may still boot `lazuar-api`

## A99.6 Tracker

- [ ] Flip only IDs listed in phase Exits that were actually proven
- [ ] Do not mark “Pay v1 complete”

## A99.7 Exit

- [ ] PR / note says **015 four hosted_link rails, tax out**, not “payments module ported”
