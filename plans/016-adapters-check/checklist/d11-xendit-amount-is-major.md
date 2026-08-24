# D11 — Xendit `paid_amount` is major units

**Track:** Units · **Depends:** A00  
**Analysis:** [`../07-xendit-crosscheck.md`](../07-xendit-crosscheck.md); live `MoneyMath.ToMinor(paid_amount)`; test `paid_amount:10`  
**IDs:** —  
**Goal:** Do not send cents to `/v2/invoices`. Do not parse cents on the webhook.

---

## D11.1

- [ ] Comment on `XenditWebhook` and `XenditHosted`: invoice amount is **major** (10.00), then `ToMinor` for match
- [ ] Create must keep major JSON (not 1000)

## D11.2 Must not

- [ ] Do not “fix” a mismatch by treating 1000 as RM10 on Xendit
- [ ] fx20 amount mismatch uses `paid_amount: 9.99` not `999`

## D11.3 Exit

- [ ] Comment exists
- [ ] Unblocked for fx20
