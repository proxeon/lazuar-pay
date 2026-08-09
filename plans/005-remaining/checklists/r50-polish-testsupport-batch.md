# R50 — TestSupport migration batch

**Track:** Polish · **Analysis:** `../09-polish-godfiles-testsupport.md`  
**Goal:** Migrate N ModuleTests off copy-paste fixtures

---

## R50.1 Select batch

- [ ] Target N = 4–6 (recommended first batch)
- [ ] Prefer: One webhook tests, Billing event-handler fixtures, one Commerce fixture
- [ ] Skip: WebApplicationFactory auth suites, mediator-heavy LHDN/provision for later

## R50.2 Migrate

- [ ] Use `Lazuar.TestSupport` FakeExecutionContext + InMemory helpers
- [ ] Delete local NoopMediator duplicates where possible
- [ ] All migrated tests green

## R50.3 Exit

- [ ] Batch merged; list remaining high-copy suites for next batch
