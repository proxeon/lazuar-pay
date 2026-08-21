# G24 — One live provider class

**Track:** Rails · **Depends:** G16  
**Analysis:** [06](../06-money-rails.md) §2.1 / §3 / §9  
**Goal:** One rail in process. Not the Hub factory of five.

---

## G24.1 Live set

- [ ] Only **one** provider class is live — the G10 name (`Stripe…` **or** `Chip…`)
- [ ] Two functions are enough: `CreateHostedCheckout` + `ParseWebhook`
- [ ] Comment listing **parked** rails: Billplz, Razorpay, Xendit (and the XOR’d G10 loser)
- [ ] Do not register a factory that can resolve five names

## G24.2 Isolation

- [ ] IsolationTests still **no MediatR** / `Modules.` / `BuildingBlocks` / `lazuar-api`
- [ ] Do not copy `IPaymentGatewayAdapter` “for later”
- [ ] Do not add `AddPaymentsModule`

## G24.3 Must not

- [ ] Do not implement both Stripe and CHIP “while we are here”
- [ ] Do not tick `NP-GW-002` because CHIP showed a card form

## G24.4 Exit

- [ ] Grep/read: one live adapter type
- [ ] Unblocked for G25
