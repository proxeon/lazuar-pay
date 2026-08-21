# D15 — Ban `organizations` / `users` / `members`

**Track:** Database · **Depends:** D14  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** NP-XX-014. Merchant existence stays in One. Pay does not grow a membership roster.

---

## D15.1 Forbidden tables

- [x] No table named `organizations` (or `tenants` / `workspaces` / `org_map` as SoT)
- [x] No table named `users` (or `global_users`)
- [x] No table named `members` (membership). `MemberGate` C# is not a table
- [x] No `REFERENCES organizations(id)` — there is no such table
- [x] Money rows copy One tenant id as `org_id`. They do not FK a Pay org row

## D15.2 Proof

- [x] Test **or** migration review: migrations/SQL under `apps/lazuar-pay` do not `CREATE TABLE` the names in D15.1
- [x] Do **not** cache `/me` into a `users` table

## D15.3 Not this file

- [x] Thin `org_settings` keyed by text/uuid One tenant id is **D19**, not this file
- [x] Do not add `name` / `logo` / roster columns “for the org table we refused”

## D15.4 Exit

- [x] D15.2 proof exists and is green
- [x] Unblocked for D16 (D19 after D16)
