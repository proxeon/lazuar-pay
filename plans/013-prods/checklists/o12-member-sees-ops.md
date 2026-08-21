# O12 — Invited `member` sees ops

**Track:** One extras · **Depends:** O10, F21  
**Analysis:** [08](../08-one-identity-production.md) §5.3  
**IDs:** NP-ONE-022  
**Goal:** Invited `member` can read payments/receipt on `:5178`. Cannot paste keys.

---

## O12.1 Allowed

- [x] Invited user role `member` can `GET` payments (F19) and receipt (F20)
- [x] `:5178` shows the payment + `RCPT-` (F21) for that member
- [x] `authz/check` `member` on the path org allows the read

## O12.2 Forbidden for member

- [x] Cannot G12 `PUT` keys — already G14 (`member` 403 on write)
- [x] Do not invent VIEWER; do not mark NP-ONE-021 done via `check(member)`

## O12.3 Proof

- [x] Live **or** hermetic with **two** tokens (owner/admin vs invited member)
- [x] Hermetic: fake One allows member on GET payments/receipt; PUT keys still 403
- [x] Prefer hermetic as the merge gate; live is M26-class runbook

## O12.4 Exit

- [x] Two-token proof exists
- [x] Unblocked for B99 member sentence
