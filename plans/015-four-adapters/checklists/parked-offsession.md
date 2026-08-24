# Parked — Off-session / vault auto-debit

**Do not start in 015.**  
**Analysis:** [00](../00-what-must-be-done.md) §9; C15, C21, R23, B25

---

- [ ] Stripe PaymentMethod / setup-intent extract (Hub) — steal PM, **not** `PAYMENT_COMPLETED`
- [ ] CHIP `force_recurring` / token charge — only with a real token
- [ ] Billplz / Xendit / Razorpay stay reminder-only even later unless a named soak proves otherwise
- [ ] Capability may become `vaulted` only after a real PM exists
- [ ] `NP-FUL-004` renew job is Bar C / parked
