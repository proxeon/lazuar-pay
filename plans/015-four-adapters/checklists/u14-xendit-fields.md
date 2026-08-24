# U14 — Xendit fields: secret + callback token

**Track:** Merchant UI · **Depends:** U10, X11  
**Analysis:** [00](../00-what-must-be-done.md) §5.3  
**IDs:** —  
**Goal:** Operable form. Amber: reminder-only.

---

## U14.1

- [x] Secret key
- [x] Callback token (`x-callback-token`)
- [x] Webhook URL hint: `/v1/webhooks/xendit/{orgId}`
- [x] Copy: wallets/DuitNow appear on Xendit’s page if you enabled them there; Pay does not draw them
- [x] Copy: we do not auto-debit (U19)

## U14.2 Exit

- [x] Two secrets + copy
