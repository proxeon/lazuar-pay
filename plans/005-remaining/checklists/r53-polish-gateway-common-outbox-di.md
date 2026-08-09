# R53 — GatewayCommon + outbox DI pilot

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`

---

## R53.1 GatewayCommon

- [x] Extract shared ExtractName / minor unit helpers used by payment adapters
- [x] **No** mega abstract gateway base class
- [x] Adapters call helpers only

## R53.2 Outbox/inbox DI pilot

- [x] Design `AddModuleOutboxInbox<TDbContext>` (Option A: keep thin job subclasses)
- [x] Optional `ApplyOutboxInbox` EF helper
- [x] Pilot on **CRM** (or agreed small module)
- [x] Zero EF migrations required
- [x] Arch tests + Lhdn registration tests still green if touched later

## R53.3 Optional ProblemDetails

- [ ] Expand stable codes on LHDN documents or One provision when editing those endpoints *(skipped — opportunistic only)*

## R53.4 Exit

- [x] At least GatewayCommon **or** outbox pilot landed *(both landed)*
