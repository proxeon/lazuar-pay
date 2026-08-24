# P26 — No PaymentGatewayFactory

**Track:** Provider door · **Depends:** H21, P25  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** A switch of five **known** names is allowed. `IEnumerable<IHostedRail>` lookup of unused names is how Hub grew five adapters.

---

## P26.1

- [x] Do not add `PaymentGatewayFactory` / `IPaymentGatewayFactory`
- [x] Do not `GetAdapter(string)` over a DI list
- [x] `Program.cs` may `AddScoped<StripeHosted>()` and later `AddScoped<ChipHosted>()` **concrete**
- [x] Webhook/start `switch` is the dispatch

## P26.2 Must not

- [x] Do not register Billplz “disabled” while implementing CHIP
- [x] Do not add keyed services for unused names

## P26.3 Exit

- [x] H21 grep includes `PaymentGatewayFactory`
- [x] Unblocked for P27
