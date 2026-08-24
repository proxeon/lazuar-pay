# A00 — Align and freeze

**Track:** Program · **Depends:** none  
**Analysis:** [00](../00-what-must-be-done.md) §0–§1  
**IDs:** —  
**Goal:** Lock 015. Fill `decisions.md`. No product code.

---

## A00.1 Read first

- [x] Read `plans/015-four-adapters/00-what-must-be-done.md` in full
- [x] Read `plans/014-evals/00-evaluation.md` P0 list (`whsec` process-wide, two-TX insert, SST throw, One HMAC dialect, member `POST /v1/checkouts`, setup-not-paid untested)
- [x] Read `plans/014-evals/09-porting-architecture.md` (no factory; `CreateHostedUrl` + parse)
- [x] Open live `apps/lazuar-pay/src/Lazuar.Pay/Gateways/GatewayEndpoints.cs` (400 `"Bar B first rail is stripe"`)
- [x] Open live `Gateways/StripeHosted.cs` and `Gateways/WebhookEndpoints.cs`
- [x] Open live `Money/Fulfillment.cs` SST throw
- [x] Confirm IsolationTests still ban `MediatR`, `BuildingBlocks`, `Modules.`, `lazuar-api`

## A00.2 Fill `decisions.md`

- [x] Remaining four = `chip`, `billplz`, `xendit`, `razorpay` (Stripe already on 8081)
- [x] Tax = **out**
- [x] One `active_provider` per org = yes
- [x] Razorpay transport = **HttpClient**
- [x] CHIP webhooks = dashboard PEM paste, not registrar
- [x] Public callback base config name = `Pay:PublicBaseUrl`

## A00.3 Amend 013 in writing (do not silently edit 013)

- [x] 013 row Rails “not five adapters” is amended **here**, not deleted from 013 history
- [x] 013 row SST “fail closed” is amended **here**: tax is out of this program
- [x] Do not un-refuse `NP-XX-001` (LHDN), `NP-XX-011` (FPX e-mandate), `NP-XX-012` (Stripe Billing SoT)

## A00.4 Must not

- [x] No product code in this phase
- [x] Do not start CHIP HTTP before T + S + H
- [x] Do not add `IPaymentGatewayAdapter` as a “temporary” seam
- [x] Do not retarget `lazuar-ops` / `lazuar-portal`
- [x] Do not bind 8080

## A00.5 Exit

- [x] [`decisions.md`](./decisions.md) filled table has no blanks
- [x] Unblocked for T10 and S10
