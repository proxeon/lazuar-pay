# F04 — Drop/archive `lhdn.DeveloperApiKeys` (optional later)

**Goal:** Remove dead table after One-only is stable.  
**Depends on:** F03 live in prod ≥ **30 days** (or explicit waiver)

---

## F04.1 Preflight

- [ ] One-only live since: ________
- [ ] Days elapsed ≥ 30: yes / waiver ________
- [ ] No dual-read code remaining
- [ ] No application references to DeveloperApiKeys write/read

## F04.2 Migration

- [ ] EF migration: drop table **or** rename to archive schema
- [ ] Remove Lhdn domain aggregates/repos if unused
- [ ] Clean DI registrations
- [ ] Architecture / module tests green

## F04.3 Exit

- [ ] Table gone or archived
- [ ] FW-1 fully closed in FUTURE-WORK.md
