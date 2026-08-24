# H21 — IsolationTests ban Hub adapter type names

**Track:** Harden · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.5  
**IDs:** NP-XX-009  
**Goal:** A well-meaning port cannot recreate the factory inside `Lazuar.Pay` while IsolationTests stay green.

---

## H21.1 Grep in Pay src

- [ ] Extend `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- [ ] Fail if any `src/**/*.cs` contains:
  - `IPaymentGatewayAdapter`
  - `PaymentGatewayFactory`
  - `IPaymentGatewayFactory`
  - `AddPaymentsModule`
  - `GatewayPaymentCompletedIntegrationEvent`
  - `Modules.Payments`
- [ ] Keep existing bans: `MediatR`, `BuildingBlocks`, `Modules.One`, `lazuar-api`, org/user/member tables

## H21.2 csproj

- [ ] Host and test csproj still have no `ProjectReference` to `apps/lazuar-api`
- [ ] Do not add `Razorpay.Api` (R14)

## H21.3 Exit

- [ ] IsolationTests fail a deliberate string if you add it in a scratch test — then remove the scratch
- [ ] Unblocked for P25, P26
