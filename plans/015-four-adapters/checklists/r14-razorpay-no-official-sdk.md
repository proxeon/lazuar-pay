# R14 — No Razorpay.Api package

**Track:** Razorpay · **Depends:** A00, H21  
**Analysis:** [00](../00-what-must-be-done.md) §3.5 / decisions.md  
**IDs:** —  
**Goal:** Hub SDK is gravity. HTTP is enough for payment links + HMAC.

---

## R14.1

- [ ] `Lazuar.Pay.csproj` has no `Razorpay.Api`
- [ ] IsolationTests may grep `Razorpay.Api` in csproj
- [ ] If HTTP is blocked in real dogfood, **amend A00** before adding the package

## R14.2 Exit

- [ ] csproj clean
