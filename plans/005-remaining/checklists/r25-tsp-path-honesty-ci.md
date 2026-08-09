# R25 — OpenAPI ↔ Minimal API path honesty CI

**Track:** TypeSpec · **Analysis:** `../08-typespec-wave-b.md` § CI gate  
**Depends on:** R20–R24 progress or allowlist ready

---

## R25.1 Design

- [ ] Script/test: OpenAPI paths ⊆ Minimal API maps
- [ ] Minimal ⊆ OpenAPI ∪ **allowlist** (unsubscribe, Resend webhook, gateway webhooks, etc.)
- [ ] Allowlist file e.g. `packages/api-spec/honesty-allowlist.yaml` with reasons

## R25.2 Implement

- [ ] Add tool under `scripts/` or test project
- [ ] Wire into `.github/workflows/ci.yml` contracts job after `task gen`
- [ ] Document how to update allowlist

## R25.3 Exit

- [ ] CI fails on new silent drift
- [ ] FW-6 CI item closed in FUTURE-WORK.md
