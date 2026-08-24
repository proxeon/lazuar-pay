# P27 — Small hosted-rail seam when the second class exists

**Track:** Provider door · **Depends:** P17, P25, P26  
**Analysis:** [00](../00-what-must-be-done.md) §3.4  
**IDs:** —  
**Goal:** CHIP looks like `StripeHosted`, not like `ChipCollectGatewayAdapter`.

---

## P27.1 Shape (introduce with C10, not before)

- [x] Optional interface **only when** the second class exists:
  - `string Provider { get; }` (lowercase)
  - `Task<string> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)`
- [x] Parse is **not** required on the interface (webhook handler owns verify)
- [x] Throw `InvalidOperationException("rail not configured")` when creds missing — Start maps to 503 (live Stripe)

## P27.2 Must not

- [x] Do not add refund/off-session/portal to this interface
- [x] Do not name it `IPaymentGatewayAdapter`

## P27.3 Exit

- [x] `StripeHosted` can implement it or stay concrete; CHIP matches the method
- [x] Unblocked for C10
