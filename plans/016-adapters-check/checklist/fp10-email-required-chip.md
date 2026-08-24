# fp10 — Public GET email_required true when active chip

**Track:** Fill public · **Depends:** I15  
**Analysis:** 09 method 62; P19.2 / K11 host half  
**Goal:** `PublicPayTests.Email_required_true_when_active_chip`

---

- [ ] PUT chip, create checkout, **no** start
- [ ] GET `/v1/pay/{token}` without Bearer
- [ ] JSON `email_required === true`
- [ ] Exit: green
