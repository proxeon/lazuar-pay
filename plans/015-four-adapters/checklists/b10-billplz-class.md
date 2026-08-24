# B10 — BillplzHosted class

**Track:** Billplz · **Depends:** C32 pattern, P27, H12  
**Analysis:** [00](../00-what-must-be-done.md) §5.2  
**IDs:** NP-LAT / reminder-only wrap  
**Goal:** Small class. Hub `BillplzGatewayAdapter.cs` is judgment only.

---

## B10.1

- [ ] `apps/lazuar-pay/src/Lazuar.Pay/Gateways/BillplzHosted.cs`
- [ ] `Provider = "billplz"`
- [ ] `CreateHostedUrlAsync` returns bill `url`
- [ ] `AddScoped<BillplzHosted>()` + HttpClient
- [ ] No `IPaymentGatewayAdapter`

## B10.2 Must not

- [ ] Do not port `ChargeOffSessionAsync` (Hub returns false)
- [ ] Do not port `IssueRefundAsync` (Hub returns false — Payment Order is a disbursement)
- [ ] Do not port `PublicDnsFallback`

## B10.3 Exit

- [ ] Class compiles
- [ ] Unblocked for B11
