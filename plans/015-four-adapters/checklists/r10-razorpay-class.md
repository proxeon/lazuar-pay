# R10 — RazorpayHosted class

**Track:** Razorpay · **Depends:** P27, H12, R14  
**Analysis:** [00](../00-what-must-be-done.md) §5.4  
**IDs:** NP-LAT-002  
**Goal:** Payment **link**, not invoice, not e-mandate. Hub `RazorpayGatewayAdapter` judgment only.

---

## R10.1

- [ ] `Gateways/RazorpayHosted.cs`, `Provider = "razorpay"`
- [ ] HttpClient to `https://api.razorpay.com`
- [ ] `CreateHostedUrlAsync` returns `short_url`
- [ ] No `Razorpay.Api` package (R14)
- [ ] No `ChargeOffSession` (R23)

## R10.2 Exit

- [ ] Class compiles
- [ ] Unblocked for R11
