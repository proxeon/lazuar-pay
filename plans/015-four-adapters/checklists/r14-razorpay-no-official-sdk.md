# R14 — No Razorpay.Api package

**Track:** Razorpay · **Depends:** A00, H21  
**Analysis:** [00](../00-what-must-be-done.md) §3.5 / decisions.md  
**IDs:** —  
**Goal:** Hub SDK is gravity. HTTP is enough for payment links + HMAC.

---

## R14.1

- [x] `Lazuar.Pay.csproj` has no `Razorpay.Api`
- [x] IsolationTests may grep `Razorpay.Api` in csproj
- [x] If HTTP is blocked in real dogfood, **amend A00** before adding the package

## R14.2 Exit

- [x] csproj clean
