# P26 — No PaymentGatewayFactory

**Track:** Provider door · **Depends:** H21, P25  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** A switch of five **known** names is allowed. `IEnumerable<IHostedRail>` lookup of unused names is how Hub grew five adapters.

---

## P26.1

- [ ] Do not add `PaymentGatewayFactory` / `IPaymentGatewayFactory`
- [ ] Do not `GetAdapter(string)` over a DI list
- [ ] `Program.cs` may `AddScoped<StripeHosted>()` and later `AddScoped<ChipHosted>()` **concrete**
- [ ] Webhook/start `switch` is the dispatch

## P26.2 Must not

- [ ] Do not register Billplz “disabled” while implementing CHIP
- [ ] Do not add keyed services for unused names

## P26.3 Exit

- [ ] H21 grep includes `PaymentGatewayFactory`
- [ ] Unblocked for P27
