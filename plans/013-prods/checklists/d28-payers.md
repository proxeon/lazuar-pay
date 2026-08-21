# D28 — `payers`

**Track:** Database · **Depends:** D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-BUY-001. Buyer profile in Pay. Not a Zitadel human.

---

## D28.1 Table

- [x] `payers`: `email`, `name`, `org_id` (merchant’s One tenant id)
- [x] Small profile. Optional phone is fine
- [x] **Not** `zitadel_user_id` / `one_user_id` / `global_user_id`

## D28.2 Legal / tax

- [x] TIN / IdType / address as **legal** columns are **not** required
- [x] Do not copy Hub `crm.ClientProfiles` TIN theatre

## D28.3 Refuse

- [x] Do not create Zitadel humans from payers
- [x] Do not store a password
- [x] Buyers are not One members

## D28.4 Exit

- [x] Table exists; identity columns for One/Zitadel are absent
- [x] Unblocked for D29
