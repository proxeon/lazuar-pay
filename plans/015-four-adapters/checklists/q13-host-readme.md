# Q13 — Host README matches Postgres + rails

**Track:** Q · **Depends:** Q12  
**Analysis:** live `apps/lazuar-pay/README.md` still says in-memory fixture  
**IDs:** —  
**Goal:** Dogfood DX is honest.

---

## Q13.1

- [ ] Replace “Checkout is an in-memory fixture (`status: open`). Not a real charge.”
- [ ] Say: Postgres `lazuar_pay` :5435, Stripe + CHIP + Billplz + Xendit + Razorpay hosted_link, webhook fulfills `RCPT-`
- [ ] Tax out: Official Receipt, not e-invoice
- [ ] One active provider per org
- [ ] `Pay:PublicBaseUrl` for Billplz (B29)
- [ ] `Pay:WrapKey`, per-org `whsec_` / PEM
- [ ] Listen 8081; Hub off when One uses 8080

## Q13.2 Exit

- [ ] README sentences a file-open can defend
