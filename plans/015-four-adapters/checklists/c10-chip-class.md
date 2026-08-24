# C10 — ChipHosted next to StripeHosted

**Track:** CHIP · **Depends:** P27, H12, H21  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-003  
**Goal:** First remaining rail is a small class, not a copy of `ChipCollectGatewayAdapter.cs`.

---

## C10.1 File

- [ ] Add `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs`
- [ ] `public const string Provider = "chip";` (lowercase)
- [ ] Constructor: `PayDbContext`, `SecretBox`, `HttpClient` (or `IHttpClientFactory` typed client — **not** Hub `PublicDnsFallback` name)
- [ ] `CreateHostedUrlAsync(CheckoutRow, CancellationToken)` returns `checkout_url`
- [ ] `Program.cs` `AddScoped<ChipHosted>()` and `AddHttpClient` if needed
- [ ] Read Hub `ChipCollectGatewayAdapter.cs` as **judgment only**

## C10.2 Must not

- [ ] Do not copy the file, MediatR, or `IPaymentGatewayAdapter`
- [ ] Do not implement `ChargeOffSessionAsync` / refunds / portal
- [ ] Do not add CHIP NuGet (C29)

## C10.3 Exit

- [ ] Class compiles; IsolationTests green
- [ ] Unblocked for C11, C12
