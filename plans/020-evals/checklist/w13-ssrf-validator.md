# W13 — Outbound URL SSRF rules

**Track:** W · **Depends:** K00  
**Analysis:** [`../03-outbound-webhooks.md`](../03-outbound-webhooks.md) §9.4.8; One URL validator as **judgment** only  
**Goal:** Pay does not POST to metadata IPs. Laptop sample still works in Testing.

**Why:** One blocks loopback (Pay inbound dogfood needs a tunnel). Plane C is the opposite problem: a writer could register `http://169.254.169.254/`. Steal One’s **judgment**, write a Pay-owned parser. Testing must allow 127.0.0.1 for E14.

**Related files**

| Path | Role today |
|------|------------|
| Sibling One `WebhookUrlValidator.cs` | Pattern only — do not copy type |
| `apps/lazuar-pay/src/Lazuar.Pay/Rails/Billplz/BillplzHosted.cs` | Public https callback — related SSRF family |
| `apps/lazuar-pay/tests/Lazuar.Pay.Tests/Hosting/CorsTests.cs` | Factory Production-shaped tests pattern |

**Current (`6d730d15`):** No outbound URL validator (no outbound).

---

## W13.1

- [x] Require `http` or `https` URL
- [x] Reject `169.254.169.254`, `10.0.0.0/8`, `192.168.0.0/16`, `172.16.0.0/12` in **all** environments (or document RFC1918 allow — **default reject private**)
- [x] Reject loopback in Production/Staging
- [x] Testing/Development: allow `127.0.0.1` / `localhost` (W24)
- [x] No redirects later on the worker (W20)

## W13.2 Tests (can land with W25)

- [x] `http://169.254.169.254/` → 400 even in Testing
- [x] Unit tests on the validator; register door uses it in W14

## W13.3 Must not

- [x] Do not copy One encryption key
- [x] Do not call One `WebhookUrlValidator` type

## W13.4 Exit

- [x] Unblocked for W14
