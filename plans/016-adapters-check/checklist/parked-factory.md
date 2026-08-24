# Parked — IPaymentGatewayAdapter factory of five

**Do not start in 016 — this is refuse, not later.**  
**Analysis:** [`../00-evaluation.md`](../00-evaluation.md) §7.3; 015 `parked-factory.md`

---

- [ ] Do not add `IPaymentGatewayAdapter` / `PaymentGatewayFactory` / `IEnumerable<IHostedRail>` lookup
- [ ] Do not ProjectReference `Modules.Payments`
- [ ] Do not `AddPaymentsModule`
- [ ] A switch of five **known** names remains the allowed dispatch
- [ ] IsolationTests must keep failing those type names
- [ ] `IFulfillPaid` (G11) is **not** a gateway factory — keep it one method, Pay-local
