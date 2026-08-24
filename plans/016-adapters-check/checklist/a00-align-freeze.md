# A00 — Align and freeze

**Track:** Program · **Depends:** none  
**Analysis:** [`../00-evaluation.md`](../00-evaluation.md) §1 / §10; [`../10-honesty-frontend-risks.md`](../10-honesty-frontend-risks.md) §8  
**IDs:** —  
**Goal:** Lock 016. Fill `decisions.md`. No product code.

---

## A00.1 Read first

- [ ] Read `plans/016-adapters-check/00-evaluation.md` in full (parent judgment)
- [ ] Read `plans/016-adapters-check/09-tests-inventory.md` §9 (strengthen) and §10 (one method per gap)
- [ ] Read `plans/016-adapters-check/10-honesty-frontend-risks.md` §1 (014 P0s) and §6 (new P0-A…E)
- [ ] Open live `PublicPayEndpoints.Start` — always `CreateHostedUrlAsync`, overwrites `ProviderSessionId`
- [ ] Open live `OneWebhookEndpoints` — body-only uppercase hex HMAC
- [ ] Open live `Fulfillment.FulfillPaidAsync` — no `ChargesPaused` read
- [ ] Open live `RazorpayWebhook` — `notes.checkout_id` only
- [ ] Open live `BillplzWebhook` — `Currency = "MYR"`
- [ ] Open live `StripeWebhook.ResolveSecret` — fallback every non-Production
- [ ] Open live `SecretBox.LoadKey` — git wrap string every non-Production
- [ ] Confirm IsolationTests still ban factory / MediatR / `Razorpay.Api`
- [ ] Confirm 015 parked files still parked; this program does not un-park them

## A00.2 Fill `decisions.md`

- [ ] Start second click = **return existing URL**
- [ ] Pause + paid webhook = **do not fulfill, do not consume paid event id**
- [ ] Razorpay missing notes = **join plink_ else 400**
- [ ] Wrap key outside Testing = **throw**
- [ ] Stripe process `whsec_` = **Testing only**
- [ ] Checkout default origin = **`Pay:CheckoutBaseUrl`**

## A00.3 What 016 does not reverse

- [ ] Five lowercase names, capability `hosted_link`, one `active_provider`
- [ ] Tax out. Official Receipt. Two-line GMV
- [ ] No `IPaymentGatewayAdapter` / factory / registrar / DNS fallback
- [ ] Buyers are not One humans. `:5179` public
- [ ] Hermetic `task pay:test`

## A00.4 Must not

- [ ] No product code in this phase
- [ ] Do not start Track F as the first PR
- [ ] Do not add a sixth provider name
- [ ] Do not retarget `lazuar-ops` / `lazuar-portal`
- [ ] Do not bind 8080
- [ ] Do not ProjectReference `apps/lazuar-api`
- [ ] Do not flip 011/11 from this phase

## A00.5 Exit

- [ ] [`decisions.md`](./decisions.md) filled table has no blanks
- [ ] Unblocked for I10 (and W10, Y10, J10, D10, E10, L10 in parallel after I10)
