# P25 — Do not add IPaymentGatewayAdapter

**Track:** Provider door · **Depends:** H21  
**Analysis:** [00](../00-what-must-be-done.md) §3.4 / §9  
**IDs:** NP-XX-009  
**Goal:** Hub’s five-method port stays in the museum.

---

## P25.1 Refuse these methods on day one of CHIP

- [ ] Do not add `GenerateCheckoutAsync` / `ParseWebhookAsync` / `IssueRefundAsync` / `GenerateCustomerPortalAsync` / `ChargeOffSessionAsync` as a required interface
- [ ] `CreateHostedUrlAsync` is enough (P27)
- [ ] Parse lives next to the webhook route or a `TryParsePaid` helper — not Hub’s `GatewayWebhookParsedResult` with taxRate/fxRate

## P25.2 Must not

- [ ] Do not copy `Modules.Payments.Application.Ports/IPaymentGatewayAdapter.cs`
- [ ] Do not add `taxAmount` / `setupFutureUsage` parameters “for later”

## P25.3 Exit

- [ ] H21 grep stays green after CHIP class lands
