# R50 — TestSupport migration batch

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Goal:** Migrate N ModuleTests off copy-paste fixtures  
**Notes:** `../r50-notes.md`

---

## R50.1 Select batch

- [x] Target N = 4–6 (recommended first batch) → **N = 6**
- [x] Prefer: One webhook tests, Billing event-handler fixtures, one Commerce fixture
- [x] Skip: WebApplicationFactory auth suites, mediator-heavy LHDN/provision for later

## R50.2 Migrate

- [x] Use `Lazuar.TestSupport` FakeExecutionContext + InMemory helpers
- [x] Delete local NoopMediator duplicates where possible (`OutboundWebhookTests` local types removed)
- [x] All migrated tests green (40 passed)

## R50.3 Exit

- [x] Batch done; list remaining high-copy suites for next batch → `../r50-notes.md`
