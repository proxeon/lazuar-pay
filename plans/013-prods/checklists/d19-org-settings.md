# D19 — Thin `org_settings`

**Track:** Database · **Depends:** D15, D16  
**Analysis:** [03](../03-host-production-seams.md), [09](../09-data-migration.md)  
**Goal:** Pay-only settings keyed by One tenant id. Not membership.

---

## D19.1 Table

- [ ] `org_settings` with `org_id` **PK** = One tenant id (text/uuid copy, not a Pay-minted id)
- [ ] `currency` default **`MYR`**
- [ ] `charges_paused` bool default **`false`** (for O16 `tenant.suspended`)
- [ ] No `REFERENCES organizations(id)`

## D19.2 Not One / Hub org

- [ ] **Not** `name` / `logo` / slug uniqueness as Pay SoT
- [ ] **Not** a membership roster (`members[]`, roles)
- [ ] **Not** `users` / `global_users` / `zitadel_org_id`
- [ ] Do not seed a row on `tenant.created`

## D19.3 Isolation

- [ ] This is not NP-XX-014. It is a thin FK-shaped row to a person you cannot JOIN
- [ ] Whoami remains `GET /v1/whoami` → One `/me`. Do not cache tenants here

## D19.4 Exit

- [ ] Table exists; D15 grep still finds no `organizations` / `users` / `members`
- [ ] Unblocked for D20
