# K18 — Payer name + email (NP-BUY-001)

**Track:** Buyer page · **Depends:** K12  
**Analysis:** [05](../05-checkout-frontend.md) §7  
**Goal:** Collect name + email before start **or** on start body. Persist on checkout/payer. Not TIN theatre.  
**011:** NP-BUY-001

---

## K18.1 Collect

- [ ] Name **and** email required before hop-2 (form on `:5179` and/or POST start body)
- [ ] `POST /v1/pay/{token}/start` accepts optional `name` / `email`; persist when present
- [ ] If merchant already put payer email on the session, field may be read-only / confirm

## K18.2 Persist

- [ ] Store on the Pay checkout row and/or D28 `payers` — **Pay**, not Zitadel
- [ ] Pass the real name to hop-2 later (not `ExtractName(email)` local-part)

## K18.3 Must not

- [ ] No TIN required; no Hub CRM `IdType` / BRN / NRIC / MyInvois validate
- [ ] No “create account” / password / Google sign-in
- [ ] No `POST /tenants/{id}/members/invite` for the buyer
- [ ] No One `user_id` as the payer key (Ada buying her own product is still a guest)

## K18.4 Exit

- [ ] Start without name+email fails closed **or** the page blocks until filled
- [ ] NP-BUY-001 may flip when a human pay path stores both fields
- [ ] Unblocked for a real G17 start that has a customer name
