# R53 — GatewayCommon + outbox DI pilot

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`

---

## R53.1 GatewayCommon

- [ ] Extract shared ExtractName / minor unit helpers used by payment adapters
- [ ] **No** mega abstract gateway base class
- [ ] Adapters call helpers only

## R53.2 Outbox/inbox DI pilot

- [ ] Design `AddModuleOutboxInbox<TDbContext>` (Option A: keep thin job subclasses)
- [ ] Optional `ApplyOutboxInbox` EF helper
- [ ] Pilot on **CRM** (or agreed small module)
- [ ] Zero EF migrations required
- [ ] Arch tests + Lhdn registration tests still green if touched later

## R53.3 Optional ProblemDetails

- [ ] Expand stable codes on LHDN documents or One provision when editing those endpoints

## R53.4 Exit

- [ ] At least GatewayCommon **or** outbox pilot landed
