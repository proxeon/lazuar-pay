# R25 — OpenAPI ↔ Minimal API path honesty CI

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md` § CI gate  
**Depends on:** R20–R24 progress or allowlist ready  
**Notes:** `../r25-notes.md`

---

## R25.1 Design

- [x] Script/test: OpenAPI paths ⊆ Minimal API maps
- [x] Minimal ⊆ OpenAPI ∪ **allowlist** (unsubscribe, Resend webhook, gateway webhooks, etc.)
- [x] Allowlist file e.g. `packages/api-spec/honesty-allowlist.yaml` with reasons

## R25.2 Implement

- [x] Add tool under `scripts/` or test project
- [x] Wire into `.github/workflows/ci.yml` contracts job after `task gen`
- [x] Document how to update allowlist

## R25.3 Exit

- [x] CI fails on new silent drift
- [x] FW-6 CI item closed in FUTURE-WORK.md
