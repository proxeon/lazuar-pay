# A00 — Align and freeze

**Track:** Program · **Depends:** none  
**Analysis:** [00](../00-what-must-be-done.md) §0–§1  
**IDs:** —  
**Goal:** Lock 015. Fill `decisions.md`. No product code.

---

## A00.1 Read first

- [ ] Read `plans/015-four-adapters/00-what-must-be-done.md` in full
- [ ] Read `plans/014-evals/00-evaluation.md` P0 list (`whsec` process-wide, two-TX insert, SST throw, One HMAC dialect, member `POST /v1/checkouts`, setup-not-paid untested)
- [ ] Read `plans/014-evals/09-porting-architecture.md` (no factory; `CreateHostedUrl` + parse)
- [ ] Open live `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` (400 `"Bar B first rail is stripe"`)
- [ ] Open live `Gateways/StripeHosted.cs` and `Gateways/WebhookEndpoints.cs`
- [ ] Open live `Money/Fulfillment.cs` SST throw
- [ ] Confirm IsolationTests still ban `MediatR`, `BuildingBlocks`, `Modules.`, `lazuar-api`

## A00.2 Fill `decisions.md`

- [ ] Remaining four = `chip`, `billplz`, `xendit`, `razorpay` (Stripe already on 8081)
- [ ] Tax = **out**
- [ ] One `active_provider` per org = yes
- [ ] Razorpay transport = **HttpClient**
- [ ] CHIP webhooks = dashboard PEM paste, not registrar
- [ ] Public callback base config name = `Pay:PublicBaseUrl`

## A00.3 Amend 013 in writing (do not silently edit 013)

- [ ] 013 row Rails “not five adapters” is amended **here**, not deleted from 013 history
- [ ] 013 row SST “fail closed” is amended **here**: tax is out of this program
- [ ] Do not un-refuse `NP-XX-001` (LHDN), `NP-XX-011` (FPX e-mandate), `NP-XX-012` (Stripe Billing SoT)

## A00.4 Must not

- [ ] No product code in this phase
- [ ] Do not start CHIP HTTP before T + S + H
- [ ] Do not add `IPaymentGatewayAdapter` as a “temporary” seam
- [ ] Do not retarget `lazuar-ops` / `lazuar-portal`
- [ ] Do not bind 8080

## A00.5 Exit

- [ ] [`decisions.md`](./decisions.md) filled table has no blanks
- [ ] Unblocked for T10 and S10
