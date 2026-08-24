# H21 — IsolationTests ban Hub adapter type names

**Track:** Harden · **Depends:** A00  
**Analysis:** [00](../00-what-must-be-done.md) §3.5  
**IDs:** NP-XX-009  
**Goal:** A well-meaning port cannot recreate the factory inside `Lazuar.Pay` while IsolationTests stay green.

---

## H21.1 Grep in Pay src

- [x] Extend `apps/lazuar-pay/tests/Lazuar.Pay.Tests/IsolationTests.cs`
- [x] Fail if any `src/**/*.cs` contains:
  - `IPaymentGatewayAdapter`
  - `PaymentGatewayFactory`
  - `IPaymentGatewayFactory`
  - `AddPaymentsModule`
  - `GatewayPaymentCompletedIntegrationEvent`
  - `Modules.Payments`
- [x] Keep existing bans: `MediatR`, `BuildingBlocks`, `Modules.One`, `lazuar-api`, org/user/member tables

## H21.2 csproj

- [x] Host and test csproj still have no `ProjectReference` to `apps/lazuar-api`
- [x] Do not add `Razorpay.Api` (R14)

## H21.3 Exit

- [x] IsolationTests fail a deliberate string if you add it in a scratch test — then remove the scratch
- [x] Unblocked for P25, P26
