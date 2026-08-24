# C10 — ChipHosted next to StripeHosted

**Track:** CHIP · **Depends:** P27, H12, H21  
**Analysis:** [00](../00-what-must-be-done.md) §5.1  
**IDs:** NP-GW-003  
**Goal:** First remaining rail is a small class, not a copy of `ChipCollectGatewayAdapter.cs`.

---

## C10.1 File

- [x] Add `apps/lazuar-pay/src/Lazuar.Pay/Gateways/ChipHosted.cs`
- [x] `public const string Provider = "chip";` (lowercase)
- [x] Constructor: `PayDbContext`, `SecretBox`, `HttpClient` (or `IHttpClientFactory` typed client — **not** Hub `PublicDnsFallback` name)
- [x] `CreateHostedUrlAsync(CheckoutRow, CancellationToken)` returns `checkout_url`
- [x] `Program.cs` `AddScoped<ChipHosted>()` and `AddHttpClient` if needed
- [x] Read Hub `ChipCollectGatewayAdapter.cs` as **judgment only**

## C10.2 Must not

- [x] Do not copy the file, MediatR, or `IPaymentGatewayAdapter`
- [x] Do not implement `ChargeOffSessionAsync` / refunds / portal
- [x] Do not add CHIP NuGet (C29)

## C10.3 Exit

- [x] Class compiles; IsolationTests green
- [x] Unblocked for C11, C12
