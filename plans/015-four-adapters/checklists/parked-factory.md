# Parked — IPaymentGatewayAdapter factory of five

**Do not start in 015 — this is refuse, not later.**  
**Analysis:** [00](../00-what-must-be-done.md) §3.4 / §9; P25, P26, H21

---

- [ ] Do not add `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IEnumerable` lookup
- [ ] Do not ProjectReference `Modules.Payments`
- [ ] Do not `AddPaymentsModule`
- [ ] A switch of five **known** names is the allowed dispatch
- [ ] IsolationTests must keep failing those type names
