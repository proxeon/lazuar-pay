# U14 — Xendit fields: secret + callback token

**Track:** Merchant UI · **Depends:** U10, X11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** —  
**Goal:** Operable form. Amber: reminder-only.

---

## U14.1

- [ ] Secret key
- [ ] Callback token (`x-callback-token`)
- [ ] Webhook URL hint: `/v1/webhooks/xendit/{orgId}`
- [ ] Copy: wallets/DuitNow appear on Xendit’s page if you enabled them there; Pay does not draw them
- [ ] Copy: we do not auto-debit (U19)

## U14.2 Exit

- [ ] Two secrets + copy
