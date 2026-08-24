# R10 — RazorpayHosted class

**Track:** Razorpay · **Depends:** P27, H12, R14  
**Analysis:** [00](../00-what-must-be-done.md) §5.4  
**IDs:** NP-LAT-002  
**Goal:** Payment **link**, not invoice, not e-mandate. Hub `RazorpayGatewayAdapter` judgment only.

---

## R10.1

- [x] `Gateways/RazorpayHosted.cs`, `Provider = "razorpay"`
- [x] HttpClient to `https://api.razorpay.com`
- [x] `CreateHostedUrlAsync` returns `short_url`
- [x] No `Razorpay.Api` package (R14)
- [x] No `ChargeOffSession` (R23)

## R10.2 Exit

- [x] Class compiles
- [x] Unblocked for R11
