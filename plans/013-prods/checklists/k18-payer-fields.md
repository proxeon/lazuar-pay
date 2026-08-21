# K18 — Payer name + email (NP-BUY-001)

**Track:** Buyer page · **Depends:** K12  
**Analysis:** [05](../05-checkout-frontend.md) §7  
**Goal:** Collect name + email before start **or** on start body. Persist on checkout/payer. Not TIN theatre.  
**011:** NP-BUY-001

---

## K18.1 Collect

- [x] Name **and** email required before hop-2 (form on `:5179` and/or POST start body)
- [x] `POST /v1/pay/{token}/start` accepts optional `name` / `email`; persist when present
- [x] If merchant already put payer email on the session, field may be read-only / confirm

## K18.2 Persist

- [x] Store on the Pay checkout row and/or D28 `payers` — **Pay**, not Zitadel
- [x] Pass the real name to hop-2 later (not `ExtractName(email)` local-part)

## K18.3 Must not

- [x] No TIN required; no Hub CRM `IdType` / BRN / NRIC / MyInvois validate
- [x] No “create account” / password / Google sign-in
- [x] No `POST /tenants/{id}/members/invite` for the buyer
- [x] No One `user_id` as the payer key (Ada buying her own product is still a guest)

## K18.4 Exit

- [x] Start without name+email fails closed **or** the page blocks until filled
- [x] NP-BUY-001 may flip when a human pay path stores both fields
- [x] Unblocked for a real G17 start that has a customer name
