# G24 — One live provider class

**Track:** Rails · **Depends:** G16  
**Analysis:** [06](../06-money-rails.md) §2.1 / §3 / §9  
**Goal:** One rail in process. Not the Hub factory of five.

---

## G24.1 Live set

- [x] Only **one** provider class is live — the G10 name (`Stripe…` **or** `Chip…`)
- [x] Two functions are enough: `CreateHostedCheckout` + `ParseWebhook`
- [x] Comment listing **parked** rails: Billplz, Razorpay, Xendit (and the XOR’d G10 loser)
- [x] Do not register a factory that can resolve five names

## G24.2 Isolation

- [x] IsolationTests still **no MediatR** / `Modules.` / `BuildingBlocks` / `lazuar-api`
- [x] Do not copy `IPaymentGatewayAdapter` “for later”
- [x] Do not add `AddPaymentsModule`

## G24.3 Must not

- [x] Do not implement both Stripe and CHIP “while we are here”
- [x] Do not tick `NP-GW-002` because CHIP showed a card form

## G24.4 Exit

- [x] Grep/read: one live adapter type
- [x] Unblocked for G25
