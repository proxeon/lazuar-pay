# D15 — Ban `organizations` / `users` / `members`

**Track:** Database · **Depends:** D14  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-XX-014. Merchant existence stays in One. Pay does not grow a membership roster.

---

## D15.1 Forbidden tables

- [ ] No table named `organizations` (or `tenants` / `workspaces` / `org_map` as SoT)
- [ ] No table named `users` (or `global_users`)
- [ ] No table named `members` (membership). `MemberGate` C# is not a table
- [ ] No `REFERENCES organizations(id)` — there is no such table
- [ ] Money rows copy One tenant id as `org_id`. They do not FK a Pay org row

## D15.2 Proof

- [ ] Test **or** migration review: migrations/SQL under `apps/lazuar-pay` do not `CREATE TABLE` the names in D15.1
- [ ] Do **not** cache `/me` into a `users` table

## D15.3 Not this file

- [ ] Thin `org_settings` keyed by text/uuid One tenant id is **D19**, not this file
- [ ] Do not add `name` / `logo` / roster columns “for the org table we refused”

## D15.4 Exit

- [ ] D15.2 proof exists and is green
- [ ] Unblocked for D16 (D19 after D16)
