# C29 — CHIP is HttpClient, no extra package

**Track:** CHIP · **Depends:** C10  
**Analysis:** [00](../00-what-must-be-done.md) §3.5  
**IDs:** —  
**Goal:** Host csproj stays EF + Stripe.net (+ Design). CHIP is raw HTTP.

---

## C29.1

- [ ] No CHIP / ChipIn package on `Lazuar.Pay.csproj`
- [ ] Use `HttpClient` / `IHttpClientFactory`

## C29.2 Exit

- [ ] csproj diff has no CHIP package
